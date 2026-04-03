using System.CommandLine;
using System.Text.RegularExpressions;
using Adliance.Kimai.Client.Models;
using Humanizer;

namespace Adliance.Kimai.Reports.Commands;

public class TicketsCommand : CommandBase
{
    public static readonly Option<DateTime> FromOption = new("--from")
    {
        Description = "A date to start the report from.",
        Required = false
    };

    public static readonly Option<DateTime> ToOption = new("--to")
    {
        Description = "A date to end the report at.",
        Required = false
    };

    public static readonly Option<string> AdoOrganizationUrlOption = new("--ado-url")
    {
        Description = "The URL to your Azure DevOps organization. For example: https://dev.azure.com/myorg.",
        Required = false
    };

    public static readonly Option<string> AdoPatOption = new("--ado-pat")
    {
        Description = "A personal access token for Azure DevOps with work item read permissions.",
        Required = false
    };

    public TicketsCommand() : base("tickets", "Creates an report summarizing all tickets worked on.")
    {
        Options.Add(FromOption);
        Options.Add(ToOption);
        Options.Add(AdoOrganizationUrlOption);
        Options.Add(AdoPatOption);
        Action = new TicketsAction();
    }
}

public class TicketsAction : ActionBase
{
    public override async Task PrepareResult(string outputPath, Data data, Configuration configuration)
    {
        var from = ParseResult.GetValue(TicketsCommand.FromOption).Date;
        var to = ParseResult.GetValue(TicketsCommand.ToOption).Date;

        // if not specified, use the last month as from/to
        if (from == default) from = new DateTime(DateTime.Now.AddMonths(-1).Year, DateTime.Now.AddMonths(-1).Month, 1, 0, 0, 0);
        if (to == default) to = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddSeconds(-1);

        var groups = FindTickets(data, from, to);
        await EnrichWithDataFromAdo(groups);
        await WriteHtmlFile(groups, outputPath, from, to);
    }

    private static List<(Customer Customer, Project Project, List<Ticket> Tickets, Ticket UnknownTicket)> FindTickets(Data data, DateTime from, DateTime to)
    {
        var result = new List<(Customer Customer, Project Project, List<Ticket> Tickets, Ticket UnknownTicket)>();

        foreach (var c in data.Customers.OrderBy(x => x.Name))
        {
            foreach (var p in data.Projects.Where(x => x.Customer == c.Id).OrderBy(x => x.Name))
            {
                var tickets = new List<Ticket>();
                var unknownTicket = new Ticket
                {
                    Identifier = "Without Ticket"
                };

                foreach (var t in data.Timesheets.Where(x => x.IsBillable && x.Project == p.Id && x.Begin >= from && x.Begin <= to.AddDays(1).AddSeconds(-1)))
                {
                    var identifier = FindTicketIdentifier(t);
                    if (identifier == null)
                    {
                        unknownTicket.Timesheets.Add(t);
                        continue;
                    }

                    var ticket = tickets.SingleOrDefault(x => x.Identifier == identifier);
                    if (ticket == null)
                    {
                        ticket = new Ticket
                        {
                            Identifier = identifier
                        };
                        tickets.Add(ticket);
                    }

                    ticket.Timesheets.Add(t);
                }

                if (tickets.Count > 0 || unknownTicket.Timesheets.Count > 0)
                {
                    result.Add((c, p, tickets, unknownTicket));
                }
            }
        }

        return result;
    }

