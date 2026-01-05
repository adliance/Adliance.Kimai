using System.Text.Json;
using System.Text.Json.Serialization;
using Adliance.Kimai.KimaiClient;
using Adliance.Kimai.KimaiClient.Models;

namespace Adliance.Kimai;

public class Data
{
    [JsonPropertyName("updated")] public DateTime Updated { get; init; }
    [JsonPropertyName("users")] public List<User> Users { get; init; } = [];
    [JsonPropertyName("activities")] public List<Activity> Activities { get; init; } = [];
    [JsonPropertyName("customers")] public List<Customer> Customers { get; init; } = [];
    [JsonPropertyName("projects")] public List<Project> Projects { get; init; } = [];
    [JsonPropertyName("timesheets")] public List<Timesheet> Timesheets { get; set; } = [];
    [JsonPropertyName("public_holidays")] public List<PublicHoliday> PublicHolidays { get; init; } = [];
    [JsonPropertyName("absences")] public List<Absence> Absences { get; init; } = [];

    public static async Task<Data> LoadFromCacheOrKimai(KimaiClient.KimaiClient client)
    {
        Data? data;
        try
        {
            data = await LoadFromCache();
        }
        catch
        {
            data = null;
        }

        if (data == null || data.Updated < DateTime.Now.AddDays(-1)) data = await LoadFromKimai(client);
        await data.SaveToCache();
        return data;
    }

    public static async Task<Data> LoadFromKimai(KimaiClient.KimaiClient client)
    {
        Console.Write("Loading data from Kimai ... ");
        var result = new Data
        {
            Updated = DateTime.Now,
            Users = await client.GetPaginated<User>("/api/users"),
            Customers = await client.GetPaginated<Customer>("/api/customers"),
            Projects = await client.GetPaginated<Project>("/api/projects"),
            Activities = await client.GetPaginated<Activity>("/api/activities"),
            PublicHolidays = await client.GetPaginated<PublicHoliday>("/api/public-holidays"),
            Absences = await client.GetPaginated<Absence>("/api/absences")
        };

        result.Timesheets = await client.GetPaginatedTimesheets(result.Users.Select(x => x.Id).ToArray());

        Console.WriteLine("done.");
        return result;
    }

    private const string CacheFileName = "data.json";

    public static async Task<Data> LoadFromCache()
    {
        try
        {
            var result = await JsonSerializer.DeserializeAsync<Data>(File.OpenRead(CacheFileName), LenientJsonOptions.Instance) ?? throw new Exception("Deserialized object was null.");
            Console.WriteLine("Loaded data from cache.");
            return result;
        }
        catch (Exception ex)
        {
            throw new Exception("Error loading data from cache.", ex);
        }
    }

    public async Task SaveToCache()
    {
        await File.WriteAllTextAsync(CacheFileName, JsonSerializer.Serialize(this, LenientJsonOptions.Instance));
    }

    public override string ToString()
    {
        return $"""
                {Users.Count} users
                {Customers.Count} customers
                {Projects.Count} projects
                {Activities.Count} activities
                {PublicHolidays.Count} holidays
                {Absences.Count} absences
                {Timesheets.Count} timesheets ({Timesheets.Min(x => x.Begin)} - {Timesheets.Max(x => x.End)})
                """;
    }
}
