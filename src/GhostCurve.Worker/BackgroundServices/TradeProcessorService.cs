using System.Text.Json;
using System.Threading.Channels;
using GhostCurve.Configuration;
using GhostCurve.Domain.Enums;
using GhostCurve.Domain.Interfaces;
using GhostCurve.Domain.Models;
using GhostCurve.Simulation.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhostCurve.Worker.BackgroundServices;

/// <summary>
/// Core processing service — reads trade events from the channel, evaluates them,
/// applies configurable delay, executes simulated copy trades, and updates the portfolio.
/// Single consumer — no concurrent mutation of portfolio state.
/// Handles both live and replay events identically (deterministic pipeline).
/// </summary>
public sealed class TradeProcessorService : BackgroundService
{
    private readonly ChannelReader<TradeEvent> _channelReader;
    private readonly ITradeExecutor _executor;
    private readonly IPortfolioManager _portfolio;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MetricsEngine _metricsEngine;
    private readonly SimulationOptions _simOptions;
    private readonly ILogger<TradeProcessorService> _logger;

    private Guid _sessionId;
    private DateTimeOffset _lastSnapshotTime;

    // Per-wallet rate limiting: wallet -> list of recent trade timestamps
    private readonly Dictionary<string, List<DateTimeOffset>> _walletTradeTimestamps = new(StringComparer.Ordinal);

