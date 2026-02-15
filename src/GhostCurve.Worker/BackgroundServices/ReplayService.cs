using System.Threading.Channels;
using GhostCurve.Configuration;
using GhostCurve.Domain.Models;
using GhostCurve.Simulation.Replay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhostCurve.Worker.BackgroundServices;

/// <summary>
/// Background service for historical replay mode.
/// Reads stored trade events via ReplayOrchestrator and publishes them
/// to the same Channel{TradeEvent} used by the live listener.
/// The entire downstream pipeline (processor, portfolio, metrics) is identical.
/// Activated only when Replay.Enabled = true in configuration.
/// </summary>
public sealed class ReplayService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ChannelWriter<TradeEvent> _channelWriter;
    private readonly ReplayOptions _replayOptions;
    private readonly ILogger<ReplayService> _logger;

    public ReplayService(
        IServiceScopeFactory scopeFactory,
        Channel<TradeEvent> channel,
        IOptions<ReplayOptions> replayOptions,
        ILogger<ReplayService> logger)
    {
        _scopeFactory = scopeFactory;
        _channelWriter = channel.Writer;
        _replayOptions = replayOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_replayOptions.Enabled)
        {
            _logger.LogInformation("Replay mode disabled — replay service inactive");
            return;
        }

        // Small delay to let the trade processor start first
        await Task.Delay(1000, stoppingToken);

        _logger.LogInformation("Replay service starting — reading from {From} to {To}",
            _replayOptions.From, _replayOptions.To ?? DateTimeOffset.UtcNow);

        try
        {
            var count = 0;

            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<ReplayOrchestrator>();

            await foreach (var tradeEvent in orchestrator.StreamEventsAsync(stoppingToken))
            {
                await _channelWriter.WriteAsync(tradeEvent, stoppingToken);
                count++;
            }

            _logger.LogInformation("Replay complete — {Count} events published to channel", count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Replay cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Replay failed");
        }
        finally
        {
            // Signal that no more events will come — this causes the processor to drain and exit
            _channelWriter.TryComplete();
            _logger.LogInformation("Replay service stopped — channel completed");
        }
    }
}
