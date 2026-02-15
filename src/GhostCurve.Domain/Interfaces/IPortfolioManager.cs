using GhostCurve.Domain.Models;

namespace GhostCurve.Domain.Interfaces;

/// <summary>
/// Manages the virtual portfolio: positions, wallet balance, PnL tracking.
/// Single-threaded access only — no concurrent mutation.
/// </summary>
public interface IPortfolioManager
{
    VirtualWallet Wallet { get; }

    /// <summary>Record a buy — deduct SOL, add/update position.</summary>
    void RecordBuy(string mint, decimal solAmount, decimal tokenAmount, decimal price, DateTimeOffset timestamp, string traderPublicKey);

    /// <summary>Record a sell — add SOL, reduce/close position, compute realized PnL.</summary>
    decimal RecordSell(string mint, decimal solAmount, decimal tokenAmount, decimal price, DateTimeOffset timestamp, string traderPublicKey);

    /// <summary>Calculate total unrealized PnL given current bonding curve prices.</summary>
    decimal CalculateUnrealizedPnl(Func<string, decimal> currentPriceResolver);

    /// <summary>Get total portfolio value (SOL balance + mark-to-market positions).</summary>
    decimal GetTotalPortfolioValue(Func<string, decimal> currentPriceResolver);

    /// <summary>Reset the portfolio for a new session.</summary>
    void Reset(decimal initialSolBalance);
}
