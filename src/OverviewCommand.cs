using System.CommandLine;
using System.CommandLine.Invocation;
using Adliance.Kimai.Extensions;

namespace Adliance.Kimai;

public class OverviewCommand : Command
{
    public static readonly Option<string> UrlOption = new("--url", "-u")
    {
        Description = "The URL to your Kimai instance. For example: https://demo.kimai.org/.",
        Required = true
    };

    public static readonly Option<string> TokenOption = new("--token", "-t")
    {
        Description = "Your Kimai API token.",
        Required = true
    };

    public OverviewCommand() : base("overview", "Creates an overview report.")
    {
        Options.Add(UrlOption);
        Options.Add(TokenOption);
        Action = new OverviewAction();
    }
}

public class OverviewAction : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = new())
    {
        var url = parseResult.GetRequiredValue(OverviewCommand.UrlOption);
        var token = parseResult.GetRequiredValue(OverviewCommand.TokenOption);

        try
        {
            var client = new KimaiClient.KimaiClient(url, token);

            var data = await Data.LoadFromCacheOrKimai(client);
            Console.WriteLine(data);

            var configuration = await Configuration.Load();
            new CalculationService(configuration, data).Calculate();
            await WriteHtmlFile(configuration);

            Console.WriteLine("Done. Goodbye.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return -1;
        }
    }

    private static async Task WriteHtmlFile(Configuration configuration)
    {
        var file = new FileInfo("overview.html");

        var html = new HtmlWriter("Overview", $"Generated on {DateTime.Now:yyyy-MM-dd HH:mm}");

        html.W("""
               <table class="striped">
               <thead>
                 <tr>
                   <th>User</th>
                   <th style="text-align:right;">Expected</th>
                   <th style="text-align:right;">Worked</th>
                   <th style="text-align:right;">Overtime</th>
                   <th style="text-align:right;">Home Office</th>
                   <th style="text-align:right;">Public Holidays</th>
                   <th style="text-align:right;">Absence</th>
                   <th style="text-align:right;">Vacation (used)</th>
                   <th style="text-align:right;">Vacation (remaining)</td>
                 </tr>
               </thead>
               <tbody>
               """);

        foreach (var u in configuration.Users.OrderBy(x => x.Name))
        {
            var day = u.GetLastEmploymentDay();
            var overtime = u.WorkedMinutes - u.ExpectedMinutes;
            var vacationDays = day.MinutesToDays(u.RemainingVacationMinutes, u);
            var vacationOffsetDays = day.MinutesToDays(u.OffsetVacationsMinutes, u);

            html.W($"""
                    <tr>
                      <td>{u.Name}</td>
                      <td style="text-align:right;">{u.ExpectedMinutes / 60d:N2}h</td>
                      <td style="text-align:right;">{u.WorkedMinutes / 60d:N2}h</td>
                      <td style="text-align:right;" title="{overtime / 60d:N2}h + {u.OffsetWorktimeMinutes / 60d:N2}h = {(overtime + u.OffsetWorktimeMinutes) / 60d:N2}h">{(overtime + u.OffsetWorktimeMinutes) / 60d:N2}h</td>
                      <td style="text-align:right;">{u.HomeOfficeDays:N0} days</td>
                      <td style="text-align:right;">{u.PublicHolidayDays:N0} days</td>
                      <td style="text-align:right;">{u.OtherAbsenceDays:N0} days</td>
                      <td style="text-align:right;">{u.VacationDays:N0} days</td>
                      <td style="text-align:right;" title="{vacationDays:N2} days + {vacationOffsetDays:N2} days = {vacationDays + vacationOffsetDays:N2} days">{vacationDays + vacationOffsetDays:N2} days</td>
                    </tr>
                    """);
        }

        html.W("""
               </tbody>
               </table>
               """);

        await File.WriteAllTextAsync(file.FullName, html.ToString());
        Console.WriteLine($"File \"{file.FullName}\" created.");
    }
}
