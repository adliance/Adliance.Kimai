using System.Reflection;
using Adliance.Kimai.Vacations;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
}
builder.Services.Configure<KimaiSettings>(builder.Configuration.GetSection("Kimai"));
builder.Services.AddScoped<VacationICalService>();

var app = builder.Build();
app.UseStatusCodePages();
app.UseDeveloperExceptionPage();

app.MapGet("/", () => Results.Content($"Adliance Kimai Vacations v{typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}", "text/plain"));

app.MapGet("/ical", async (VacationICalService service) =>
{
    var content = await service.GetICalFeedAsync();
    return Results.Content(content, "text/calendar");
});

app.Run();
