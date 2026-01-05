using System.Text.Json.Serialization;

namespace Adliance.Kimai.KimaiClient.Models;

public class Customer
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    /*
    [JsonPropertyName("number")] public string Number { get; set; } = string.Empty;
    [JsonPropertyName("comment")] public string Comment { get; set; } = string.Empty;
    [JsonPropertyName("visible")] public bool Visible { get; set; }
    [JsonPropertyName("billable")] public bool Billable { get; set; }
    [JsonPropertyName("company")] public string Company { get; set; } = string.Empty;
    [JsonPropertyName("country")] public string Country { get; set; } = string.Empty;
    [JsonPropertyName("currency")] public string Currency { get; set; } = string.Empty;
    [JsonPropertyName("phone")] public string Phone { get; set; } = string.Empty;
    [JsonPropertyName("fax")] public string Fax { get; set; } = string.Empty;
    [JsonPropertyName("mobile")] public string Mobile { get; set; } = string.Empty;
    [JsonPropertyName("homepage")] public string Homepage { get; set; } = string.Empty;
    [JsonPropertyName("timezone")] public string Timezone { get; set; } = string.Empty;
    [JsonPropertyName("metaFields")] public List<MetaField> MetaFields { get; set; } = [];
    [JsonPropertyName("teams")] public List<Team> Teams { get; set; } = [];
    [JsonPropertyName("color")] public string Color { get; set; } = string.Empty;
    */
}
