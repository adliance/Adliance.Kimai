using System.CommandLine;
using System.Globalization;
using Adliance.Kimai.Reports.Extensions;

namespace Adliance.Kimai.Reports.Commands;

public class OverviewCommand : CommandBase
{
    public static readonly Option<DateOnly> FromOption = new("--from")
    {
        Description = "A date to calculate the report from (including this day). Defaults to the beginning of the first employment.",
        Required = false,
        DefaultValueFactory = _ => DateOnly.MinValue
    };

    public static readonly Option<DateOnly> UntilOption = new("--until")
    {
        Description = "A date to calculate the report up to (including this day), to get the state at this day. Defaults to today.",
        Required = false,
        DefaultValueFactory = _ => DateOnly.FromDateTime(DateTime.Today)
    };

    public OverviewCommand() : base("overview", "Creates an overview report for all users that the API key has access to.")
    {
        Options.Add(FromOption);
        Options.Add(UntilOption);
        Action = new OverviewAction();
    }
}

public class OverviewAction : ActionBase
{
    public override async Task PrepareResult(string basePath, Data data, Configuration configuration)
    {
        var from = ParseResult.GetValue(OverviewCommand.FromOption);
        var until = ParseResult.GetValue(OverviewCommand.UntilOption);

        new CalculationService(configuration, data, from, until).Calculate();
        var file = new FileInfo(Path.Combine(basePath, "overview.html"));

        var subTitle = $"Generated on {DateTime.Now:yyyy-MM-dd HH:mm}, calculated up to and including {until:yyyy-MM-dd}";
        if (from > DateOnly.MinValue)
        {
            subTitle += $", starting with {from:yyyy-MM-dd}";
        }

        var html = new HtmlWriter("Overview", subTitle + ".");

        html.W("""
               <table class="striped">
               <thead>
                 <tr>
                   <th rowspan="2">User</th>
                   <th style="text-align:center; border:0; padding-bottom:0;" colspan="2">Expected</th>
                   <th style="text-align:center; border:0; padding-bottom:0;" colspan="2">Worked</th>
                   <th style="text-align:center;" rowspan="2">Productivity</th>
                   <th style="text-align:center;" rowspan="2">Billable</th>
                   <th style="text-align:center;" rowspan="2">Overtime</th>
                   <th style="text-align:center;" title="Paid absences (e.g. sickness), that reduce the expected hours." rowspan="2">Absence</th>
                   <th style="text-align:center;" rowspan="2">HomeOffice</th>
                   <th style="text-align:center;" title="Public Holidays" rowspan="2">Holidays</th>
                   <th style="text-align:center; border:0; padding-bottom:0;" colspan="3">Vacation</th>
                   <th style="text-align:center;" title="Warnings" rowspan="2"></td>
                 </tr>
                 <tr>
                   <th style="text-align:center;">Net</th>
                   <th style="text-align:center;">Total</th>
                   <th style="text-align:center;">Net</th>
                   <th style="text-align:center;">Total</th>
                   <th style="text-align:center;">Used</th>
                   <th style="text-align:center;" title="Remaining vacation, if vacation is earned aliquot day by day.">Aliquot</th>
                   <th style="text-align:center;" title="Remaining vacation according to the Austrian vacation law: aliquot during the first 6 months, then the allowance for the first working year, and the allowance at the start of each following working year. The allowance of a working year is calculated pro rata from the configured employments, so changed working days and the end of the employment are considered. Always based on all configured employments, ignoring the --from option.">Entitled</th>
                 </tr>
               </thead>
               <tbody>
               """);

        var users = configuration.Users
            .Where(x => x.FoundInKimai)
            .Where(x => x.Employments.Any(e => e.Begin <= until && e.End >= from)) // users that aren't employed in the calculated range can't be calculated
            .OrderBy(x => x.Name)
            .ToList();

        foreach (var u in users)
        {
            var day = u.GetLastEmploymentDay(until);
            var overtime = u.WorkedTotalMinutesNetto - u.ExpectedMinutesNetto;
            var vacationDays = day.MinutesToDays(u.RemainingVacationMinutes, u);
            var vacationOffsetDays = day.MinutesToDays(u.OffsetVacationsMinutes, u);
            var vacationEntitlementDays = day.MinutesToDays(u.RemainingVacationEntitlementMinutes, u);

            html.W($"""
                    <tr>
                      <td style="white-space:nowrap;">{u.Name}</td>
                      <td style="text-align:right;">{u.ExpectedMinutesNetto / 60d:N2}h</td>
                      <td style="text-align:right;">{u.ExpectedMinutesBrutto / 60d:N2}h</td>
                      <td style="text-align:right;">{u.WorkedTotalMinutesNetto / 60d:N2}h</td>
                      <td style="text-align:right;">{u.WorkedTotalMinutesBrutto / 60d:N2}h</td>
                      <td style="text-align:right;" title="= Worked Hours Net relative to Expected Hours Total, so basically the percentage of actual work hours related to hours paid.">
                        {u.ProductivityPercent.ToString("N0", CultureInfo.InvariantCulture)}%
                      </td>
                      <td style="text-align:right;" title="{u.BillablePercent:N2}% / {u.ExpectedBillablePercent:N2}%">
                        {html.Tag("mark", u.BillablePercent < u.ExpectedBillablePercent, u.BillablePercent.ToString("N0", CultureInfo.InvariantCulture) + "/" + u.ExpectedBillablePercent.ToString("N0", CultureInfo.InvariantCulture) + "%")}
                      </td>
                      <td style="text-align:right;" title="{overtime / 60d:N2}h + {u.OffsetWorktimeMinutes / 60d:N2}h = {(overtime + u.OffsetWorktimeMinutes) / 60d:N2}h (includes {u.TimeOffMinutes / 60d:N2}h time off)">
                        {(overtime + u.OffsetWorktimeMinutes) / 60d:N2}h
                      </td>
                    <td style="text-align:right;" title="{u.OtherAbsenceMinutes:N0} minutes">{u.OtherAbsenceMinutes / 60d:N2}h</td>
                      <td style="text-align:right;">{u.HomeOfficeDays:N0}d</td>
                      <td style="text-align:right;">{u.PublicHolidayDays:N0}d</td>
                      <td style="text-align:right;">{u.VacationDays:N0}d</td>
                      <td style="text-align:right;" title="{vacationDays:N2} days + {vacationOffsetDays:N2} days = {vacationDays + vacationOffsetDays:N2} days">{vacationDays + vacationOffsetDays:N2}d</td>
                      <td style="text-align:right;" title="{vacationEntitlementDays:N2} days + {vacationOffsetDays:N2} days = {vacationEntitlementDays + vacationOffsetDays:N2} days{(u.NextVacationEntitlementDay.HasValue ? $" (next entitlement on {u.NextVacationEntitlementDay:yyyy-MM-dd})" : "")}">{vacationEntitlementDays + vacationOffsetDays:N2}d</td>
                      <td style="text-align:center;">{(u.Warnings.Count > 0 ? $"<a href=\"#warnings_{u.Username}\"><mark>{u.Warnings.Count}</mark></a>" : "")}</td>
                    </tr>
                    """);
        }

        html.W("""
               </tbody>
               </table>
               """);

        if (users.Any(x => x.Warnings.Count > 0))
        {
            foreach (var u in users.Where(x => x.Warnings.Count > 0))
            {
                html.W($"""
                        <section id="warnings_{u.Username}">
                        <h4>Warnings for {u.Name}</h4>
                        <ul>
                        """);
                foreach (var w in u.Warnings.OrderBy(x => x.Date))
                {
                    html.W($"<li><code>{w.Date:yyyy-MM-dd}</code> {w.Text}</li>");
                }

                html.W("</ul></section>");
            }
        }

        WriteVacationTable(html, users, until);

        await File.WriteAllTextAsync(file.FullName, html.ToString());
        Console.WriteLine($"File \"{file.FullName}\" created.");
    }

