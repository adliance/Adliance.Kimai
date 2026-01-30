# Adliance.Kimai

Some little tool for Adliance that fetches data from Kimai to build some reports.

## Installation

Ensure you have the .NET 10 SDK installed. Clone the repository and build the project:

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

**Example:**
```bash
dotnet run --project src/Adliance.Kimai.csproj -- overview --url https://your-kimai-instance.com --token your-api-token
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
* `employments`: A list of employment periods, including:
    * `begin`: Start date (YYYY-MM-DD).
    * `end`: End date (YYYY-MM-DD).
    * `minutes_per_day`: Expected working minutes per day.
    * `weekdays`: Array of working days (e.g., `Monday`, `Tuesday`).
