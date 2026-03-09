using System.CommandLine;

namespace Adliance.Kimai.Reports.Commands;

public abstract class CommandBase : Command
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

    public static readonly Option<string> ConfigPath = new("--config", "-c")
    {
        Description = "The path where you want to store the resulting HTML reports. Defaults to \"./config.json\".",
        Required = false
    };

    public static readonly Option<string> OutputPath = new("--output", "-o")
    {
        Description = "The path where you want to store the resulting HTML reports. Defaults to the current working directory \"./\".",
        Required = false
    };

    protected CommandBase(string name, string description) : base(name, description)
    {
        Options.Add(UrlOption);
        Options.Add(TokenOption);
        Options.Add(ConfigPath);
        Options.Add(OutputPath);
    }
}
