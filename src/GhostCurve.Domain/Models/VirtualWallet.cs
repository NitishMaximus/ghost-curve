namespace GhostCurve.Domain.Models;

/// <summary>
/// In-memory virtual wallet tracking SOL balance, open positions, and realized PnL.
/// Only mutated by the single-threaded TradeProcessorService — no locking required.
/// </summary>
public sealed class VirtualWallet
{
    /// <summary>Available SOL balance.</summary>
    public decimal SolBalance { get; set; }

    /// <summary>Open positions keyed by token mint address.</summary>
    public Dictionary<string, Position> Positions { get; } = new(StringComparer.Ordinal);

    /// <summary>Running total of realized PnL in SOL across all closed/partial positions.</summary>
    public decimal TotalRealizedPnl { get; set; }

    /// <summary>Total number of trades executed.</summary>
    public int TotalTradeCount { get; set; }

    /// <summary>Number of winning trades (realized PnL > 0).</summary>
    public int WinCount { get; set; }

    /// <summary>Number of losing trades (realized PnL <= 0).</summary>
    public int LossCount { get; set; }

    /// <summary>High-water mark of portfolio value in SOL — used for drawdown calculation.</summary>
    public decimal HighWaterMark { get; set; }

    /// <summary>Maximum drawdown observed as a percentage.</summary>
    public decimal MaxDrawdownPercent { get; set; }

    /// <summary>Sum of all individual trade ROI percentages — for computing average.</summary>
    public decimal CumulativeRoiPercent { get; set; }
}