    /// <summary>
    /// A table that shows per user and calendar year, when vacation entitlement was added and how much vacation was used,
    /// to make it easy to understand how the remaining vacation (entitlement) is calculated.
    /// </summary>
    private static void WriteVacationTable(HtmlWriter html, List<Configuration.User> users, DateOnly until)
    {
        if (users.Count == 0)
        {
            return;
        }

        const string newLine = "&#10;";
        var firstYear = users.SelectMany(x => x.Employments).Min(x => x.Begin).Year;
        var years = Enumerable.Range(firstYear, Math.Max(0, until.Year - firstYear + 1)).ToList();

        html.W($"""
                <h3 id="vacation">Vacation</h3>
                <p>
                  Vacation according to the Austrian vacation law, per calendar year:
                  <code>+</code> is the entitlement that was added in this year, <code>-</code> is the vacation that was used in this year.
                  During the first 6 months of an employment, the entitlement grows day by day. After 6 months, the entitlement for the whole first working year is added,
                  and then the entitlement for the whole working year at the start of each working year (the anniversary of the employment).
                  All values are in days, based on the current hours per day of the user. Hover over a value for details.
                </p>
                <table class="striped">
                <thead>
                  <tr>
                    <th>User</th>
                    <th style="text-align:center;" title="The current hours per day (which are used to convert the vacation to days) and the current days per week.">Hours/Day (Days/Week)</th>
                    <th style="text-align:center;" title="Remaining vacation from before {firstYear}.">Before {firstYear}</th>
                    {string.Join(Environment.NewLine, years.Select(y => $"<th style=\"text-align:center;\">{y}</th>"))}
                    <th style="text-align:center;" title="The remaining vacation, which is the sum of all previous columns.">Remaining</th>
                    <th style="text-align:center;" title="For comparison: remaining vacation, if vacation is earned aliquot day by day (same as in the table above).">Aliquot</th>
                    <th style="text-align:center;" title="The day when the next entitlement will be added.">Next Entitlement</th>
                  </tr>
                </thead>
                <tbody>
                """);

        foreach (var u in users)
        {
            var day = u.GetLastEmploymentDay(until);
            var employment = u.Employments.Single(x => day >= x.Begin && day <= x.End);
            var offsetDays = day.MinutesToDays(u.OffsetVacationsMinutes, u);
            var remainingDays = day.MinutesToDays(u.OffsetVacationsMinutes + u.RemainingVacationEntitlementMinutes, u);
            var aliquotDays = day.MinutesToDays(u.RemainingVacationMinutes, u);

            html.W($"""
                    <tr>
                      <td style="white-space:nowrap;">{u.Name}</td>
                      <td style="text-align:right; white-space:nowrap;" title="{employment.MinutesPerDay / 60d:N2} hours per day, {employment.Weekdays.Length} days per week">{employment.MinutesPerDay / 60d:N2}h ({employment.Weekdays.Length}d)</td>
                      <td style="text-align:right;">{offsetDays:N2}d</td>
                    """);

            foreach (var year in years)
            {
                var entries = u.VacationEntitlementEntries.Where(x => x.Date.Year == year).ToList();
                var aliquot = entries.Where(x => x.Type == Configuration.User.VacationEntryType.Aliquot).ToList();
                var entitlements = entries.Where(x => x.Type == Configuration.User.VacationEntryType.Entitlement).ToList();
                var used = entries.Where(x => x.Type == Configuration.User.VacationEntryType.Used).ToList();
                var usedDays = day.MinutesToDays(used.Sum(x => x.Minutes), u);

                // show each addition separately (e.g. the rest of the running working year, and the new working year), so it's clear where the entitlement comes from
                var additions = new List<(DateOnly Date, double Minutes, string Text)>();

                // the entitlement that grows day by day during the first 6 months, and the rest of the first working year that is added after 6 months,
                // are shown as a single value, because both together are the entitlement of the (rest of the) first working year
                var sixMonthsEntitlement = aliquot.Count > 0 ? entitlements.FirstOrDefault(x => x.Date == aliquot.Max(a => a.Date).AddDays(1)) : null;
                if (aliquot.Count > 0)
                {
                    var minutes = aliquot.Sum(x => x.Minutes) + (sixMonthsEntitlement?.Minutes ?? 0);
                    var text = sixMonthsEntitlement != null
                        ? $"added from {aliquot.Min(x => x.Date):yyyy-MM-dd} until the full entitlement of the first working year on {sixMonthsEntitlement.Date:yyyy-MM-dd}"
                        : $"added day by day from {aliquot.Min(x => x.Date):yyyy-MM-dd} to {aliquot.Max(x => x.Date):yyyy-MM-dd} (first 6 months, until the full entitlement of the first working year)";
                    additions.Add((aliquot.Min(x => x.Date), minutes, text));
                }

                var firstConfiguredDay = u.Employments.Min(x => x.Begin);
                foreach (var x in entitlements.Where(x => x != sixMonthsEntitlement))
                {
                    var isRestOfWorkingYear = x.Date == firstConfiguredDay && u.EmploymentStartForVacationEntitlement < firstConfiguredDay;
                    additions.Add((x.Date, x.Minutes, $"added on {x.Date:yyyy-MM-dd}"
                                                      + (isRestOfWorkingYear ? $" (rest of the working year that was running on this day, the part before is included in &quot;Before {firstYear}&quot;)" : "")));
                }

                var parts = new List<string>();
                var details = new List<string>();
                foreach (var a in additions.OrderBy(x => x.Date))
                {
                    parts.Add(FormatDays(day.MinutesToDays(a.Minutes, u)));
                    details.Add($"{FormatDays(day.MinutesToDays(a.Minutes, u))} {a.Text}");
                }

                if (used.Count > 0)
                {
                    var months = used.GroupBy(x => x.Date.Month).OrderBy(x => x.Key).Select(x => $"{new DateOnly(year, x.Key, 1).ToString("MMM", CultureInfo.InvariantCulture)}: {x.Count()}");
                    parts.Add(FormatDays(usedDays));
                    details.Add($"{FormatDays(usedDays)} used on {used.Count} days ({string.Join(", ", months)})");
                }

                var content = string.Join(" ", parts);
                html.W($"""<td style="text-align:right; white-space:nowrap;" title="{string.Join(newLine, details)}">{content}</td>""");
            }

            html.W($"""
                      <td style="text-align:right;"><strong>{remainingDays:N2}d</strong></td>
                      <td style="text-align:right;" title="{aliquotDays:N2} days + {offsetDays:N2} days = {aliquotDays + offsetDays:N2} days">{aliquotDays + offsetDays:N2}d</td>
                      <td style="text-align:center;">{(u.NextVacationEntitlementDay.HasValue ? $"{u.NextVacationEntitlementDay:yyyy-MM-dd}" : "-")}</td>
                    </tr>
                    """);
        }

        html.W("""
               </tbody>
               </table>
               """);
    }

    private static string FormatDays(double days)
    {
        return days.ToString("+#,##0.00;-#,##0.00;+0.00", CultureInfo.CurrentCulture) + "d";
    }
}
