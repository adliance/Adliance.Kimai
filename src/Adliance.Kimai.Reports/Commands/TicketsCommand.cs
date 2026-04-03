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
        Description = "A date to start the report from.",
        Required = false
    };

    public TicketsCommand() : base("tickets", "Creates an report summarizing all tickets worked on.")
    {
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

        var file = new FileInfo(Path.Combine(outputPath, "tickets.html"));

        var html = new HtmlWriter("Tickets", $"Generated on {DateTime.Now:yyyy-MM-dd HH:mm} for entries between {from:yyyy-MM-dd} and {to:yyyy-MM-dd}");

        foreach (var c in data.Customers.OrderBy(x => x.Name))
        {
            var customerHasBeenPrinted = false;

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
                    var minutesInTickets = tickets.Sum(x => x.DurationMinutes);
                    var minutesWithoutTicket = unknownTicket.DurationMinutes;

                    if (!customerHasBeenPrinted)
                    {
                        html.W("<h4>" + c.Name + "</h4>");
                        customerHasBeenPrinted = true;
                    }

                    html.W($"""
                            <details>
                                <summary><b>{p.Name}</b>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
                                {minutesInTickets.Minutes().Humanize(maxUnit: TimeUnit.Hour)} in {"ticket".ToQuantity(tickets.Count)}&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
                                {minutesWithoutTicket.Minutes().Humanize(maxUnit: TimeUnit.Hour)} without ticket ({100d / (minutesWithoutTicket + minutesInTickets) * minutesWithoutTicket:N0}%)</summary>
                            """);


                    html.W("""
                           <table class="striped">
                           <thead>
                             <tr>
                               <th style="width:1px;">Ticket</th>
                               <th>Users</th>
                               <th>Descriptions</th>
                               <th style="text-align:right; width:1px;">Entries</th>
                               <th style="text-align:right; width:1px;">Worked</th>
                             </tr>
                           </thead>
                           <tbody>
                           """);

                    tickets = tickets.OrderByDescending(x => x.DurationMinutes).ToList();
                    tickets.Add(unknownTicket);
                    foreach (var ticket in tickets)
                    {
                        html.W($"""
                                <tr>
                                  <td style="vertical-align:top; white-space:nowrap;">{ticket.Identifier}</td>
                                  <td style="vertical-align:top;"><small>{string.Join("<br />", ticket.Users)}</small></td>
                                  <td style="vertical-align:top;"><small>{string.Join("<br />", ticket.Descriptions)}</small></td>
                                  <td style="text-align:right; vertical-align:top;">{ticket.Timesheets.Count:N0}</td>
                                  <td style="text-align:right; vertical-align:top; white-space:nowrap;">{ticket.DurationMinutes.Minutes().Humanize(maxUnit: TimeUnit.Hour)}</td>
                                </tr>
                                """);
                    }

                    html.W("</tbody></table></details>");
                }
            }
        }

        await File.WriteAllTextAsync(file.FullName, html.ToString());
        Console.WriteLine($"File \"{file.FullName}\" created.");
    }

    private static string? FindTicketIdentifier(Timesheet timesheet)
    {
        var text = timesheet.Description;
        if (string.IsNullOrWhiteSpace(text)) return null;

        var match = Regex.Match(text, @"(\w{2,4}\-\d{2,5})");
        if (match.Success) return match.Groups[1].Value;

        match = Regex.Match(text, @"(\#\d{3,5})");
        if (match.Success) return match.Groups[1].Value;

        match = Regex.Match(text, @"(\d{3,5})");
        if (match.Success) return match.Groups[1].Value;

        return null;
    }

    public class Ticket
    {
        public required string Identifier { get; init; }
        public List<Timesheet> Timesheets { get; set; } = [];

        public double DurationMinutes => Timesheets.Sum(x => x.DurationMinutes);
        public IEnumerable<string> Users => Timesheets.Select(x => x.User?.Title ?? "Unknown User").Distinct().OrderBy(x => x);
        public IEnumerable<string> Descriptions => Timesheets.Select(x => x.Description).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x);
    }
}
