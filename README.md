# Adliance.Kimai

[![NuGet](https://img.shields.io/nuget/v/Adliance.Kimai.svg)](https://www.nuget.org/packages/Adliance.Kimai)

Some little tool for Adliance that fetches data from Kimai to build some reports.

## Installation

Ensure you have the .NET 11 SDK installed.

### From NuGet as global tool

```bash
dotnet tool install --global Adliance.Kimai
```

### From Source

Clone the repository and build the project:

```bash
dotnet build
```

## Usage

You can run the tool using `dotnet run`:

```bash
dotnet run --project src/Adliance.Kimai.csproj -- [command] [options]
```

### Commands

#### `overview`
Creates an overview report by fetching data from Kimai.

**Options:**
* `-u|--url <url>` (Required): The URL to your Kimai instance. For example: `https://demo.kimai.org/`.
* `-t|--token <token>` (Required): Your Kimai API token.
* `--from <date>` (Optional): Calculate the report only starting with (and including) this day. Employments that begin earlier are only calculated starting with this day. Defaults to the beginning of the first employment.
* `--until <date>` (Optional): Calculate the report only up to (and including) this day, to get the state at this day. Defaults to today.

**Example:**
```bash
dotnet run --project src/Adliance.Kimai.csproj -- overview --url https://your-kimai-instance.com --token your-api-token
dotnet run --project src/Adliance.Kimai.csproj -- overview --url https://your-kimai-instance.com --token your-api-token --until 2025-12-31
dotnet run --project src/Adliance.Kimai.csproj -- overview --url https://your-kimai-instance.com --token your-api-token --from 2025-01-01 --until 2025-12-31
```

#### `example-config`
Creates an example `config.json` file in the current directory. This file is required to specify details for users (e.g., employment periods, worktime offsets).

**Example:**
```bash
dotnet run --project src/Adliance.Kimai.csproj -- example-config
```

## Configuration

The tool requires a `config.json` file to map Kimai data to specific users and their employment details. You can generate an initial version using the `example-config` command.

The configuration file includes:
* `username`: The Kimai username (email).
* `offset_worktime_minutes`: Initial overtime/undertime offset in minutes.
* `offset_vacation_minutes`: Initial vacation balance offset in minutes.
* `employment_start_for_vacation_entitlement` (optional): The day the employment actually started, if this is before the first configured employment (e.g. because Kimai was introduced later). Used to determine the working years for the vacation entitlement according to the Austrian vacation law. Defaults to the begin of the first employment.
* `employments`: A list of employment periods, including:
    * `begin`: Start date (YYYY-MM-DD).
    * `end`: End date (YYYY-MM-DD).
    * `minutes_per_day`: Expected working minutes per day.
    * `weekdays`: Array of working days (e.g., `Monday`, `Tuesday`).
