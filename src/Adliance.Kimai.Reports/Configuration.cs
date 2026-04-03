using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Adliance.Kimai.Client;
using Adliance.Kimai.Reports.Extensions;

namespace Adliance.Kimai.Reports;

public class Configuration
{
    [JsonPropertyName("users")] public List<User> Users { get; init; } = [];

    public class User
    {
        [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;
        [JsonPropertyName("offset_worktime_minutes")] public double OffsetWorktimeMinutes { get; set; }
        [JsonPropertyName("offset_vacation_minutes")] public double OffsetVacationsMinutes { get; set; }
        [JsonPropertyName("employments")] public List<Employment> Employments { get; set; } = [];

        [JsonIgnore] public string Name { get; set; } = string.Empty;
        [JsonIgnore] public double ExpectedMinutes { get; set; }
        [JsonIgnore] public double WorkedTotalMinutes { get; set; }
        [JsonIgnore] public double WorkedBillableMinutes { get; set; }
        [JsonIgnore] public double RemainingVacationMinutes { get; set; }
        [JsonIgnore] public int HomeOfficeDays { get; set; }
        [JsonIgnore] public int PublicHolidayDays { get; set; }
        [JsonIgnore] public int VacationDays { get; set; }
        [JsonIgnore] public int OtherAbsenceDays { get; set; }
        [JsonIgnore] public bool FoundInKimai { get; set; }
        [JsonIgnore] public List<Warning> Warnings { get; set; } = [];
        [JsonIgnore] public double BillablePercent => 100d / WorkedTotalMinutes * WorkedBillableMinutes;
        [JsonIgnore] public double ExpectedBillablePercent { get; set; }

        public DateOnly GetLastEmploymentDay()
        {
            foreach (var e in Employments.OrderByDescending(x => x.End))
            {
                var currentDay = e.End;
                while (currentDay >= e.Begin)
                {
                    if (e.GetExpectedMinutes(currentDay) > 0) return currentDay;
                    currentDay = currentDay.AddDays(-1);
                }
            }

            throw new Exception("No last employment day found.");
        }

        public override string ToString() => Name;

        public record Warning(DateOnly Date, string Text);
    }

    public class Employment
    {
        [JsonPropertyName("begin")] public DateOnly Begin { get; set; }
        [JsonPropertyName("end")] public DateOnly End { get; set; } = DateOnly.MaxValue;
        [JsonPropertyName("minutes_per_day")] public double MinutesPerDay { get; set; } = 462;
        [JsonPropertyName("expected_billable_percent")] public double ExpectedBillablePercent { get; set; } = 95;

        [JsonPropertyName("weekdays")]
        public DayOfWeek[] Weekdays { get; set; } =
        [
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday
        ];

        public double GetExpectedMinutes(DateOnly date)
        {
            if (date < Begin || date > End) return 0;
            if (!Weekdays.Contains(date.DayOfWeek)) return 0;
            return MinutesPerDay;
        }

        public double? GetExpectedBillablePercent(DateOnly date)
        {
            if (date < Begin || date > End) return null;
            if (!Weekdays.Contains(date.DayOfWeek)) return null;
            return ExpectedBillablePercent;
        }
    }

    public static async Task<Configuration> Load(string filePath)
    {
        try
        {
            await using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<Configuration>(stream, LenientJsonOptions.Instance) ?? throw new Exception("Deserialized object was null.");
        }
        catch (Exception ex)
        {
            throw new Exception("Error loading configuration from cache.", ex);
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var u in Users)
        {
            var day = u.GetLastEmploymentDay();
            var overtime = u.WorkedTotalMinutes - u.ExpectedMinutes;
            var vacationDays = day.MinutesToDays(u.RemainingVacationMinutes, u);
            var vacationOffsetDays = day.MinutesToDays(u.OffsetVacationsMinutes, u);

            sb.AppendLine(CultureInfo.InvariantCulture, $"{u.Name}:");
            sb.AppendLine(CultureInfo.InvariantCulture, $"\tExpected: {u.ExpectedMinutes / 60d:N2}h.");
            sb.AppendLine(CultureInfo.InvariantCulture, $"\tWorked: {u.WorkedTotalMinutes / 60d:N2}h.");
            sb.AppendLine(CultureInfo.InvariantCulture, $"\tOvertime: {overtime / 60d:N2}h + {u.OffsetWorktimeMinutes / 60d:N2}h = {(overtime + u.OffsetWorktimeMinutes) / 60d:N2}h.");
            sb.AppendLine(CultureInfo.InvariantCulture, $"\tVacation: {vacationDays:N2} days + {vacationOffsetDays:N2} days = {vacationDays + vacationOffsetDays:N2} days.");
            sb.AppendLine(CultureInfo.InvariantCulture, $"\tHome Office: {u.HomeOfficeDays:N0} days.");
            sb.AppendLine(CultureInfo.InvariantCulture, $"\tPublic Holidays: {u.PublicHolidayDays:N0} days.");
            sb.AppendLine(CultureInfo.InvariantCulture, $"\tVacation: {u.VacationDays:N0} days.");
            sb.AppendLine(CultureInfo.InvariantCulture, $"\tOther Abscences: {u.OtherAbsenceDays:N0} days.");
        }

        return sb.ToString();
    }
}
