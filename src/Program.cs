using System.CommandLine;
using Adliance.Kimai;

RootCommand rootCommand = new("Some little tool for Adliance that fetches data from Kimai to build some reports.")
{
    new OverviewCommand(),
    new ProjectReportCommand(),
    new ExampleConfigurationCommand()
};

var parseResult = rootCommand.Parse(args);
parseResult.Invoke();
