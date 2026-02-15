using System.Runtime.CompilerServices;
using GhostCurve.Configuration;
using GhostCurve.Domain.Enums;
using GhostCurve.Domain.Interfaces;
using GhostCurve.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhostCurve.Simulation.Replay;

/// <summary>
/// Orchestrates historical replay of trade events from the database.
/// Streams events in time order and publishes them for processing.
/// Replay is near-instantaneous — no real-time delay simulation.
/// </summary>
public sealed class ReplayOrchestrator
{
    private readonly ITradeEventStore _tradeEventStore;
    private readonly ReplayOptions _replayOptions;
    private readonly ILogger<ReplayOrchestrator> _logger;

    public ReplayOrchestrator(
        ITradeEventStore tradeEventStore,
        IOptions<ReplayOptions> replayOptions,
        ILogger<ReplayOrchestrator> logger)
    {
        _tradeEventStore = tradeEventStore;
        _replayOptions = replayOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Stream trade events for replay, applying configured time range and wallet filters.
    /// Events are yielded in deterministic order: (received_at_utc, id).
    /// Each event is tagged with TradeSource.Replay.
    /// </summary>
    public async IAsyncEnumerable<TradeEvent> StreamEventsAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var from = _replayOptions.From ?? throw new InvalidOperationException("Replay 'From' date must be configured");
        var to = _replayOptions.To ?? DateTimeOffset.UtcNow;

        _logger.LogInformation("Starting replay from {From} to {To}", from, to);

        var count = 0;
        var filterWallets = _replayOptions.FilterWallets.Count > 0
            ? new HashSet<string>(_replayOptions.FilterWallets, StringComparer.Ordinal)
            : null;

        await foreach (var tradeEvent in _tradeEventStore.GetEventsAsync(from, to, ct))
        {
            // Apply wallet filter if configured
            if (filterWallets is not null && !filterWallets.Contains(tradeEvent.TraderPublicKey))
                continue;

            tradeEvent.Source = TradeSource.Replay;
            count++;

            if (count % 1000 == 0)
                _logger.LogInformation("Replay progress: {Count} events streamed", count);

            yield return tradeEvent;
        }

        _logger.LogInformation("Replay complete: {Count} events streamed", count);
    }
}