    public TradeProcessorService(
        Channel<TradeEvent> channel,
        ITradeExecutor executor,
        IPortfolioManager portfolio,
        IServiceScopeFactory scopeFactory,
        MetricsEngine metricsEngine,
        IOptions<SimulationOptions> simOptions,
        ILogger<TradeProcessorService> logger)
    {
        _channelReader = channel.Reader;
        _executor = executor;
        _portfolio = portfolio;
        _scopeFactory = scopeFactory;
        _metricsEngine = metricsEngine;
        _simOptions = simOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Trade processor starting");

        // Initialize session
        _sessionId = Guid.NewGuid();
        _portfolio.Reset(_simOptions.InitialSolBalance);
        _metricsEngine.Reset();
        _lastSnapshotTime = DateTimeOffset.UtcNow;

        var session = new SimulationSession
        {
            Id = _sessionId,
            StartedAtUtc = DateTimeOffset.UtcNow,
            Mode = SimulationMode.Live, // Updated to Replay by ReplayService if applicable
            ConfigJson = JsonSerializer.Serialize(_simOptions),
            InitialSolBalance = _simOptions.InitialSolBalance
        };

        using (var scope = _scopeFactory.CreateScope())
        {
            var tradeStore = scope.ServiceProvider.GetRequiredService<ISimulatedTradeStore>();
            await tradeStore.InsertSessionAsync(session, stoppingToken);
        }
        _logger.LogInformation("Simulation session {SessionId} started with {Balance} SOL",
            _sessionId, _simOptions.InitialSolBalance);

        try
        {
            await foreach (var tradeEvent in _channelReader.ReadAllAsync(stoppingToken))
            {
                await ProcessTradeEventAsync(tradeEvent, stoppingToken);

                // Periodic snapshots
                if (DateTimeOffset.UtcNow - _lastSnapshotTime > TimeSpan.FromSeconds(_simOptions.SnapshotIntervalSeconds))
                {
                    await TakeAndPersistSnapshotAsync(stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Clean shutdown
        }

        // Final snapshot and session close
        await TakeAndPersistSnapshotAsync(CancellationToken.None);
        session.EndedAtUtc = DateTimeOffset.UtcNow;
        session.FinalSolBalance = _portfolio.Wallet.SolBalance;
        using (var scope = _scopeFactory.CreateScope())
        {
            var tradeStore = scope.ServiceProvider.GetRequiredService<ISimulatedTradeStore>();
            await tradeStore.UpdateSessionAsync(session, CancellationToken.None);
        }

        _metricsEngine.LogCurrentMetrics();
        _logger.LogInformation("Trade processor stopped. Session {SessionId} complete.", _sessionId);
    }

    public void SetSessionMode(SimulationMode mode)
    {
        // Called by ReplayService before processing begins
        _logger.LogInformation("Session mode set to {Mode}", mode);
    }

    private async Task ProcessTradeEventAsync(TradeEvent tradeEvent, CancellationToken ct)
    {
        // Always update cached bonding curve state for metrics
        _metricsEngine.UpdateCurveState(tradeEvent.Mint, tradeEvent.VTokensInBondingCurve, tradeEvent.VSolInBondingCurve);

        // Filter: skip migrated tokens if configured
        if (_simOptions.SkipMigratedTokens && tradeEvent.Pool is not null && tradeEvent.Pool != "pump")
        {
            _logger.LogDebug("Skipping migrated token {Mint} (pool: {Pool})", tradeEvent.Mint, tradeEvent.Pool);
            return;
        }

        // Rate limiting per wallet
        if (!CheckRateLimit(tradeEvent.TraderPublicKey))
        {
            _logger.LogDebug("Rate limit exceeded for wallet {Wallet}, skipping trade", tradeEvent.TraderPublicKey[..8]);
            return;
        }

        // Apply execution delay (real delay in live mode, skip in replay)
        if (tradeEvent.Source == TradeSource.Live && _simOptions.ExecutionDelayMs > 0)
        {
            await Task.Delay(_simOptions.ExecutionDelayMs, ct);
        }

        // Determine trade intent
        TradeIntent intent;
        if (tradeEvent.TxType == TradeType.Buy)
        {
            // Check if we have enough SOL
            if (_portfolio.Wallet.SolBalance < _simOptions.PositionSizeSol)
            {
                _logger.LogDebug("Insufficient SOL for copy buy on {Mint}", tradeEvent.Mint);
                return;
            }

            intent = new TradeIntent(
                Mint: tradeEvent.Mint,
                Side: TradeType.Buy,
                SolAmount: _simOptions.PositionSizeSol,
                MaxSlippageBps: _simOptions.MaxSlippageBps,
                VTokensInBondingCurve: tradeEvent.VTokensInBondingCurve,
                VSolInBondingCurve: tradeEvent.VSolInBondingCurve,
                SourceTradeEventId: tradeEvent.Id,
                DelayMs: _simOptions.ExecutionDelayMs);
        }
        else
        {
            // Sell — sell our entire position in this token
            if (!_portfolio.Wallet.Positions.TryGetValue(tradeEvent.Mint, out var position) || position.IsClosed)
            {
                _logger.LogDebug("No position to sell for {Mint}", tradeEvent.Mint);
                return;
            }

            intent = new TradeIntent(
                Mint: tradeEvent.Mint,
                Side: TradeType.Sell,
                SolAmount: position.TokenBalance, // For sells, pass token amount in SolAmount field
                MaxSlippageBps: _simOptions.MaxSlippageBps,
                VTokensInBondingCurve: tradeEvent.VTokensInBondingCurve,
                VSolInBondingCurve: tradeEvent.VSolInBondingCurve,
                SourceTradeEventId: tradeEvent.Id,
                DelayMs: _simOptions.ExecutionDelayMs);
        }

        // Execute via the simulation (or future live) engine
        var result = await _executor.ExecuteAsync(intent, ct);

        if (!result.Success)
        {
            _logger.LogWarning("Trade execution failed for {Mint}: {Reason}", tradeEvent.Mint, result.ErrorReason);
            return;
        }

        // Update portfolio
        decimal? realizedPnl = null;
        if (intent.Side == TradeType.Buy)
        {
            _portfolio.RecordBuy(intent.Mint, result.ActualSolAmount, result.ActualTokenAmount, result.EffectivePrice, tradeEvent.ReceivedAtUtc, tradeEvent.TraderPublicKey);
        }
        else
        {
            realizedPnl = _portfolio.RecordSell(intent.Mint, result.ActualSolAmount, result.ActualTokenAmount, result.EffectivePrice, tradeEvent.ReceivedAtUtc, tradeEvent.TraderPublicKey);
        }

        // Update drawdown tracking
        var portfolioValue = _portfolio.GetTotalPortfolioValue(_metricsEngine.ResolveCurrentPrice);
        ((Simulation.Portfolio.PortfolioManager)_portfolio).UpdateDrawdown(portfolioValue);

        // Persist simulated trade
        var simulatedTrade = new SimulatedTrade
        {
            SourceTradeEventId = tradeEvent.Id,
            SessionId = _sessionId,
            Mint = intent.Mint,
            Side = intent.Side,
            SolAmount = result.ActualSolAmount,
            TokenAmount = result.ActualTokenAmount,
            SimulatedPrice = result.EffectivePrice,
            SlippageBps = result.SlippageBps,
            DelayMs = intent.DelayMs,
            ExecutedAtUtc = DateTimeOffset.UtcNow,
            VTokensAtExecution = tradeEvent.VTokensInBondingCurve,
            VSolAtExecution = tradeEvent.VSolInBondingCurve,
            RealizedPnl = realizedPnl
        };

        using (var scope = _scopeFactory.CreateScope())
        {
            var tradeStore = scope.ServiceProvider.GetRequiredService<ISimulatedTradeStore>();
            await tradeStore.InsertAsync(simulatedTrade, ct);
        }
    }

    private bool CheckRateLimit(string walletAddress)
    {
        var now = DateTimeOffset.UtcNow;
        var oneMinuteAgo = now.AddMinutes(-1);

        if (!_walletTradeTimestamps.TryGetValue(walletAddress, out var timestamps))
        {
            timestamps = [];
            _walletTradeTimestamps[walletAddress] = timestamps;
        }

        // Remove timestamps older than 1 minute
        timestamps.RemoveAll(t => t < oneMinuteAgo);

        if (timestamps.Count >= _simOptions.MaxTradesPerWalletPerMinute)
            return false;

        timestamps.Add(now);
        return true;
    }

    private async Task TakeAndPersistSnapshotAsync(CancellationToken ct)
    {
        try
        {
            var snapshot = _metricsEngine.TakeSnapshot(_sessionId);
            using var scope = _scopeFactory.CreateScope();
            var tradeStore = scope.ServiceProvider.GetRequiredService<ISimulatedTradeStore>();
            await tradeStore.InsertSnapshotAsync(snapshot, ct);
            _lastSnapshotTime = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist performance snapshot");
        }
    }
}
