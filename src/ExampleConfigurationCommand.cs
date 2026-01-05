using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using Adliance.Kimai.KimaiClient;

namespace Adliance.Kimai;

public class ExampleConfigurationCommand : Command
{
    public ExampleConfigurationCommand() : base("example-config", "Creates an example for the config.json file that is required to specify details for users.")
    {
        Action = new ExampleConfigurationAction();
    }
}

public class ExampleConfigurationAction : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = new())
    {
        var file = new FileInfo("config.json");

        if (file.Exists)
        {
            Console.WriteLine($"A file \"{file.FullName}\" already exists. Please remove it first to ensure it's not overwritten unintentionally.");
            return -1;
        }

        var exampleConfiguration = new Configuration
        {
            Users = new List<Configuration.User>([
                new Configuration.User
                {
                    Username = "hannes.sachsenhofer@adliance.net",
                    OffsetVacationsMinutes = 0,
                    OffsetWorktimeMinutes = 0,
                    Employments =
                    [
                        new Configuration.Employment
                        {
                            Begin = new DateOnly(2025, 4, 1),
                            End = new DateOnly(2025, 12, 31),
                            MinutesPerDay = 360,
                            Weekdays =
                            [
                                DayOfWeek.Monday,
                                DayOfWeek.Tuesday,
                                DayOfWeek.Wednesday,
                                DayOfWeek.Friday
                            ]
                        },
                        new Configuration.Employment
                        {
                            Begin = new DateOnly(2026, 1, 1)
                        }
                    ]
                }
            ])
        };

        await File.WriteAllTextAsync(file.FullName, JsonSerializer.Serialize(exampleConfiguration, LenientJsonOptions.Instance), cancellationToken);
        Console.WriteLine($"A file \"{file.FullName}\" was created.");
        return 0;
    }
}