    private async Task EnrichWithDataFromAdo(List<(Customer Customer, Project Project, List<Ticket> Tickets, Ticket UnknownTicket)> groups)
    {
        // Enrich with Azure DevOps data if configured
        var adoUrl = ParseResult.GetValue(TicketsCommand.AdoOrganizationUrlOption);
        var adoPat = ParseResult.GetValue(TicketsCommand.AdoPatOption);
        if (!string.IsNullOrWhiteSpace(adoUrl) && !string.IsNullOrWhiteSpace(adoPat))
        {
            Console.WriteLine("Loading work items from Azure DevOps ...");
            var adoClient = new AzureDevOpsClient(adoUrl, adoPat);

            var allTickets = groups.SelectMany(g => g.Tickets).ToList();
            var numericIds = allTickets
                .Select(t => TryGetNumericId(t.Identifier, out var id) ? (int?)id : null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var workItems = await adoClient.LoadWorkItems(numericIds);
            foreach (var ticket in allTickets)
            {
                if (TryGetNumericId(ticket.Identifier, out var id))
                {
                    var workItem = workItems.SingleOrDefault(w => w.Id == id);
                    if (workItem != null)
                    {
                        ticket.AdoTitle = workItem.Fields?.Title;
                        ticket.AdoState = workItem.Fields?.State;
                        ticket.AdoUrl = workItem.Links?.Html?.Href;
                        ticket.AdoOriginalEstimate = workItem.Fields?.OriginalEstimate?.Hours();
                    }
                }
            }

            Console.WriteLine($"Loaded {workItems.Count} work items from Azure DevOps.");
        }
    }

    private static async Task WriteHtmlFile(List<(Customer Customer, Project Project, List<Ticket> Tickets, Ticket UnknownTicket)> groups, string outputPath, DateTime from, DateTime to)
    {
        // Render HTML
        var file = new FileInfo(Path.Combine(outputPath, "tickets.html"));
        var html = new HtmlWriter("Tickets", $"Generated on {DateTime.Now:yyyy-MM-dd HH:mm} for entries between {from:yyyy-MM-dd} and {to:yyyy-MM-dd}");

        foreach (var c in groups.Select(x => x.Customer).Distinct())
        {
            html.W($"""
                    <article>
                    <header><h5>{c.Name}</h5></header>
                    """);

            foreach (var (_, p, tickets, unknownTicket) in groups.Where(x => x.Customer == c))
            {
                var minutesWithoutTicket = unknownTicket.Duration.TotalMinutes;
                var minutesInTickets = tickets.Sum(x => x.Duration.TotalMinutes);
                var minutesInTicketsWithEstimation = tickets.Where(x => x.AdoOriginalEstimate != null).Sum(x => x.Duration.TotalMinutes);
                var minutesEstimated = tickets.Where(x => x.AdoOriginalEstimate != null).Sum(x => x.AdoOriginalEstimate!.Value.TotalMinutes);

                html.W($"""
                        <details>
                            <summary><b>{p.Name}</b>
                        """);

                if (minutesInTickets > 0)
                {
                    html.W($"""
                            &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
                            {minutesInTickets.Minutes().Humanize(maxUnit: TimeUnit.Hour)} in {"ticket".ToQuantity(tickets.Count)}
                            """);
                }

                if (minutesWithoutTicket > 0)
                {
                    html.W($"""
                            &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
                            {minutesWithoutTicket.Minutes().Humanize(maxUnit: TimeUnit.Hour)} without ticket ({100d / (minutesWithoutTicket + minutesInTickets) * minutesWithoutTicket:N0}%)
                            """);
                }

                if (minutesEstimated > 0)
                {
                    html.W($"""
                            &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
                            {100d / minutesEstimated * minutesInTicketsWithEstimation:N0}% of estimation
                            """);
                }

                html.W("</summary>");

                html.W($"""
                        <table class="striped">
                        <thead>
                          <tr>
                            <th colspan="2">Ticket</th>
                            <th style="width:1px;">Users</th>
                            <th style="text-align:right; width:1px;">Estimated</th>
                            <th style="text-align:right; width:1px;">Worked</th>
                          </tr>
                        </thead>
                        <tbody>
                        """);

                var sortedTickets = tickets.OrderByDescending(x => x.Duration).ToList();
                if (unknownTicket.Timesheets.Count > 0) sortedTickets.Add(unknownTicket);
                foreach (var ticket in sortedTickets)
                {
                    if (!string.IsNullOrEmpty(ticket.AdoTitle))
                    {
                        html.W($"""
                                <td colspan="2" style="vertical-align:top;">
                                  <a href="{ticket.AdoUrl}" target="_blank">#{ticket.Identifier} {ticket.AdoTitle}</a> ({ticket.AdoState})
                                </td>
                                """);
                    }
                    else
                    {
                        html.W($"""
                                <td style="vertical-align:top;">{ticket.Identifier}</td>
                                <td style="vertical-align:top;"><small>{string.Join("<br />", ticket.Descriptions)}</small></td>
                                """);
                    }

                    html.W($"""
                              <td style="vertical-align:top; white-space:nowrap;"><small>{string.Join("<br />", ticket.Users)}</small></td>
                              <td style="text-align:right; vertical-align:top; white-space:nowrap;">
                                {html.Tag("mark", ticket.AdoOriginalEstimate < ticket.Duration, ticket.AdoOriginalEstimate?.Humanize(maxUnit: TimeUnit.Hour) ?? "")}
                              </td>
                              <td style="text-align:right; vertical-align:top; white-space:nowrap;">{ticket.Duration.Humanize(maxUnit: TimeUnit.Hour)}</td>
                            </tr>
                            """);
                }

                html.W("</tbody></table></details>");
            }

            html.W("""
                    </article>
                    """);
        }

        await File.WriteAllTextAsync(file.FullName, html.ToString());
        Console.WriteLine($"File \"{file.FullName}\" created.");
    }

    private static string? FindTicketIdentifier(Timesheet timesheet)
    {
        var text = timesheet.Description;
        if (string.IsNullOrWhiteSpace(text)) return null;

        var match = Regex.Match(text, @"\#(\d{1,5})");
        if (match.Success) return match.Groups[1].Value;

        match = Regex.Match(text, @"(\w{2,5}\-\d{2,5})");
        if (match.Success) return match.Groups[1].Value;

        match = Regex.Match(text, @"(\d{3,5})");
        if (match.Success) return match.Groups[1].Value;

        return null;
    }

    private static bool TryGetNumericId(string identifier, out int id)
    {
        return int.TryParse(identifier, out id);
    }

    public class Ticket
    {
        public required string Identifier { get; init; }
        public List<Timesheet> Timesheets { get; set; } = [];
        public string? AdoTitle { get; set; }
        public string? AdoState { get; set; }
        public string? AdoUrl { get; set; }
        public TimeSpan? AdoOriginalEstimate { get; set; }
        public TimeSpan Duration => Timesheets.Sum(x => x.DurationMinutes).Minutes();
        public IEnumerable<string> Users => Timesheets.Select(x => x.User?.Title ?? "Unknown User").Distinct().OrderBy(x => x);
        public IEnumerable<string> Descriptions => Timesheets.Select(x => x.Description).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x);
    }
}
