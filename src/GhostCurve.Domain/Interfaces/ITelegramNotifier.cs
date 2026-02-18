namespace GhostCurve.Domain.Interfaces;

/// <summary>
/// Interface for sending trade notifications via external channels (e.g., Telegram).
/// </summary>
public interface ITelegramNotifier
{
    /// <summary>
    /// Sends a notification for a buy trade.
    /// </summary>
    /// <param name="mint">Token mint address.</param>
    /// <param name="traderWallet">Trader wallet display name.</param>
    /// <param name="solAmount">Amount of SOL spent.</param>
    /// <param name="tokenAmount">Amount of tokens received.</param>
    /// <param name="price">Effective price per token in SOL.</param>
    /// <param name="slippageBps">Slippage in basis points.</param>
    /// <param name="vSolInBondingCurve">Virtual SOL reserves in bonding curve after trade (market cap proxy).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyBuyAsync(
        string mint,
        string traderWallet,
        decimal solAmount,
        decimal tokenAmount,
        decimal price,
        decimal slippageBps,
        decimal vSolInBondingCurve,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification for a sell trade.
    /// </summary>
    /// <param name="mint">Token mint address.</param>
    /// <param name="traderWallet">Trader wallet display name.</param>
    /// <param name="tokenAmount">Amount of tokens sold.</param>
    /// <param name="solReceived">Amount of SOL received.</param>
    /// <param name="realizedPnl">Realized profit/loss in SOL.</param>
    /// <param name="vSolInBondingCurveAtBuy">Virtual SOL reserves when token was bought.</param>
    /// <param name="vSolInBondingCurveAtSell">Virtual SOL reserves when token was sold.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifySellAsync(
        string mint,
        string traderWallet,
        decimal tokenAmount,
        decimal solReceived,
        decimal realizedPnl,
        decimal vSolInBondingCurveAtBuy,
        decimal vSolInBondingCurveAtSell,
        CancellationToken cancellationToken = default);
}
