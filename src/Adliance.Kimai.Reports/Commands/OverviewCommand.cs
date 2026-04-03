using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Text.Json;
using Adliance.Kimai.Client;
using Adliance.Kimai.Reports.Extensions;

namespace Adliance.Kimai.Reports.Commands;

public class OverviewCommand : CommandBase
{
    public OverviewCommand() : base("overview", "Creates an overview report for all users that the API key has access to.")
    {
        Action = new OverviewAction();
    }
}

public class OverviewAction : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = new())
    {
        var url = parseResult.GetRequiredValue(CommandBase.UrlOption);
        var token = parseResult.GetRequiredValue(CommandBase.TokenOption);
        var configPath = parseResult.GetValue(CommandBase.ConfigPath);
        if (string.IsNullOrWhiteSpace(configPath)) configPath = "./config.json";
        var outputPath = parseResult.GetValue(CommandBase.OutputPath);
        if (string.IsNullOrWhiteSpace(outputPath)) outputPath = "./";

        try
        {
            var client = new KimaiClient(url, token);

            var data = await Data.LoadFromCacheOrKimai(client);
            Console.WriteLine(data);

            var configuration = await Configuration.Load(configPath);
            new CalculationService(configuration, data).Calculate();
            await WriteHtmlFile(outputPath, configuration);

            Console.WriteLine("Done. Goodbye.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return -1;
        }
    }

    private static async Task WriteHtmlFile(string basePath, Configuration configuration)
    {
        var file = new FileInfo(Path.Combine(basePath, "overview.html"));

        var html = new HtmlWriter("Overview", $"Generated on {DateTime.Now:yyyy-MM-dd HH:mm}");

        html.W("""
               <table class="striped">
               <thead>
                 <tr>
                   <th>User</th>
                   <th style="text-align:right;">Expected</th>
                   <th style="text-align:right;">Worked</th>
                   <th style="text-align:right;">Billable</th>
                   <th style="text-align:right;">Overtime</th>
                   <th style="text-align:right;">Home Office</th>
                   <th style="text-align:right;">Public Holidays</th>
                   <th style="text-align:right;">Absence</th>
                   <th style="text-align:right;">Vacation (used)</th>
                   <th style="text-align:right;">Vacation (remaining)</td>
                   <th style="text-align:center;" title="Warnings"></td>
                 </tr>
               </thead>
               <tbody>
               """);

        var users = configuration.Users.Where(x => x.FoundInKimai).OrderBy(x => x.Name).ToList();

        foreach (var u in users)
        {
            var day = u.GetLastEmploymentDay();
            var overtime = u.WorkedTotalMinutes - u.ExpectedMinutes;
            var vacationDays = day.MinutesToDays(u.RemainingVacationMinutes, u);
            var vacationOffsetDays = day.MinutesToDays(u.OffsetVacationsMinutes, u);

            html.W($"""
                    <tr>
                      <td>{u.Name}</td>
                      <td style="text-align:right;">{u.ExpectedMinutes / 60d:N2}h</td>
                      <td style="text-align:right;">{u.WorkedTotalMinutes / 60d:N2}h</td>
                      <td style="text-align:right;" title="{u.BillablePercent:N2}% / {u.ExpectedBillablePercent:N2}%">
                        {html.Tag("mark", u.BillablePercent < u.ExpectedBillablePercent, u.BillablePercent.ToString("N0", CultureInfo.InvariantCulture) + "/" + u.ExpectedBillablePercent.ToString("N0", CultureInfo.InvariantCulture) + "%")}
                      </td>
                      <td style="text-align:right;" title="{overtime / 60d:N2}h + {u.OffsetWorktimeMinutes / 60d:N2}h = {(overtime + u.OffsetWorktimeMinutes) / 60d:N2}h">
                        {(overtime + u.OffsetWorktimeMinutes) / 60d:N2}h
                      </td>
                      <td style="text-align:right;">{u.HomeOfficeDays:N0} days</td>
                      <td style="text-align:right;">{u.PublicHolidayDays:N0} days</td>
                      <td style="text-align:right;">{u.OtherAbsenceDays:N0} days</td>
                      <td style="text-align:right;">{u.VacationDays:N0} days</td>
                      <td style="text-align:right;" title="{vacationDays:N2} days + {vacationOffsetDays:N2} days = {vacationDays + vacationOffsetDays:N2} days">{vacationDays + vacationOffsetDays:N2} days</td>
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
