using GhostCurve.Domain.Models;

namespace GhostCurve.Domain.Interfaces;

/// <summary>
/// Persistence abstraction for trade events — the append-only event log.
/// </summary>
public interface ITradeEventStore
{
    /// <summary>Persist a batch of trade events. Skips duplicates by signature.</summary>
    Task<int> InsertBatchAsync(IReadOnlyList<TradeEvent> events, CancellationToken ct);

    /// <summary>
    /// Stream trade events for a time range, ordered by (received_at_utc, id).
    /// Uses cursor-based pagination for efficient replay.
    /// </summary>
    IAsyncEnumerable<TradeEvent> GetEventsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct);

    /// <summary>
    /// Get trade events for a specific trader within a time range.
    /// </summary>
    IAsyncEnumerable<TradeEvent> GetEventsByTraderAsync(
        string traderPublicKey,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct);
}
