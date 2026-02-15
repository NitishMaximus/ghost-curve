namespace GhostCurve.Domain.Models;

/// <summary>
/// Represents an open position in a single token within the virtual portfolio.
/// Mutable — updated as buys/sells occur.
/// </summary>
public sealed class Position
{
    /// <summary>Token mint address.</summary>
    public required string Mint { get; set; }

    /// <summary>Current token balance held.</summary>
    public decimal TokenBalance { get; set; }

    /// <summary>Volume-weighted average entry price in SOL per token.</summary>
    public decimal AvgEntryPrice { get; set; }

    /// <summary>Total SOL cost basis (sum of all buy costs for this position).</summary>
    public decimal TotalCostBasis { get; set; }

    /// <summary>When this position was first opened.</summary>
    public DateTimeOffset OpenedAtUtc { get; set; }

    /// <summary>Number of buy trades that contributed to this position.</summary>
    public int BuyCount { get; set; }

    /// <summary>Number of sell trades against this position.</summary>
    public int SellCount { get; set; }

    /// <summary>Whether this position has been fully closed.</summary>
    public bool IsClosed => TokenBalance <= 0m;
}
