using System.Text.Json.Serialization;

namespace Adliance.Kimai.Client.Models;

public class Timesheet
{
    [JsonPropertyName("activity")] public int ActivityId { get; set; }
    [JsonPropertyName("project")] public int Project { get; set; }
    [JsonPropertyName("user")] public int UserId { get; set; }
    [JsonPropertyName("begin")] public DateTime Begin { get; set; }
    [JsonPropertyName("end")] public DateTime? End { get; set; } // yes, fucking hell, it's apparently possible that the API returns an entry without end date

    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("metaFields")] public List<MetaField> MetaFields { get; set; } = [];
    [JsonPropertyName("billable")] public bool IsBillable { get; set; }

    [JsonIgnore] public User? User { get; set; }
    [JsonIgnore] public Activity? Activity { get; set; }
    [JsonIgnore] public double DurationMinutes => End?.Subtract(Begin).TotalMinutes ?? 0; // Fall back to 0, as the time entry is not finished yet

    /*
    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = [];
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("duration")] public int Duration { get; set; }
    [JsonPropertyName("rate")] public double Rate { get; set; }
    [JsonPropertyName("internalRate")] public double InternalRate { get; set; }
    [JsonPropertyName("exported")] public bool Exported { get; set; }
    */

    public bool IsHomeOffice => MetaFields.Any(x => x is { Name: "homeoffice", Value: "1" });
}
