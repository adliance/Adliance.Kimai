using Adliance.Kimai.Reports.Extensions;

namespace Adliance.Kimai.Reports;

public class CalculationService(Configuration config, Data data, DateOnly from, DateOnly until)
{
    public void Calculate()
    {
        foreach (var u in config.Users)
        {
            CalculateUser(u);
        }
    }

    private void CalculateUser(Configuration.User user)
    {
        var kimaiUser = data.Users.SingleOrDefault(x => x.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase));
        if (kimaiUser == null)
        {
            return;
        }

        user.Name = kimaiUser.Title;
        user.FoundInKimai = true;

        // we need the average of billable, so we need to store them all, to get the average at the end
        var expectedBillable = new List<double>();

        foreach (var e in user.Employments)
        {
            if (e.Begin > until)
            {
                continue; // so we can add future employments to the config
            }

            if (e.End < from)
            {
                continue; // employments that ended before the "from" day are not part of the calculation
            }

            // only calculate within the "from" and "until" days (the very beginning and today by default)
            // we don't modify the employment itself, because the vacation entitlement is calculated based on the configured employments
            var begin = e.Begin < from ? from : e.Begin;
            var end = e.End > until ? until : e.End;

            if (end < begin)
            {
                throw new Exception($"End date {end} is before start date {begin} (user {user.Username}).");
            }

            var currentDay = begin;
            while (currentDay <= end)
            {
                var expectedMinutesForThisDay = e.GetExpectedMinutes(currentDay);
                if (expectedMinutesForThisDay > 0)
                {
                    user.ExpectedMinutesBrutto += expectedMinutesForThisDay;
                }

                var earnedVacationMinutesForThisDay = GetAliquotVacationMinutes(e, currentDay);
                var expectedBillableOnThisDay = e.GetExpectedBillablePercent(currentDay);
                if (expectedBillableOnThisDay.HasValue)
                {
                    expectedBillable.Add(expectedBillableOnThisDay.Value);
                }

                if (currentDay.IsPublicHoliday(data))
                {
                    user.PublicHolidayDays++;
                    user.WorkedTotalMinutesBrutto += expectedMinutesForThisDay;
                }
                else if (currentDay.IsVacationDay(user, data))
                {
                    if (expectedMinutesForThisDay > 0)
                    {
                        user.RemainingVacationMinutes -= expectedMinutesForThisDay;
                        user.VacationDays++;
                        user.WorkedTotalMinutesBrutto += expectedMinutesForThisDay;
                    }
                }
                else
                {
                    if (expectedMinutesForThisDay > 0)
                    {
                        if (currentDay.IsHomeOffice(user, data))
                        {
                            user.HomeOfficeDays++;
                        }

                        user.ExpectedMinutesNetto += expectedMinutesForThisDay - currentDay.GetOtherAbsenceMinutes(user, data);
                        user.OtherAbsenceMinutes += currentDay.GetOtherAbsenceMinutes(user, data);
                        user.TimeOffMinutes += currentDay.GetTimeOffMinutes(user, data); // only informational, time off doesn't reduce the expected minutes, so it reduces the overtime
                    }

                    user.WorkedTotalMinutesNetto += currentDay.GetWorkedTotalMinutes(user, data);
                    user.WorkedTotalMinutesBrutto += currentDay.GetWorkedTotalMinutes(user, data);
                    user.WorkedTotalMinutesBrutto += currentDay.GetOtherAbsenceMinutes(user, data);
                    user.WorkedBillableMinutes += currentDay.GetWorkedBillableMinutes(user, data);
                }

                CalculateWarnings(user, currentDay);

                user.RemainingVacationMinutes += earnedVacationMinutesForThisDay;
                currentDay = currentDay.AddDays(1);
            }
        }

