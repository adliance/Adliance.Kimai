using System.CommandLine;
using System.CommandLine.Invocation;
using Adliance.Kimai.KimaiClient.Models;

namespace Adliance.Kimai.Commands;

public class ProjectReportCommand : CommandBase
{
    public static readonly Option<string> ProjectOption = new("--project", "-p")
    {
        Description = "The name of the project.",
        Required = true
    };

    public static readonly Option<DateTime> FromOption = new("--from")
    {
        Description = "A date to start the report from.",
        Required = false
    };

    public static readonly Option<DateTime> ToOption = new("--to")
    {
        Description = "A date to start the report from.",
        Required = false
    };

    public static readonly Option<double> TotalPoolSize = new("--pool")
    {
        Description = "The total number of hours in the pool for this project.",
        Required = false
    };

    public static readonly Option<double> OffsetPoolSize = new("--pool-offset")
    {
        Description = "A number of hours that need to be added or removed from the pool additionally. Useful for data migrations.",
        Required = false
    };

    public ProjectReportCommand() : base("project-report", "Creates a report for a specific project.")
    {
        Options.Add(ProjectOption);
        Options.Add(FromOption);
        Options.Add(ToOption);
        Options.Add(TotalPoolSize);
        Options.Add(OffsetPoolSize);
        Action = new ProjectReportAction();
    }
}

public class ProjectReportAction : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = new())
    {
        var url = parseResult.GetRequiredValue(CommandBase.UrlOption);
        var token = parseResult.GetRequiredValue(CommandBase.TokenOption);
        var outputPath = parseResult.GetValue(CommandBase.OutputPath);
        if (string.IsNullOrWhiteSpace(outputPath)) outputPath = "./";
        var projectName = parseResult.GetRequiredValue(ProjectReportCommand.ProjectOption);
        var from = parseResult.GetValue(ProjectReportCommand.FromOption);
        var to = parseResult.GetValue(ProjectReportCommand.ToOption);
        var pool = parseResult.GetValue(ProjectReportCommand.TotalPoolSize);
        var poolOffset = parseResult.GetValue(ProjectReportCommand.OffsetPoolSize);

        try
        {
            var client = new KimaiClient.KimaiClient(url, token);

            var data = await Data.LoadFromCacheOrKimai(client);
            Console.WriteLine(data);

            var project = data.Projects.FirstOrDefault(x => x.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));
            if (project == null) throw new Exception($"Project {projectName} not found.");

            // if not specified, use the last month as from/to
            if (from == default) from = new DateTime(DateTime.Now.AddMonths(-1).Year, DateTime.Now.AddMonths(-1).Month, 1, 0, 0, 0);
            if (to == default) to = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddSeconds(-1);

            var matchingEntries = data.Timesheets
                .Where(x => x.Project == project.Id && x.Begin >= from && x.End <= to)
                .OrderBy(x => x.Begin)
                .ToList();

            await WriteHtmlFile(outputPath, project.Name, from, to, pool, poolOffset, matchingEntries);

            Console.WriteLine("Done. Goodbye.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return -1;
        }
    }

    private static async Task WriteHtmlFile(string basePath, string projectName, DateTime from, DateTime to, double pool, double poolOffset, IList<Timesheet> entries)
    {
        var file = new FileInfo(Path.Combine(basePath, $"Project Report {projectName}.html"));

        string subTitle;
        if (from == default && to == DateTime.MaxValue) subTitle = "All project time entries.";
        else if (from == default) subTitle = $"Project time entries until {to:yyyy-MM-dd}.";
        else if (to == DateTime.MaxValue) subTitle = $"Project time entries from {from:yyyy-MM-dd}.";
        else subTitle = $"Project time entries from {from:yyyy-MM-dd} to {to:yyyy-MM-dd}.";

        var html = new HtmlWriter($"Project Report for Project {projectName}", subTitle);

        html.W("""
               <table class="striped">
               <thead>
                 <tr>
                   <th>Day</th>
                   <th>Person</th>
                   <th>Task</th>
                   <th style="text-align:right; width:1px;">Duration</th>
                 </tr>
               </thead>
               <tbody>
               """);

        var sumHours = 0.0;
        foreach (var t in entries.Where(x => x.End != null))
        {
            var hours = (t.End!.Value - t.Begin).TotalHours;
            sumHours += hours;

            html.W($"""
                    <tr>
                      <td>{t.Begin:yyyy-MM-dd}</td>
                      <td>{t.User?.Title}</td>
                      <td>{(t.Activity?.Name + ": " + t.Description).Trim(' ', ':')}</td>
                      <td style="text-align:right;">{hours:N2}h</td>
                    </tr>
                    """);
        }

        html.W($"""
                <tr>
                  <td colspan="3" style="text-align:right;"><b>Total</b></td>
                  <td style="text-align:right; text-decoration-line:underline; text-decoration-style:double;"><b>{sumHours:N2}h</b></td>
                </tr>
                """);

        if (pool > 0)
        {
            var remainingPoolHours = pool + poolOffset - sumHours;

            html.W($"""
                    <tr>
                      <td colspan="4">&nbsp;</td>
                    </tr>
                    <tr>
                      <td colspan="3" style="text-align:right;">Total pool of hours (retainer scope)</td>
                      <td style="text-align:right;">{pool:N2}h</td>
                    </tr>
                    <tr>
                      <td colspan="3" style="text-align:right;">Remaining in pool</td>
                      <td style="text-align:right;">{remainingPoolHours:N2}h</td>
                    </tr>
                    """);

            if (remainingPoolHours < 0)
            {
                html.W($"""
                        <tr>
                          <td colspan="4" style="text-align:right;"><b>This pool of hours (retainer) is overdrawn!</b></td>
                        </tr>
                        """);
            }
        }

        html.W("""
               </tbody>
               </table>
               """);

        await File.WriteAllTextAsync(file.FullName, html.ToString());
        Console.WriteLine($"File \"{file.FullName}\" created.");
    }
}
