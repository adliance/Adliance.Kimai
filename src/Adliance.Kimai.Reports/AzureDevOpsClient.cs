using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Adliance.Kimai.Client;

namespace Adliance.Kimai.Reports;

public class AzureDevOpsClient
{
    private readonly HttpClient _http;

    public AzureDevOpsClient(string organizationUrl, string personalAccessToken)
    {
        _http = new HttpClient();
        _http.BaseAddress = new Uri(organizationUrl.TrimEnd('/') + "/");
        var encodedPat = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedPat);
    }

    public async Task<List<WorkItem>> LoadWorkItems(IEnumerable<int> ids)
    {
        var idList = ids.OrderBy(x => x).ToList();
        if (idList.Count == 0) return [];

        var results = new List<WorkItem>();

        // ADO allows max 200 IDs per request
        foreach (var batch in idList.Chunk(25))
        {
            var batchResults = await FetchBatch(batch);
            if (batchResults != null)
            {
                results.AddRange(batchResults);
            }
            else
            {
                // Batch failed (e.g. one or more IDs don't exist or are inaccessible) — fall back to one-by-one
                foreach (var id in batch)
                {
                    var single = await FetchBatch([id]);
                    if (single != null) results.AddRange(single);
                }
            }
        }

        return results;
    }

    // Returns null if the request failed (e.g. an ID doesn't exist or is inaccessible).
    private async Task<List<WorkItem>?> FetchBatch(IEnumerable<int> ids)
    {
        var idsParam = string.Join(",", ids);
        var url = $"_apis/wit/workitems?ids={idsParam}&fields=System.Title,System.State,Microsoft.VSTS.Scheduling.OriginalEstimate&$expand=Links&api-version=7.1";
        var response = await _http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<WorkItemsResponse>(json, LenientJsonOptions.Instance);
        return result?.Value ?? [];
    }

    public class WorkItem
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("fields")] public WorkItemFields? Fields { get; set; }
        [JsonPropertyName("_links")] public WorkItemLinks? Links { get; set; }
    }

    public class WorkItemLinks
    {
        [JsonPropertyName("html")] public WorkItemHtmlLink? Html { get; set; }
    }

    public class WorkItemHtmlLink
    {
        [JsonPropertyName("href")] public string? Href { get; set; }
    }

    public class WorkItemFields
    {
        [JsonPropertyName("System.Title")] public string? Title { get; set; }
        [JsonPropertyName("System.State")] public string? State { get; set; }

        [JsonPropertyName("Microsoft.VSTS.Scheduling.OriginalEstimate")] public double? OriginalEstimate { get; set; }
    }

    private sealed class WorkItemsResponse
    {
        [JsonPropertyName("value")] public List<WorkItem>? Value { get; set; }
    }
}
