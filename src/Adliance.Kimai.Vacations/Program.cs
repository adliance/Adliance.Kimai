using Adliance.Kimai.Vacations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<KimaiSettings>(builder.Configuration.GetSection("Kimai"));
builder.Services.AddScoped<VacationICalService>();

var app = builder.Build();
app.UseStatusCodePages();
app.UseDeveloperExceptionPage();

app.MapGet("/", () => Results.Content("Adliance Kimai Vacations", "text/plain"));

app.MapGet("/ical", async (VacationICalService service) =>
{
    var content = await service.GetICalFeedAsync();
    return Results.Content(content, "text/calendar");
});

app.Run();
