using System.Text.Json.Serialization;

namespace Adliance.Kimai.KimaiClient.Models;

public class Absence
{
    [JsonPropertyName("user")] public User? User { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("date")] public DateTime Date { get; set; }

    public bool IsVacation => Type == "holiday";
    public DateOnly DateOnly => DateOnly.FromDateTime(Date);

    /*
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("duration")] public int? Duration { get; set; }
    [JsonPropertyName("halfDay")] public bool HalfDay { get; set; }
    */
}
