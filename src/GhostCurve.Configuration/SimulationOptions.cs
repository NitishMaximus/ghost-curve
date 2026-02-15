using System.ComponentModel.DataAnnotations;

namespace GhostCurve.Configuration;

/// <summary>
/// Configuration for the simulation engine — position sizing, slippage, delay.
/// Snapshot of this is persisted per session for reproducibility.
/// </summary>
public sealed class SimulationOptions
{
    public const string SectionName = "Simulation";

    /// <summary>Initial virtual SOL balance for the wallet.</summary>
    [Range(0.01, 10_000)]
    public decimal InitialSolBalance { get; set; } = 10m;

    /// <summary>Fixed SOL amount per copy trade (position size).</summary>
    [Range(0.001, 1_000)]
    public decimal PositionSizeSol { get; set; } = 1m;

    /// <summary>Simulated execution delay in milliseconds (0 = instant).</summary>
    [Range(0, 30_000)]
    public int ExecutionDelayMs { get; set; } = 500;

    /// <summary>Base slippage in basis points applied to every trade.</summary>
    [Range(0, 5_000)]
    public decimal BaseSlippageBps { get; set; } = 100m;

    /// <summary>
    /// Price impact multiplier — additional slippage proportional to trade size vs. curve liquidity.
    /// Formula: totalSlippage = baseSlippageBps + (solAmount / vSolInCurve) * impactFactor * 10000
    /// </summary>
    [Range(0, 100)]
    public decimal PriceImpactFactor { get; set; } = 1.0m;

    /// <summary>Maximum total slippage (base + impact) in bps before rejecting a trade.</summary>
    [Range(0, 10_000)]
    public decimal MaxSlippageBps { get; set; } = 1_000m;

    /// <summary>Maximum copy trades per tracked wallet per minute (rate limiting).</summary>
    [Range(1, 1_000)]
    public int MaxTradesPerWalletPerMinute { get; set; } = 10;

    /// <summary>Interval in seconds between performance snapshot persistence.</summary>
    [Range(10, 3_600)]
    public int SnapshotIntervalSeconds { get; set; } = 60;

    /// <summary>Whether to skip tokens that have migrated off the bonding curve (pool != "pump").</summary>
    public bool SkipMigratedTokens { get; set; } = true;
}
