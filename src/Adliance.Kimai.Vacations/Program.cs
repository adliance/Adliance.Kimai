using System.Net;
using System.Reflection;
using Adliance.Kimai.Vacations;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
}
builder.Services.Configure<KimaiOptions>(builder.Configuration.GetSection("Kimai"));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.AddScoped<VacationICalService>();

var app = builder.Build();
app.UseStatusCodePages();
app.UseDeveloperExceptionPage();

app.MapGet("/", () => Results.Content($"Adliance Kimai Vacations v{typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}", "text/plain"));

app.MapGet("/ical", async (VacationICalService service, [FromQuery(Name = "key")] string? key) =>
{
    try
    {
        var content = await service.GetICalFeedAsync(key);
        return Results.Content(content, "text/calendar");
    }
    catch (UnauthorizedAccessException)
    {
        return Results.StatusCode((int)HttpStatusCode.Forbidden);
    }
});

app.Run();
