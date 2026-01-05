using System.Text.Json.Serialization;

namespace Adliance.Kimai.KimaiClient.Models;

public class PublicHoliday
{
    [JsonPropertyName("date")] public DateTime Date { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    public DateOnly DateOnly => DateOnly.FromDateTime(Date);

    /*
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("publicHolidayGroup")] public PublicHolidayGroup? PublicHolidayGroup { get; set; }
    [JsonPropertyName("halfDay")] public bool HalfDay { get; set; }
    */
}
