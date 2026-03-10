using Adliance.Buddy.DateTime;
using Adliance.Kimai.Client;
using Adliance.Kimai.Client.Extensions;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.Extensions.Options;

namespace Adliance.Kimai.Vacations;

public class VacationICalService(IOptions<KimaiSettings> settings)
{
    public async Task<string> GetICalFeedAsync()
    {
        using var client = new KimaiClient(settings.Value.BaseUrl, settings.Value.ApiKey);

        var users = await client.GetUsersAsync();
        var absences = await client.GetAbsencesAsync(users.Select(u => u.Id));

        var approvedVacations = absences
            .Where(a => a is { IsVacation: true, Status: "approved" })
            .OrderBy(a => a.Date)
            .ToList();

        var calendar = new Calendar();
        calendar.Properties.Add(new CalendarProperty("X-WR-CALNAME", "Vacations"));

        var byUser = approvedVacations
            .GroupBy(a => a.User?.Id ?? 0)
            .OrderBy(g => g.First().User?.Title ?? g.First().User?.Username ?? "");

        foreach (var group in byUser)
        {
            var userName = group.First().User?.Title ?? group.First().User?.Username ?? "Unknown";
            var days = group.Select(a => a.DateOnly).OrderBy(d => d).ToList();

            foreach (var (start, end) in GroupConsecutiveDays(days))
            {
                var ev = new CalendarEvent
                {
                    Summary = $"Urlaub: {userName}",
                    DtStart = new CalDateTime(start, "Europe/Vienna"),
                    DtEnd = new CalDateTime(end.AddDays(1), "Europe/Vienna"),
                    Uid = $"{group.Key}-{start:yyyy-MM-dd}-{end:yyyy-MM-dd}"
                };
                calendar.Events.Add(ev);
            }
        }

        return new CalendarSerializer().SerializeToString(calendar) ?? string.Empty;
    }

    private static IEnumerable<(DateTime Start, DateTime End)> GroupConsecutiveDays(List<DateOnly> days)
    {
        if (days.Count == 0) yield break;

        var start = new DateTime(days[0].Year, days[0].Month, days[0].Day).UtcToCet();
        var end = new DateTime(days[0].Year, days[0].Month, days[0].Day).UtcToCet();

        for (var i = 1; i < days.Count; i++)
        {
            yield return (start, end);
            start = new DateTime(days[i].Year, days[i].Month, days[i].Day).UtcToCet();
            end = new DateTime(days[i].Year, days[i].Month, days[i].Day).UtcToCet();
        }

        yield return (start, end);
    }
}
