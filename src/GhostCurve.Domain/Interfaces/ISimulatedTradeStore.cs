using GhostCurve.Domain.Models;

namespace GhostCurve.Domain.Interfaces;

/// <summary>
/// Persistence for simulated trades and simulation sessions.
/// </summary>
public interface ISimulatedTradeStore
{
    Task InsertAsync(SimulatedTrade trade, CancellationToken ct);

    Task<IReadOnlyList<SimulatedTrade>> GetBySessionAsync(Guid sessionId, CancellationToken ct);

    Task InsertSessionAsync(SimulationSession session, CancellationToken ct);

    Task UpdateSessionAsync(SimulationSession session, CancellationToken ct);

    Task InsertSnapshotAsync(PerformanceSnapshot snapshot, CancellationToken ct);
}
