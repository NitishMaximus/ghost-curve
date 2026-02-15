namespace GhostCurve.Domain.Models;

/// <summary>
/// Periodically persisted snapshot of portfolio performance metrics.
/// </summary>
public sealed class PerformanceSnapshot
{
    public long Id { get; init; }

    public Guid SessionId { get; init; }

    public DateTimeOffset SnapshotAtUtc { get; init; }

    public int TotalTrades { get; init; }

    public int WinCount { get; init; }

    public int LossCount { get; init; }

    /// <summary>Win rate as a percentage (0–100).</summary>
    public decimal WinRate { get; init; }

    /// <summary>Average ROI per trade as a percentage.</summary>
    public decimal AvgRoiPercent { get; init; }

    public decimal TotalRealizedPnl { get; init; }

    public decimal TotalUnrealizedPnl { get; init; }

    public decimal MaxDrawdownPercent { get; init; }

    public decimal SolBalance { get; init; }

    /// <summary>Total portfolio value (SOL balance + unrealized position values).</summary>
    public decimal TotalPortfolioValue { get; init; }
}
