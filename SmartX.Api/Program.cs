using SmartX.Api.Data;
using SmartX.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// single instances shared across every request, this is our in memory database for part 1
builder.Services.AddSingleton<SensorStore>();
builder.Services.AddSingleton<TelemetryHistoryStore>();

// part 2 additions, same idea, one shared instance each
builder.Services.AddSingleton<TelemetryIngestionQueue>();
builder.Services.AddSingleton<CommandHistoryStack>();
builder.Services.AddSingleton<DeviceRegistry>();
builder.Services.AddSingleton<TimestampedLogStore>();
builder.Services.AddSingleton<DeviceAlertTracker>();
builder.Services.AddSingleton<ActionPatternEngine>();
builder.Services.AddHostedService<TelemetryProcessingService>();

// client runs on a different port so it needs cors turned on to call the api
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();
