using GhostCurve.Domain.Enums;
using GhostCurve.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GhostCurve.Simulation.Execution;

/// <summary>
/// Phase 1 trade executor — computes trade outcomes entirely from bonding curve math.
/// No network calls, no external state — fully deterministic.
/// Implements ITradeExecutor so it can be swapped for JupiterTradeExecutor in Phase 2.
/// </summary>
public sealed class SimulationTradeExecutor : ITradeExecutor
{
    private readonly IPriceResolver _priceResolver;
    private readonly SlippageModel _slippageModel;
    private readonly ILogger<SimulationTradeExecutor> _logger;

    public SimulationTradeExecutor(
        IPriceResolver priceResolver,
        SlippageModel slippageModel,
        ILogger<SimulationTradeExecutor> logger)
    {
        _priceResolver = priceResolver;
        _slippageModel = slippageModel;
        _logger = logger;
    }

    public Task<TradeExecutionResult> ExecuteAsync(TradeIntent intent, CancellationToken ct)
    {
        var vTokens = intent.VTokensInBondingCurve;
        var vSol = intent.VSolInBondingCurve;

        // Calculate slippage
        var slippageBps = _slippageModel.CalculateSlippageBps(intent.SolAmount, vSol);

        if (_slippageModel.ExceedsMaxSlippage(slippageBps))
        {
            _logger.LogWarning("Trade rejected — slippage {Slippage} bps exceeds max {Max} bps for {Mint}",
                slippageBps, intent.MaxSlippageBps, intent.Mint);

            return Task.FromResult(new TradeExecutionResult(
                Success: false,
                ActualTokenAmount: 0,
                ActualSolAmount: 0,
                EffectivePrice: 0,
                SlippageBps: slippageBps,
                ErrorReason: $"Slippage {slippageBps:F1} bps exceeds maximum {intent.MaxSlippageBps:F1} bps"));
        }

        decimal actualTokenAmount;
        decimal actualSolAmount;
        decimal effectivePrice;

        if (intent.Side == TradeType.Buy)
        {
            // Calculate tokens received for our SOL input
            var rawTokensOut = _priceResolver.CalculateTokensOut(intent.SolAmount, vTokens, vSol);
            actualTokenAmount = _slippageModel.ApplySlippageToTokens(rawTokensOut, slippageBps);
            actualSolAmount = intent.SolAmount;
            effectivePrice = actualTokenAmount > 0 ? actualSolAmount / actualTokenAmount : 0;
        }
        else
        {
            // For sells, the SOL amount in the intent is the token amount to sell
            // (since we size by tokens on sells, using our position)
            var tokensToSell = intent.SolAmount; // Reusing field — see TradeProcessorService
            var rawSolOut = _priceResolver.CalculateSolOut(tokensToSell, vTokens, vSol);
            actualSolAmount = _slippageModel.ApplySlippageToSol(rawSolOut, slippageBps);
            actualTokenAmount = tokensToSell;
            effectivePrice = actualTokenAmount > 0 ? actualSolAmount / actualTokenAmount : 0;
        }

        _logger.LogDebug("Simulated {Side} on {Mint}: {SolAmt:F6} SOL ↔ {TokenAmt:F4} tokens @ {Price:F12} SOL/token, slippage {Slip:F1} bps",
            intent.Side, intent.Mint, actualSolAmount, actualTokenAmount, effectivePrice, slippageBps);

        return Task.FromResult(new TradeExecutionResult(
            Success: true,
            ActualTokenAmount: actualTokenAmount,
            ActualSolAmount: actualSolAmount,
            EffectivePrice: effectivePrice,
            SlippageBps: slippageBps));
    }
}
