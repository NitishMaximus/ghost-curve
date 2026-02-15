using GhostCurve.Domain.Interfaces;
using GhostCurve.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GhostCurve.Simulation.Metrics;

/// <summary>
/// Computes and produces performance snapshots from the current portfolio state.
/// Stateless — derives all metrics from the VirtualWallet at snapshot time.
/// </summary>
public sealed class MetricsEngine
{
    private readonly IPortfolioManager _portfolio;
    private readonly IPriceResolver _priceResolver;
    private readonly ILogger<MetricsEngine> _logger;

    // Cache of latest bonding curve state per mint — updated as trade events flow through
    private readonly Dictionary<string, (decimal VTokens, decimal VSol)> _latestCurveState = new(StringComparer.Ordinal);

    public MetricsEngine(
        IPortfolioManager portfolio,
        IPriceResolver priceResolver,
        ILogger<MetricsEngine> logger)
    {
        _portfolio = portfolio;
        _priceResolver = priceResolver;
        _logger = logger;
    }

    /// <summary>
    /// Update the cached bonding curve state for a token (called on every trade event).
    /// </summary>
    public void UpdateCurveState(string mint, decimal vTokens, decimal vSol)
    {
        _latestCurveState[mint] = (vTokens, vSol);
    }

    /// <summary>
    /// Resolve current price for a mint using cached bonding curve state.
    /// </summary>
    public decimal ResolveCurrentPrice(string mint)
    {
        if (_latestCurveState.TryGetValue(mint, out var state) && state.VTokens > 0)
        {
            return _priceResolver.GetSpotPrice(state.VTokens, state.VSol);
        }

        return 0m;
    }

    /// <summary>
    /// Take a performance snapshot of the current portfolio state.
    /// </summary>
    public PerformanceSnapshot TakeSnapshot(Guid sessionId)
    {
        var wallet = _portfolio.Wallet;
        var totalTrades = wallet.WinCount + wallet.LossCount;
        var winRate = totalTrades > 0 ? (decimal)wallet.WinCount / totalTrades * 100m : 0m;
        var avgRoi = totalTrades > 0 ? wallet.CumulativeRoiPercent / totalTrades : 0m;

        var unrealizedPnl = _portfolio.CalculateUnrealizedPnl(ResolveCurrentPrice);
        var portfolioValue = _portfolio.GetTotalPortfolioValue(ResolveCurrentPrice);

        var snapshot = new PerformanceSnapshot
        {
            SessionId = sessionId,
            SnapshotAtUtc = DateTimeOffset.UtcNow,
            TotalTrades = wallet.TotalTradeCount,
            WinCount = wallet.WinCount,
            LossCount = wallet.LossCount,
            WinRate = winRate,
            AvgRoiPercent = avgRoi,
            TotalRealizedPnl = wallet.TotalRealizedPnl,
            TotalUnrealizedPnl = unrealizedPnl,
            MaxDrawdownPercent = wallet.MaxDrawdownPercent,
            SolBalance = wallet.SolBalance,
            TotalPortfolioValue = portfolioValue
        };

        _logger.LogInformation(
            "📊 Snapshot | Trades: {Trades} | Win Rate: {WinRate:F1}% | Realized PnL: {RPnl:+0.000000;-0.000000;0.000000} SOL | Unrealized: {UPnl:+0.000000;-0.000000;0.000000} SOL | Portfolio: {Port:F6} SOL | Drawdown: {DD:F2}%",
            snapshot.TotalTrades,
            snapshot.WinRate,
            snapshot.TotalRealizedPnl,
            snapshot.TotalUnrealizedPnl,
            snapshot.TotalPortfolioValue,
            snapshot.MaxDrawdownPercent);

        return snapshot;
    }

    /// <summary>
    /// Log a summary of current metrics without persisting.
    /// </summary>
    public void LogCurrentMetrics()
    {
        var wallet = _portfolio.Wallet;
        var totalTrades = wallet.WinCount + wallet.LossCount;
        var winRate = totalTrades > 0 ? (decimal)wallet.WinCount / totalTrades * 100m : 0m;

        _logger.LogInformation(
            "Metrics | Balance: {Bal:F6} SOL | Positions: {Pos} | Wins: {W}/{Total} ({WR:F1}%) | PnL: {PnL:+0.000000;-0.000000;0.000000} SOL",
            wallet.SolBalance,
            wallet.Positions.Count,
            wallet.WinCount,
            totalTrades,
            winRate,
            wallet.TotalRealizedPnl);
    }

    /// <summary>
    /// Reset cached curve state (for new sessions).
    /// </summary>
    public void Reset()
    {
        _latestCurveState.Clear();
    }
}
