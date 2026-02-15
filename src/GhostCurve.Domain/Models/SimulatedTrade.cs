using GhostCurve.Domain.Enums;

namespace GhostCurve.Domain.Models;

/// <summary>
/// A simulated copy trade executed by the simulation engine.
/// Immutable after creation — represents a decision and its computed outcome.
/// </summary>
public sealed class SimulatedTrade
{
    public long Id { get; init; }

    /// <summary>FK to the trade event that triggered this copy trade.</summary>
    public long SourceTradeEventId { get; init; }

    /// <summary>Reference to the simulation session.</summary>
    public Guid SessionId { get; init; }

    /// <summary>Token mint address.</summary>
    public required string Mint { get; init; }

    public TradeType Side { get; init; }

    /// <summary>SOL amount used (our position size).</summary>
    public decimal SolAmount { get; init; }

    /// <summary>Token amount received/sold (computed from bonding curve math).</summary>
    public decimal TokenAmount { get; init; }

    /// <summary>Effective price in SOL per token at execution.</summary>
    public decimal SimulatedPrice { get; init; }

    /// <summary>Slippage applied in basis points.</summary>
    public decimal SlippageBps { get; init; }

    /// <summary>Configured execution delay at the time of this trade (ms).</summary>
    public int DelayMs { get; init; }

    /// <summary>When this simulated trade was executed.</summary>
    public DateTimeOffset ExecutedAtUtc { get; init; }

    /// <summary>Bonding curve virtual token reserves at execution time.</summary>
    public decimal VTokensAtExecution { get; init; }

    /// <summary>Bonding curve virtual SOL reserves at execution time.</summary>
    public decimal VSolAtExecution { get; init; }

    /// <summary>Realized PnL in SOL — populated on sell trades only.</summary>
    public decimal? RealizedPnl { get; init; }
}
