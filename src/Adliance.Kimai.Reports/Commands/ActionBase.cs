using System.CommandLine;
using System.CommandLine.Invocation;
using Adliance.Kimai.Client;

namespace Adliance.Kimai.Reports.Commands;

public abstract class ActionBase : AsynchronousCommandLineAction
{
    protected ParseResult ParseResult { get; private set; } = null!;

    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = new())
    {
        ParseResult = parseResult;

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
            await PrepareResult(outputPath, data, configuration);

            Console.WriteLine("Done. Goodbye.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($": {ex.Message}");
            return -1;
        }
    }

    public abstract Task PrepareResult(string outputPath, Data data, Configuration configuration);
}
