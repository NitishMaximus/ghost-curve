using GhostCurve.Domain.Enums;

namespace GhostCurve.Domain.Interfaces;

/// <summary>
/// Represents an intent to execute a trade — decoupled from how it's executed.
/// Used by both simulation and (future) live execution engines.
/// </summary>
public sealed record TradeIntent(
    string Mint,
    TradeType Side,
    decimal SolAmount,
    decimal MaxSlippageBps,
    decimal VTokensInBondingCurve,
    decimal VSolInBondingCurve,
    long SourceTradeEventId,
    int DelayMs);

/// <summary>
/// Result of executing a trade — whether simulated or real.
/// </summary>
public sealed record TradeExecutionResult(
    bool Success,
    decimal ActualTokenAmount,
    decimal ActualSolAmount,
    decimal EffectivePrice,
    decimal SlippageBps,
    string? TxSignature = null,
    string? ErrorReason = null);

/// <summary>
/// Abstraction over trade execution — the seam between simulation and live trading.
/// Phase 1: SimulationTradeExecutor (bonding curve math).
/// Phase 2: JupiterTradeExecutor (on-chain swap).
/// </summary>
public interface ITradeExecutor
{
    Task<TradeExecutionResult> ExecuteAsync(TradeIntent intent, CancellationToken ct);
}
