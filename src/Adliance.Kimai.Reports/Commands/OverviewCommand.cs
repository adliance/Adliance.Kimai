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
        if (from > DateOnly.MinValue) subTitle += $", starting with {from:yyyy-MM-dd}";
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
                   <th style="text-align:center;" rowspan="2">Absence</th>
                   <th style="text-align:center;" rowspan="2">HomeOffice</th>
                   <th style="text-align:center;" title="Public Holidays" rowspan="2">Holidays</th>
                   <th style="text-align:center; border:0; padding-bottom:0;" colspan="2">Vacation</th>
                   <th style="text-align:center;" title="Warnings" rowspan="2"></td>
                 </tr>
                 <tr>
                   <th style="text-align:center;">Net</th>
                   <th style="text-align:center;">Total</th>
                   <th style="text-align:center;">Net</th>
                   <th style="text-align:center;">Total</th>
                   <th style="text-align:center;">Used</th>
                   <th style="text-align:center;">Remaining</td>
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
                      <td style="text-align:right;" title="{overtime / 60d:N2}h + {u.OffsetWorktimeMinutes / 60d:N2}h = {(overtime + u.OffsetWorktimeMinutes) / 60d:N2}h">
                        {(overtime + u.OffsetWorktimeMinutes) / 60d:N2}h
                      </td>
                    <td style="text-align:right;" title="{u.OtherAbsenceMinutes:N0} minutes">{u.OtherAbsenceMinutes / 60d:N2}h</td>
                      <td style="text-align:right;">{u.HomeOfficeDays:N0}d</td>
                      <td style="text-align:right;">{u.PublicHolidayDays:N0}d</td>
                      <td style="text-align:right;">{u.VacationDays:N0}d</td>
                      <td style="text-align:right;" title="{vacationDays:N2} days + {vacationOffsetDays:N2} days = {vacationDays + vacationOffsetDays:N2} days">{vacationDays + vacationOffsetDays:N2}d</td>
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

        await File.WriteAllTextAsync(file.FullName, html.ToString());
        Console.WriteLine($"File \"{file.FullName}\" created.");
    }
}
