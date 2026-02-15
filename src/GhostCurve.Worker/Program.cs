using System.Threading.Channels;
using GhostCurve.Configuration;
using GhostCurve.Domain.Interfaces;
using GhostCurve.Domain.Models;
using GhostCurve.Infrastructure.Persistence;
using GhostCurve.Infrastructure.Persistence.Repositories;
using GhostCurve.Infrastructure.WebSocket;
using GhostCurve.Simulation.Execution;
using GhostCurve.Simulation.Metrics;
using GhostCurve.Simulation.Portfolio;
using GhostCurve.Simulation.Pricing;
using GhostCurve.Simulation.Replay;
using GhostCurve.Worker.BackgroundServices;
using Microsoft.EntityFrameworkCore;
using Serilog;

// ── Serilog bootstrap ──
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/ghostcurve-.log", rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("GhostCurve starting");

    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSerilog();

    // ── Configuration (strongly-typed) ──
    builder.Services.Configure<SimulationOptions>(builder.Configuration.GetSection(SimulationOptions.SectionName));
    builder.Services.Configure<WebSocketOptions>(builder.Configuration.GetSection(WebSocketOptions.SectionName));
    builder.Services.Configure<WalletTrackingOptions>(builder.Configuration.GetSection(WalletTrackingOptions.SectionName));
    builder.Services.Configure<ReplayOptions>(builder.Configuration.GetSection(ReplayOptions.SectionName));

    // ── Database ──
    builder.Services.AddDbContext<GhostCurveDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("GhostCurveDb")));

    // ── Repositories ──
    builder.Services.AddScoped<ITradeEventStore, TradeEventRepository>();
    builder.Services.AddScoped<ISimulatedTradeStore, SimulatedTradeRepository>();

    // ── In-memory event channel (bounded, MPSC) ──
    builder.Services.AddSingleton(Channel.CreateBounded<TradeEvent>(new BoundedChannelOptions(10_000)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    }));

    // ── Simulation components ──
    builder.Services.AddSingleton<IPriceResolver, BondingCurvePriceResolver>();
    builder.Services.AddSingleton<SlippageModel>();
    builder.Services.AddSingleton<ITradeExecutor, SimulationTradeExecutor>();
    builder.Services.AddSingleton<IPortfolioManager, PortfolioManager>();
    builder.Services.AddSingleton<MetricsEngine>();

    // ── WebSocket client ──
    builder.Services.AddSingleton<PumpPortalClient>();

    // ── Replay ──
    builder.Services.AddScoped<ReplayOrchestrator>();

    // ── Background services ──
    builder.Services.AddHostedService<PumpPortalListenerService>();
    builder.Services.AddHostedService<TradeProcessorService>();
    builder.Services.AddHostedService<ReplayService>();

    var host = builder.Build();

    // Ensure database is created/migrated
    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<GhostCurveDbContext>();
        await db.Database.MigrateAsync();
        Log.Information("Database migration complete");
    }

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "GhostCurve terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
