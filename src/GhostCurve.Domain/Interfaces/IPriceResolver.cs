namespace GhostCurve.Domain.Interfaces;

/// <summary>
/// Resolves token price from bonding curve reserves using constant-product AMM math.
/// </summary>
public interface IPriceResolver
{
    /// <summary>
    /// Get the current spot price (SOL per token) from bonding curve reserves.
    /// Price = vSol / vTokens
    /// </summary>
    decimal GetSpotPrice(decimal vTokensInBondingCurve, decimal vSolInBondingCurve);

    /// <summary>
    /// Calculate how many tokens you receive for a given SOL input.
    /// Uses constant-product formula: tokensOut = vTokens - (k / (vSol + solIn))
    /// </summary>
    decimal CalculateTokensOut(decimal solIn, decimal vTokensInBondingCurve, decimal vSolInBondingCurve);

    /// <summary>
    /// Calculate how much SOL you receive for selling a given amount of tokens.
    /// Uses constant-product formula: solOut = vSol - (k / (vTokens + tokensIn))
    /// </summary>
    decimal CalculateSolOut(decimal tokensIn, decimal vTokensInBondingCurve, decimal vSolInBondingCurve);
}
