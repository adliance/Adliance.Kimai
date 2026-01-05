using System.CommandLine;
using System.CommandLine.Invocation;

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
        var token = parseResult.GetRequiredValue(OverviewCommand.UrlOption);

        try
        {
            var client = new KimaiClient.KimaiClient(url, token);

            var data = await Data.LoadFromCacheOrKimai(client);
            Console.WriteLine(data);
            Console.WriteLine();
            Console.WriteLine();

            var configuration = await Configuration.Load();
            new CalculationService(configuration, data).Calculate();
            Console.WriteLine(configuration);

            Console.WriteLine("Done. Goodbye.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return -1;
        }
    }
}
