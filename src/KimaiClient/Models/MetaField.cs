using System.Text.Json.Serialization;

namespace Adliance.Kimai.KimaiClient.Models;

public class MetaField
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("value")] public string Value { get; set; } = string.Empty;
}
