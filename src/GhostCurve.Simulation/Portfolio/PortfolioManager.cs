using GhostCurve.Configuration;
using GhostCurve.Domain.Interfaces;
using GhostCurve.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhostCurve.Simulation.Portfolio;

/// <summary>
/// Manages the virtual portfolio: positions, wallet balance, PnL tracking, and drawdown.
/// Single-threaded access only (called exclusively by TradeProcessorService).
/// No locks, no concurrent mutation.
/// </summary>
public sealed class PortfolioManager : IPortfolioManager
{
    private readonly ILogger<PortfolioManager> _logger;
    private readonly WalletTrackingOptions _walletOptions;
    private readonly TelegramOptions _telegramOptions;
    private readonly ITelegramNotifier _telegramNotifier;

    public VirtualWallet Wallet { get; private set; } = new();

    public PortfolioManager(
        ILogger<PortfolioManager> logger,
        IOptions<WalletTrackingOptions> walletOptions,
        IOptions<TelegramOptions> telegramOptions,
        ITelegramNotifier telegramNotifier)
    {
        _logger = logger;
        _walletOptions = walletOptions.Value;
        _telegramOptions = telegramOptions.Value;
        _telegramNotifier = telegramNotifier;
    }

    public void Reset(decimal initialSolBalance)
    {
        Wallet = new VirtualWallet
        {
            SolBalance = initialSolBalance,
            HighWaterMark = initialSolBalance
        };
        _logger.LogInformation("Portfolio reset with {Balance} SOL", initialSolBalance);
    }

    /// <summary>
    /// Record a buy: deduct SOL from wallet, create or add to position.
    /// </summary>
    public void RecordBuy(string mint, decimal solAmount, decimal tokenAmount, decimal price, DateTimeOffset timestamp, string traderPublicKey, decimal vSolInBondingCurve)
    {
        if (Wallet.SolBalance < solAmount)
        {
            _logger.LogWarning("Insufficient SOL balance ({Balance}) for buy of {Amount} SOL on {Mint}",
                Wallet.SolBalance, solAmount, mint);
            return;
        }

        Wallet.SolBalance -= solAmount;
        Wallet.TotalTradeCount++;

        decimal slippageBps = 0m; // Will be populated from execution result if needed
        if (Wallet.Positions.TryGetValue(mint, out var position))
        {
            // Add to existing position — update VWAP
            var totalCost = position.TotalCostBasis + solAmount;
            var totalTokens = position.TokenBalance + tokenAmount;
            position.AvgEntryPrice = totalTokens > 0 ? totalCost / totalTokens : 0;
            position.TokenBalance = totalTokens;
            position.TotalCostBasis = totalCost;
            position.BuyCount++;
        }
        else
        {
            // New position
            Wallet.Positions[mint] = new Position
            {
                Mint = mint,
                TokenBalance = tokenAmount,
                AvgEntryPrice = price,
                TotalCostBasis = solAmount,
                OpenedAtUtc = timestamp,
                VSolInBondingCurveAtOpen = vSolInBondingCurve,
                BuyCount = 1
            };
        }

        _logger.LogInformation("BUY {Mint} by {Wallet}: {SolAmt:F6} SOL → {TokenAmt:F4} tokens @ {Price:F12} | Balance: {Bal:F6} SOL",
            mint, GetWalletDisplayName(traderPublicKey), solAmount, tokenAmount, price, Wallet.SolBalance);

        // Send Telegram notification (only if above threshold)
        if (solAmount >= _telegramOptions.MinBuyAmountSolForNotification)
        {
            var walletDisplay = GetWalletDisplayName(traderPublicKey);
            _ = _telegramNotifier.NotifyBuyAsync(mint, walletDisplay, solAmount, tokenAmount, price, slippageBps, vSolInBondingCurve);
        }
    }

