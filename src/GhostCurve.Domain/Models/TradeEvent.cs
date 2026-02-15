using GhostCurve.Domain.Enums;

namespace GhostCurve.Domain.Models;

/// <summary>
/// Immutable record of a trade event received from PumpPortal WebSocket.
/// Append-only — never mutated after creation.
/// </summary>
public sealed class TradeEvent
{
    public long Id { get; init; }

    /// <summary>Solana transaction signature — unique identifier.</summary>
    public required string Signature { get; init; }

    /// <summary>Token mint (contract) address.</summary>
    public required string Mint { get; init; }

    /// <summary>The wallet address that made the trade.</summary>
    public required string TraderPublicKey { get; init; }

    public TradeType TxType { get; init; }

    public decimal TokenAmount { get; init; }

    public decimal SolAmount { get; init; }

    /// <summary>Trader's new token balance after the trade.</summary>
    public decimal NewTokenBalance { get; init; }

    public required string BondingCurveKey { get; init; }

    /// <summary>Virtual token reserves in the bonding curve after this trade.</summary>
    public decimal VTokensInBondingCurve { get; init; }

    /// <summary>Virtual SOL reserves in the bonding curve after this trade.</summary>
    public decimal VSolInBondingCurve { get; init; }

    /// <summary>Market cap denominated in SOL.</summary>
    public decimal MarketCapSol { get; init; }

    /// <summary>Pool identifier — "pump" for bonding curve, or Raydium/PumpSwap address.</summary>
    public string? Pool { get; init; }

    /// <summary>Timestamp when we received this event from the WebSocket.</summary>
    public DateTimeOffset ReceivedAtUtc { get; init; }

    /// <summary>Timestamp when this event was persisted to the database.</summary>
    public DateTimeOffset IngestedAtUtc { get; init; }

    /// <summary>Source of this event — live feed or historical replay.</summary>
    public TradeSource Source { get; set; } = TradeSource.Live;
}
