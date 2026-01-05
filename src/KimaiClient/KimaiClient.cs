using System.Globalization;
using System.Text.Json;
using Adliance.Kimai.KimaiClient.Models;

namespace Adliance.Kimai.KimaiClient;

public class KimaiClient : IDisposable
{
    private readonly HttpClient _client;

    public KimaiClient(string baseUrl, string apiKey)
    {
        _client = new HttpClient();
        _client.BaseAddress = new Uri(baseUrl);
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<List<Timesheet>> GetPaginatedTimesheets(int[] userIds)
    {
        var query = "";
        foreach (var userId in userIds) query += $"users[]={userId}&";
        return await GetPaginated<Timesheet>($"api/timesheets?{query.TrimEnd('&')}");
    }


    public async Task<List<T>> GetPaginated<T>(string url)
    {
        var result = new List<T>();
        const int size = 500; // max. allowed value at Kimai
        var currentPage = 1;
        var totalPages = int.MaxValue;

        while (currentPage <= totalPages)
        {
            if (url.Contains('?')) url += "&";
            else url += "?";
            url += $"size={size}&page={currentPage}";

            var response = await _client.GetAsync($"{url}");
            var responseString = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new Exception($"Error fetching data from Kimai ({response.StatusCode}).{Environment.NewLine}{responseString}");

            if (response.Headers.TryGetValues("X-Total-Pages", out var values))
            {
                totalPages = int.Parse(values.First(), CultureInfo.InvariantCulture);
            }
            else
            {
                totalPages = 1;
            }

            try
            {
                var responseObject = JsonSerializer.Deserialize<T[]>(responseString, LenientJsonOptions.Instance) ?? throw new Exception("Deserialized object was null.");
                result.AddRange(responseObject);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parsing response from Kimai.{Environment.NewLine}{responseString}", ex);
            }

            currentPage++;
        }

        return result;
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
