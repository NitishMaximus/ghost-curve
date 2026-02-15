using GhostCurve.Domain.Interfaces;
using GhostCurve.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace GhostCurve.Infrastructure.Persistence.Repositories;

/// <summary>
/// Persistence for simulated trades, sessions, and performance snapshots.
/// Uses EF Core — these are lower-volume writes than trade events.
/// </summary>
public sealed class SimulatedTradeRepository : ISimulatedTradeStore
{
    private readonly GhostCurveDbContext _db;

    public SimulatedTradeRepository(GhostCurveDbContext db)
    {
        _db = db;
    }

    public async Task InsertAsync(SimulatedTrade trade, CancellationToken ct)
    {
        _db.SimulatedTrades.Add(trade);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SimulatedTrade>> GetBySessionAsync(Guid sessionId, CancellationToken ct)
    {
        return await _db.SimulatedTrades
            .AsNoTracking()
            .Where(t => t.SessionId == sessionId)
            .OrderBy(t => t.ExecutedAtUtc)
            .ToListAsync(ct);
    }

    public async Task InsertSessionAsync(SimulationSession session, CancellationToken ct)
    {
        _db.SimulationSessions.Add(session);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateSessionAsync(SimulationSession session, CancellationToken ct)
    {
        _db.SimulationSessions.Update(session);
        await _db.SaveChangesAsync(ct);
    }

    public async Task InsertSnapshotAsync(PerformanceSnapshot snapshot, CancellationToken ct)
    {
        _db.PerformanceSnapshots.Add(snapshot);
        await _db.SaveChangesAsync(ct);
    }
}
