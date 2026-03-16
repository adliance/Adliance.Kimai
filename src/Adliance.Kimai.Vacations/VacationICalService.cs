using Adliance.Buddy.DateTime;
using Adliance.Kimai.Client;
using Adliance.Kimai.Client.Extensions;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.Extensions.Options;

namespace Adliance.Kimai.Vacations;

public class VacationICalService(IOptions<KimaiOptions> kimaiOptions, IOptions<AuthOptions> authOptions)
{
    public async Task<string> GetICalFeedAsync(string? key)
    {
        if (!string.IsNullOrWhiteSpace(authOptions.Value.ICalAccessKey) && !authOptions.Value.ICalAccessKey.Equals(key, StringComparison.InvariantCultureIgnoreCase)) throw new UnauthorizedAccessException();

        using var client = new KimaiClient(kimaiOptions.Value.BaseUrl, kimaiOptions.Value.ApiKey);

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
                    DtStart = new CalDateTime(start.Year, start.Month, start.Day),
                    DtEnd = new CalDateTime(end.Year, end.Month, end.Day),
                    Uid = $"{group.Key}-{start:yyyy-MM-dd}-{end:yyyy-MM-dd}"
                };
                calendar.Events.Add(ev);
            }
        }

        return new CalendarSerializer().SerializeToString(calendar) ?? string.Empty;
    }

    private static IEnumerable<(DateOnly Start, DateOnly End)> GroupConsecutiveDays(List<DateOnly> days)
    {
        if (days.Count == 0) yield break;

        var start = days[0];
        var end = days[0];

        for (var i = 1; i < days.Count; i++)
        {
            if (days[i].DayNumber - end.DayNumber <= 1)
            {
                end = days[i];
            }
            else
            {
                yield return (start.AddDays(1), end.AddDays(2)); // increase days to correct for UTC -> CET
                start = days[i];
                end = days[i];
            }
        }

        yield return (start.AddDays(1), end.AddDays(2));
    }
}
