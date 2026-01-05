using System.Text.Json.Serialization;

namespace Adliance.Kimai.KimaiClient.Models;

public class Timesheet
{
    [JsonPropertyName("activity")] public int Activity { get; set; }
    [JsonPropertyName("project")] public int Project { get; set; }
    [JsonPropertyName("user")] public int User { get; set; }
    [JsonPropertyName("begin")] public DateTime Begin { get; set; }
    [JsonPropertyName("end")] public DateTime End { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("metaFields")] public List<MetaField> MetaFields { get; set; } = [];

    /*
    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = [];
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("duration")] public int Duration { get; set; }
    [JsonPropertyName("rate")] public double Rate { get; set; }
    [JsonPropertyName("internalRate")] public double InternalRate { get; set; }
    [JsonPropertyName("exported")] public bool Exported { get; set; }
    [JsonPropertyName("billable")] public bool Billable { get; set; }
    */

    public bool IsHomeOffice => MetaFields.Any(x => x is { Name: "homeoffice", Value: "1" });
}
