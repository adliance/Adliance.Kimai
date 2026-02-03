using System.CommandLine;
using System.CommandLine.Invocation;
using Adliance.Kimai.Extensions;

namespace Adliance.Kimai.Commands;

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
            var client = new KimaiClient.KimaiClient(url, token);

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

        foreach (var u in configuration.Users.Where(x => x.FoundInKimai).OrderBy(x => x.Name))
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