    /// <summary>
    /// Record a sell: add SOL to wallet, reduce/close position, compute realized PnL.
    /// Returns the realized PnL for this sell.
    /// </summary>
    public decimal RecordSell(string mint, decimal solAmount, decimal tokenAmount, decimal price, DateTimeOffset timestamp, string traderPublicKey, decimal vSolInBondingCurve)
    {
        Wallet.TotalTradeCount++;

        if (!Wallet.Positions.TryGetValue(mint, out var position) || position.TokenBalance <= 0)
        {
            _logger.LogWarning("No open position for {Mint} — skipping sell", mint);
            return 0;
        }

        // Capture market cap data for notification
        var vSolAtBuy = position.VSolInBondingCurveAtOpen;

        // Clamp to actual position size
        var actualTokensSold = Math.Min(tokenAmount, position.TokenBalance);
        var proportionSold = actualTokensSold / position.TokenBalance;

        // Cost basis for the portion being sold
        var costBasisSold = position.TotalCostBasis * proportionSold;

        // Actual SOL received (proportional if we clamped)
        var actualSolReceived = tokenAmount > 0
            ? solAmount * (actualTokensSold / tokenAmount)
            : 0;

        var realizedPnl = actualSolReceived - costBasisSold;

        // Update wallet
        Wallet.SolBalance += actualSolReceived;

        // Update position
        position.TokenBalance -= actualTokensSold;
        position.TotalCostBasis -= costBasisSold;
        position.SellCount++;

        // Track wins/losses
        Wallet.TotalRealizedPnl += realizedPnl;
        if (realizedPnl > 0)
        {
            Wallet.WinCount++;
            var roi = costBasisSold > 0 ? (realizedPnl / costBasisSold) * 100m : 0;
            Wallet.CumulativeRoiPercent += roi;
        }
        else
        {
            Wallet.LossCount++;
            var roi = costBasisSold > 0 ? (realizedPnl / costBasisSold) * 100m : 0;
            Wallet.CumulativeRoiPercent += roi;
        }

        // Remove fully closed positions
        if (position.IsClosed)
        {
            Wallet.Positions.Remove(mint);
        }

        _logger.LogInformation("SELL {Mint} by {Wallet}: {TokenAmt:F4} tokens → {SolAmt:F6} SOL @ {Price:F12} | PnL: {Pnl:+0.000000;-0.000000;0.000000} SOL | Balance: {Bal:F6} SOL",
            mint, GetWalletDisplayName(traderPublicKey), actualTokensSold, actualSolReceived, price, realizedPnl, Wallet.SolBalance);

        // Send Telegram notification
        var walletDisplay = GetWalletDisplayName(traderPublicKey);
        _ = _telegramNotifier.NotifySellAsync(
            mint,
            walletDisplay,
            actualTokensSold,
            actualSolReceived,
            realizedPnl,
            vSolAtBuy,
            vSolInBondingCurve);

        return realizedPnl;
    }

    /// <summary>
    /// Calculate total unrealized PnL across all open positions using current prices.
    /// </summary>
    public decimal CalculateUnrealizedPnl(Func<string, decimal> currentPriceResolver)
    {
        var totalUnrealized = 0m;

        foreach (var (mint, position) in Wallet.Positions)
        {
            if (position.TokenBalance <= 0) continue;

            var currentPrice = currentPriceResolver(mint);
            var marketValue = position.TokenBalance * currentPrice;
            totalUnrealized += marketValue - position.TotalCostBasis;
        }

        return totalUnrealized;
    }

    /// <summary>
    /// Total portfolio value = SOL balance + mark-to-market value of all positions.
    /// </summary>
    public decimal GetTotalPortfolioValue(Func<string, decimal> currentPriceResolver)
    {
        var positionValue = 0m;

        foreach (var (mint, position) in Wallet.Positions)
        {
            if (position.TokenBalance <= 0) continue;

            var currentPrice = currentPriceResolver(mint);
            positionValue += position.TokenBalance * currentPrice;
        }

        return Wallet.SolBalance + positionValue;
    }

    /// <summary>
    /// Update the high-water mark and max drawdown based on current portfolio value.
    /// Call this after every trade or periodically.
    /// </summary>
    public void UpdateDrawdown(decimal currentPortfolioValue)
    {
        if (currentPortfolioValue > Wallet.HighWaterMark)
        {
            Wallet.HighWaterMark = currentPortfolioValue;
        }

        if (Wallet.HighWaterMark > 0)
        {
            var drawdown = (Wallet.HighWaterMark - currentPortfolioValue) / Wallet.HighWaterMark * 100m;
            if (drawdown > Wallet.MaxDrawdownPercent)
            {
                Wallet.MaxDrawdownPercent = drawdown;
            }
        }
    }

    private string GetWalletDisplayName(string walletAddress)
    {
        if (_walletOptions.WalletAliases.TryGetValue(walletAddress, out var alias))
            return $"{alias} ({walletAddress[..8]}...)";
        
        return $"{walletAddress[..8]}...";
    }
}