        user.ExpectedBillablePercent = expectedBillable.Count > 0 ? expectedBillable.Average() : 0;
        CalculateVacationEntitlement(user);
    }

    /// <summary>
    /// Calculates the remaining vacation entitlement according to the Austrian Urlaubsgesetz.
    /// This deliberately ignores the "from" day, because the entitlement depends on the employments as configured (when the
    /// working year started, which employments are part of it), so it always covers the configured employments up to the "until" day.
    /// </summary>
    private void CalculateVacationEntitlement(Configuration.User user)
    {
        foreach (var e in user.Employments)
        {
            var end = e.End > until ? until : e.End;
            for (var day = e.Begin; day <= end; day = day.AddDays(1))
            {
                user.RemainingVacationEntitlementMinutes += GetEntitledVacationMinutes(user, day);

                if (!day.IsPublicHoliday(data) && day.IsVacationDay(user, data))
                {
                    user.RemainingVacationEntitlementMinutes -= e.GetExpectedMinutes(day);
                }
            }
        }

        user.NextVacationEntitlementDay = GetNextVacationEntitlementDay(user);
    }

    /// <summary>
    /// The next day (after the "until" day) when a new vacation entitlement arises: 6 months after the start in the first working year,
    /// otherwise at the start of the next working year. <c>null</c> if the employment relationship ends before (or isn't active on the "until" day).
    /// </summary>
    private DateOnly? GetNextVacationEntitlementDay(Configuration.User user)
    {
        if (!user.Employments.Any(x => until >= x.Begin && until <= x.End))
        {
            return null;
        }

        var (relationshipStart, relationshipEnd) = GetEmploymentRelationship(user, until);
        var fullYears = GetFullYearsBetween(relationshipStart, until);
        var sixMonthsAfterStart = relationshipStart.AddMonths(6);
        var next = fullYears == 0 && until < sixMonthsAfterStart ? sixMonthsAfterStart : relationshipStart.AddYears(fullYears + 1);
        return next <= relationshipEnd ? next : null;
    }

    /// <summary>
    /// The yearly vacation allowance of an employment, which is 5 weeks (= 25 days for a 5-day week, 20 days for a 4-day week, ...).
    /// </summary>
    private static double GetYearlyVacationMinutes(Configuration.Employment employment)
    {
        return employment.MinutesPerDay * 5.0 * employment.Weekdays.Length;
    }

    /// <summary>
    /// The vacation that is earned on a single day, if the yearly vacation allowance is distributed evenly over the year.
    /// </summary>
    private static double GetAliquotVacationMinutes(Configuration.Employment employment, DateOnly day)
    {
        return GetYearlyVacationMinutes(employment) / (DateTime.IsLeapYear(day.Year) ? 366.0 : 365.0);
    }

    /// <summary>
    /// The vacation entitlement ("Urlaubsanspruch") that arises on a single day according to the Austrian Urlaubsgesetz (§ 2 UrlG):
    /// During the first 6 months of the first working year, the entitlement grows aliquot.
    /// After 6 months, the entitlement for the whole first working year arises.
    /// With the beginning of each following working year, the entitlement for the whole working year arises.
    /// The working year starts with the day the employment relationship started (not the calendar year), which can be configured
    /// for the first employment relationship, if it started before the first configured employment.
    /// The vacation offset is expected to contain the aliquot vacation balance up to the first configured employment, so if the entitlement
    /// for the running working year arose before, the rest of it (that is not yet part of the aliquot offset) arises on the first configured day.
    /// The entitlement for a working year is calculated pro rata from all employments within this working year, so
    /// changes of the working days (e.g. from 5 to 4 days a week) as well as the end of the employment relationship
    /// within the working year (which caps the entitlement to the aliquot part, § 10 UrlG) are considered.
    /// </summary>
    private static double GetEntitledVacationMinutes(Configuration.User user, DateOnly day)
    {
        var (relationshipStart, relationshipEnd) = GetEmploymentRelationship(user, day);
        var fullYears = GetFullYearsBetween(relationshipStart, day);
        var workingYearStart = relationshipStart.AddYears(fullYears);
        var nextWorkingYearStart = relationshipStart.AddYears(fullYears + 1);
        var daysInWorkingYear = nextWorkingYearStart.DayNumber - workingYearStart.DayNumber;
        var workingYearEnd = nextWorkingYearStart.AddDays(-1) < relationshipEnd ? nextWorkingYearStart.AddDays(-1) : relationshipEnd;

        var sixMonthsAfterStart = relationshipStart.AddMonths(6);

        // if the employment started before the first configured employment, the vacation offset contains the aliquot vacation up to the first configured day,
        // but the entitlement for the running working year already arose before, so we need to add the part of the running working year that is not yet part of the offset
        var firstConfiguredDay = user.Employments.Min(x => x.Begin);
        if (day == firstConfiguredDay && relationshipStart < firstConfiguredDay)
        {
            var entitlementDay = fullYears == 0 ? sixMonthsAfterStart : workingYearStart;
            if (entitlementDay < day)
            {
                return GetAliquotVacationMinutes(user, day, workingYearEnd, daysInWorkingYear);
            }
        }

        if (fullYears == 0)
        {
            if (day < sixMonthsAfterStart)
            {
                return GetAliquotVacationMinutes(user, day, day, daysInWorkingYear);
            }

            if (day == sixMonthsAfterStart)
            {
                // the entitlement for the whole working year, minus what was already granted aliquot in the first 6 months
                return GetAliquotVacationMinutes(user, workingYearStart, workingYearEnd, daysInWorkingYear)
                       - GetAliquotVacationMinutes(user, workingYearStart, sixMonthsAfterStart.AddDays(-1), daysInWorkingYear);
            }

            return 0;
        }

        if (day == workingYearStart)
        {
            return GetAliquotVacationMinutes(user, workingYearStart, workingYearEnd, daysInWorkingYear);
        }

        return 0;
    }

    /// <summary>
    /// The aliquot part of the yearly vacation allowance for all days between (and including) "from" and "to", based on the employment on each day.
    /// </summary>
    private static double GetAliquotVacationMinutes(Configuration.User user, DateOnly from, DateOnly to, int daysInWorkingYear)
    {
        var result = 0.0;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var employment = GetEmploymentForVacationEntitlement(user, day);
            if (employment != null)
            {
                result += GetYearlyVacationMinutes(employment) / daysInWorkingYear;
            }
        }

        return result;
    }

    /// <summary>
    /// The employment on this day. For days between the real employment start (if configured) and the first configured employment,
    /// we assume the conditions of the first configured employment, because we don't know the employments before.
    /// </summary>
    private static Configuration.Employment? GetEmploymentForVacationEntitlement(Configuration.User user, DateOnly day)
    {
        var employment = user.Employments.FirstOrDefault(x => day >= x.Begin && day <= x.End);
        if (employment == null && user.EmploymentStartForVacationEntitlement <= day)
        {
            var firstEmployment = user.Employments.MinBy(x => x.Begin);
            if (firstEmployment != null && day < firstEmployment.Begin)
            {
                return firstEmployment;
            }
        }

        return employment;
    }

    /// <summary>
    /// Employments that directly follow each other (e.g. a change from 5 to 4 days a week) are one single employment relationship,
    /// so the working year starts with the first of these employments, and the relationship ends with the last of these employments.
    /// </summary>
    private static (DateOnly Start, DateOnly End) GetEmploymentRelationship(Configuration.User user, DateOnly day)
    {
        var employment = user.Employments.First(x => day >= x.Begin && day <= x.End);

        var start = employment.Begin;
        while (user.Employments.FirstOrDefault(x => x.Begin < start && x.End >= start.AddDays(-1)) is { } previous)
        {
            start = previous.Begin;
        }

        // the first employment relationship might have started before the first configured employment (e.g. before we started using Kimai)
        if (user.EmploymentStartForVacationEntitlement.HasValue && start == user.Employments.Min(x => x.Begin))
        {
            if (user.EmploymentStartForVacationEntitlement.Value > start)
            {
                throw new Exception($"The employment start for vacation entitlement {user.EmploymentStartForVacationEntitlement.Value} is after the first employment {start} (user {user.Username}).");
            }

            start = user.EmploymentStartForVacationEntitlement.Value;
        }

        var end = employment.End;
        while (end < DateOnly.MaxValue && user.Employments.FirstOrDefault(x => x.End > end && x.Begin <= end.AddDays(1)) is { } next)
        {
            end = next.End;
        }

        return (start, end);
    }

    private static int GetFullYearsBetween(DateOnly start, DateOnly day)
    {
        var years = day.Year - start.Year;
        if (start.AddYears(years) > day)
        {
            years--;
        }

        return years;
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
        if (timesheets.Any(x => x.Description?.Contains("Dienstreise", StringComparison.OrdinalIgnoreCase) == true))
        {
            return;
        }

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
