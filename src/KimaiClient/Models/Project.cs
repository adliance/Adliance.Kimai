using System.Text.Json.Serialization;

namespace Adliance.Kimai.KimaiClient.Models;

public class Project
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    /*
    [JsonPropertyName("parentTitle")] public string ParentTitle { get; set; } = string.Empty;
    [JsonPropertyName("customer")] public int Customer { get; set; }
    [JsonPropertyName("orderNumber")] public string OrderNumber { get; set; } = string.Empty;
    [JsonPropertyName("start")] public DateTime? Start { get; set; }
    [JsonPropertyName("end")] public DateTime? End { get; set; }
    [JsonPropertyName("comment")] public string Comment { get; set; } = string.Empty;
    [JsonPropertyName("visible")] public bool Visible { get; set; }
    [JsonPropertyName("billable")] public bool Billable { get; set; }
    [JsonPropertyName("metaFields")] public List<MetaField> MetaFields { get; set; } = [];
    [JsonPropertyName("teams")] public List<Team> Teams { get; set; } = [];
    [JsonPropertyName("globalActivities")] public bool GlobalActivities { get; set; }
    [JsonPropertyName("number")] public string Number { get; set; } = string.Empty;
    [JsonPropertyName("color")] public string Color { get; set; } = string.Empty;
    */
}
