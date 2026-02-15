using GhostCurve.Configuration;
using Microsoft.Extensions.Options;

namespace GhostCurve.Simulation.Execution;

/// <summary>
/// Deterministic slippage model for simulation.
/// Computes total slippage = base slippage + price impact proportional to trade size vs. curve liquidity.
/// No randomness — fully reproducible for replay.
/// </summary>
public sealed class SlippageModel
{
    private readonly SimulationOptions _options;

    public SlippageModel(IOptions<SimulationOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Calculate total slippage in basis points for a trade.
    /// </summary>
    /// <param name="solAmount">SOL amount being traded.</param>
    /// <param name="vSolInBondingCurve">Current virtual SOL reserves in the bonding curve.</param>
    /// <returns>Total slippage in basis points, capped at MaxSlippageBps.</returns>
    public decimal CalculateSlippageBps(decimal solAmount, decimal vSolInBondingCurve)
    {
        if (vSolInBondingCurve <= 0)
            return _options.BaseSlippageBps;

        // Impact increases proportionally to how large our trade is relative to pool liquidity
        var impactBps = (solAmount / vSolInBondingCurve) * _options.PriceImpactFactor * 10_000m;
        var totalBps = _options.BaseSlippageBps + impactBps;

        return Math.Min(totalBps, _options.MaxSlippageBps);
    }

    /// <summary>
    /// Apply slippage reduction to a token amount (for buys — you receive fewer tokens).
    /// </summary>
    public decimal ApplySlippageToTokens(decimal tokensOut, decimal slippageBps)
    {
        var slippageFactor = 1m - (slippageBps / 10_000m);
        return tokensOut * slippageFactor;
    }

    /// <summary>
    /// Apply slippage reduction to a SOL amount (for sells — you receive less SOL).
    /// </summary>
    public decimal ApplySlippageToSol(decimal solOut, decimal slippageBps)
    {
        var slippageFactor = 1m - (slippageBps / 10_000m);
        return solOut * slippageFactor;
    }

    /// <summary>
    /// Check if the total slippage exceeds the configured maximum.
    /// </summary>
    public bool ExceedsMaxSlippage(decimal slippageBps)
    {
        return slippageBps > _options.MaxSlippageBps;
    }
}
