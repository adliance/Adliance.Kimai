using Adliance.Kimai.Reports.Extensions;

namespace Adliance.Kimai.Reports;

public class CalculationService(Configuration config, Data data, DateOnly until)
{
    public void Calculate()
    {
        foreach (var u in config.Users) CalculateUser(u);
    }

    private void CalculateUser(Configuration.User user)
    {
        var kimaiUser = data.Users.SingleOrDefault(x => x.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase));
        if (kimaiUser == null) return;
        user.Name = kimaiUser.Title;
        user.FoundInKimai = true;

        // we need the average of billable, so we need to store them all, to get the average at the end
        var expectedBillable = new List<double>();

        foreach (var e in user.Employments)
        {
            if (e.Begin > until) continue; // so we can add future employments to the config
            if (e.End > until) e.End = until; // only calculate up to the "until" day (today by default), default value is DateOnly.Max

            if (e.End < e.Begin) throw new Exception($"End date {e.End} is before start date {e.Begin} (user {user.Username}).");

            var currentDay = e.Begin;
            while (currentDay <= e.End)
            {
                var expectedMinutes = e.GetExpectedMinutes(currentDay);
                var earnedVacationMinutesForThisDay = e.MinutesPerDay * (25.0 / 5.0 * e.Weekdays.Length) / (DateTime.IsLeapYear(currentDay.Year) ? 366.0 : 365.0);
                var expectedBillableOnThisDay = e.GetExpectedBillablePercent(currentDay);
                if (expectedBillableOnThisDay.HasValue) expectedBillable.Add(expectedBillableOnThisDay.Value);

                if (currentDay.IsPublicHoliday(data))
                {
                    user.PublicHolidayDays++;
                }
                else if (currentDay.IsVacationDay(user, data))
                {
                    if (expectedMinutes > 0)
                    {
                        user.RemainingVacationMinutes -= expectedMinutes;
                        user.VacationDays++;
                    }
                }
                else if (currentDay.IsOtherAbsence(user, data))
                {
                    user.OtherAbsenceDays++;
                }
                else
                {
                    if (expectedMinutes > 0)
                    {
                        if (currentDay.IsHomeOffice(user, data)) user.HomeOfficeDays++;
                        user.ExpectedMinutes += expectedMinutes;
                    }

                    user.WorkedTotalMinutes += currentDay.GetWorkedTotalMinutes(user, data);
                    user.WorkedBillableMinutes += currentDay.GetWorkedBillableMinutes(user, data);
                }

                CalculateWarnings(user, currentDay);

                user.RemainingVacationMinutes += earnedVacationMinutesForThisDay;
                currentDay = currentDay.AddDays(1);
            }
        }

        user.ExpectedBillablePercent = expectedBillable.Count > 0 ? expectedBillable.Average() : 0;
    }

    private void CalculateWarnings(Configuration.User user, DateOnly day)
    {
        var timesheets = data.Timesheets
            .Where(x => x.User?.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase) == true)
            .Where(x => x.End.HasValue)
            .Where(x => day == DateOnly.FromDateTime(x.Begin))
            .OrderBy(x => x.End!.Value)
            .ToList();

        // simple hack, if we have a Dienstreise text on this day, we ignore certain warnings regarding max work times
        if (timesheets.Any(x => x.Description?.Contains("Dienstreise", StringComparison.OrdinalIgnoreCase) == true)) return;

        const double maxMinutesPerDay = 12 * 60;
        const double maxMinutesWithoutBreak = 6 * 60;
        const double minutesBreak = 30;

        var totalMinutes = timesheets.Sum(x => x.DurationMinutes);
        if (totalMinutes > maxMinutesPerDay)
        {
            user.Warnings.Add(new Configuration.User.Warning(day, $"Logged more than {maxMinutesWithoutBreak / 60:N2}h ({totalMinutes / 60:N2}h) on this day."));
        }

        var minutesWithoutBreak = 0.0;
        var lastEnd = DateTime.MinValue;
        foreach (var t in timesheets)
        {
            if (t.Begin < lastEnd.AddMinutes(minutesBreak))
            {
                minutesWithoutBreak += t.DurationMinutes;
            }
            else
            {
                minutesWithoutBreak = 0;
            }

            if (minutesWithoutBreak > maxMinutesWithoutBreak)
            {
                user.Warnings.Add(new Configuration.User.Warning(day, $"Logged more than {maxMinutesWithoutBreak / 60:N2}h ({minutesWithoutBreak / 60:N2}h) without a break."));
                break;
            }

            lastEnd = t.End!.Value;
        }
    }
}
