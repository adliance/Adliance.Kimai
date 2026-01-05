using System.Text.Json.Serialization;

namespace Adliance.Kimai.KimaiClient.Models;

public class User
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;

    /*
    [JsonPropertyName("apiToken")] public bool ApiToken { get; set; }
    [JsonPropertyName("initials")] public string Initials { get; set; } = string.Empty;
    [JsonPropertyName("alias")] public string Alias { get; set; } = string.Empty;
    [JsonPropertyName("avatar")] public string Avatar { get; set; } = string.Empty;
    [JsonPropertyName("accountNumber")] public string AccountNumber { get; set; } = string.Empty;
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("color")] public string Color { get; set; } = string.Empty;
    */
}
