using CarSimulatorService;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", false, true);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Logging.AddSerilog(Log.Logger);

var carSimulatorSettings = builder.Configuration.GetSection("CarSimulator").Get<CarSimulationSettings>() ??
                           new CarSimulationSettings();

builder.Services.AddSingleton(carSimulatorSettings);
builder.Services.AddSingleton<RedisQueue>();

builder.Services.AddHostedService<CarSimulatorWorker>();
builder.Services.AddHostedService<RedisMessageProcessor>();

var host = builder.Build();

// Ensure Logs flush on shutdown
try
{
    Log.Information("Starting CarSimulationService...");

    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start!");
}
finally
{
    Log.CloseAndFlush();
}