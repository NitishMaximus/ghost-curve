using GhostCurve.Domain.Interfaces;

namespace GhostCurve.Simulation.Pricing;

/// <summary>
/// Computes token prices using Pump.fun's constant-product (x * y = k) bonding curve math.
/// All calculations use decimal for financial precision — no floating point.
/// </summary>
public sealed class BondingCurvePriceResolver : IPriceResolver
{
    /// <summary>
    /// Spot price = vSol / vTokens (SOL per token).
    /// </summary>
    public decimal GetSpotPrice(decimal vTokensInBondingCurve, decimal vSolInBondingCurve)
    {
        if (vTokensInBondingCurve <= 0)
            throw new ArgumentException("vTokens must be positive", nameof(vTokensInBondingCurve));

        return vSolInBondingCurve / vTokensInBondingCurve;
    }

    /// <summary>
    /// Calculate tokens received for a given SOL input using constant-product formula.
    /// tokensOut = vTokens - (k / (vSol + solIn))
    /// where k = vSol * vTokens
    /// </summary>
    public decimal CalculateTokensOut(decimal solIn, decimal vTokensInBondingCurve, decimal vSolInBondingCurve)
    {
        if (solIn <= 0)
            throw new ArgumentException("SOL input must be positive", nameof(solIn));
        if (vTokensInBondingCurve <= 0)
            throw new ArgumentException("vTokens must be positive", nameof(vTokensInBondingCurve));
        if (vSolInBondingCurve <= 0)
            throw new ArgumentException("vSol must be positive", nameof(vSolInBondingCurve));

        var k = vSolInBondingCurve * vTokensInBondingCurve;
        var newVSol = vSolInBondingCurve + solIn;
        var newVTokens = k / newVSol;
        var tokensOut = vTokensInBondingCurve - newVTokens;

        return tokensOut > 0 ? tokensOut : 0;
    }

    /// <summary>
    /// Calculate SOL received for selling tokens using constant-product formula.
    /// solOut = vSol - (k / (vTokens + tokensIn))
    /// where k = vSol * vTokens
    /// </summary>
    public decimal CalculateSolOut(decimal tokensIn, decimal vTokensInBondingCurve, decimal vSolInBondingCurve)
    {
        if (tokensIn <= 0)
            throw new ArgumentException("Token input must be positive", nameof(tokensIn));
        if (vTokensInBondingCurve <= 0)
            throw new ArgumentException("vTokens must be positive", nameof(vTokensInBondingCurve));
        if (vSolInBondingCurve <= 0)
            throw new ArgumentException("vSol must be positive", nameof(vSolInBondingCurve));

        var k = vSolInBondingCurve * vTokensInBondingCurve;
        var newVTokens = vTokensInBondingCurve + tokensIn;
        var newVSol = k / newVTokens;
        var solOut = vSolInBondingCurve - newVSol;

        return solOut > 0 ? solOut : 0;
    }
}
