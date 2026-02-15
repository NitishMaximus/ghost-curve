using System.Threading.Channels;
using GhostCurve.Configuration;
using GhostCurve.Domain.Interfaces;
using GhostCurve.Domain.Models;
using GhostCurve.Infrastructure.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhostCurve.Worker.BackgroundServices;

/// <summary>
/// Background service that connects to PumpPortal WebSocket, receives trade events,
/// persists them to the database, and publishes them to the in-memory channel.
/// Handles automatic reconnection with exponential backoff.
/// This service is disabled when replay mode is active.
/// </summary>
public sealed class PumpPortalListenerService : BackgroundService
{
    private readonly PumpPortalClient _wsClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ChannelWriter<TradeEvent> _channelWriter;
    private readonly ReplayOptions _replayOptions;
    private readonly ILogger<PumpPortalListenerService> _logger;

    // Batch buffer for efficient DB writes
    private readonly List<TradeEvent> _writeBatch = new(50);
    private DateTime _lastFlush = DateTime.UtcNow;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(100);

    public PumpPortalListenerService(
        PumpPortalClient wsClient,
        IServiceScopeFactory scopeFactory,
        Channel<TradeEvent> channel,
        IOptions<ReplayOptions> replayOptions,
        ILogger<PumpPortalListenerService> logger)
    {
        _wsClient = wsClient;
        _scopeFactory = scopeFactory;
        _channelWriter = channel.Writer;
        _replayOptions = replayOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_replayOptions.Enabled)
        {
            _logger.LogInformation("Replay mode active — PumpPortal listener disabled");
            return;
        }

        _logger.LogInformation("PumpPortal listener starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _wsClient.ConnectAndSubscribeAsync(stoppingToken);

                while (_wsClient.IsConnected && !stoppingToken.IsCancellationRequested)
                {
                    var tradeEvent = await _wsClient.ReceiveTradeEventAsync(stoppingToken);

                    if (tradeEvent is null)
                    {
                        // Null means invalid message, duplicate, or clean close — check connection
                        if (!_wsClient.IsConnected)
                            break;
                        continue;
                    }

                    // Add to batch for DB persistence
                    _writeBatch.Add(tradeEvent);

                    // Flush to DB if batch is full or interval elapsed
                    if (_writeBatch.Count >= 50 || DateTime.UtcNow - _lastFlush > FlushInterval)
                    {
                        await FlushBatchAsync(stoppingToken);
                    }


                    // Publish to in-memory channel for processing
                    await _channelWriter.WriteAsync(tradeEvent, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Flush any remaining events before reconnect
                await FlushBatchSafeAsync(stoppingToken);

                var delay = _wsClient.GetReconnectDelay();
                _logger.LogWarning(ex, "WebSocket error — reconnecting in {Delay}ms", delay.TotalMilliseconds);
                await Task.Delay(delay, stoppingToken);
            }
        }

        // Final flush on shutdown
        await FlushBatchSafeAsync(CancellationToken.None);
        _channelWriter.TryComplete();
        _logger.LogInformation("PumpPortal listener stopped");
    }

    private async Task FlushBatchAsync(CancellationToken ct)
    {
        if (_writeBatch.Count == 0) return;

        using var scope = _scopeFactory.CreateScope();
        var tradeEventStore = scope.ServiceProvider.GetRequiredService<ITradeEventStore>();
        var count = await tradeEventStore.InsertBatchAsync(_writeBatch, ct);
        _logger.LogDebug("Flushed {Count} trade events to database", count);

        _writeBatch.Clear();
        _lastFlush = DateTime.UtcNow;
    }

    private async Task FlushBatchSafeAsync(CancellationToken ct)
    {
        try
        {
            await FlushBatchAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush trade event batch to database");
            _writeBatch.Clear();
        }
    }
}
