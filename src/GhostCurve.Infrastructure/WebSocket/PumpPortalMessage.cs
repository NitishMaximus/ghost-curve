using System.Text.Json.Serialization;

namespace GhostCurve.Infrastructure.WebSocket;

/// <summary>
/// Raw DTO matching the PumpPortal WebSocket trade event payload.
/// Deserialized directly from JSON — mapped to domain TradeEvent downstream.
/// </summary>
public sealed class PumpPortalMessage
{
    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    [JsonPropertyName("mint")]
    public string? Mint { get; set; }

    [JsonPropertyName("traderPublicKey")]
    public string? TraderPublicKey { get; set; }

    [JsonPropertyName("txType")]
    public string? TxType { get; set; }

    [JsonPropertyName("tokenAmount")]
    public decimal TokenAmount { get; set; }

    [JsonPropertyName("solAmount")]
    public decimal SolAmount { get; set; }

    [JsonPropertyName("newTokenBalance")]
    public decimal NewTokenBalance { get; set; }

    [JsonPropertyName("bondingCurveKey")]
    public string? BondingCurveKey { get; set; }

    [JsonPropertyName("vTokensInBondingCurve")]
    public decimal VTokensInBondingCurve { get; set; }

    [JsonPropertyName("vSolInBondingCurve")]
    public decimal VSolInBondingCurve { get; set; }

    [JsonPropertyName("marketCapSol")]
    public decimal MarketCapSol { get; set; }

    [JsonPropertyName("pool")]
    public string? Pool { get; set; }

    /// <summary>
    /// Validates that all required fields are present and non-empty.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Signature) &&
        !string.IsNullOrWhiteSpace(Mint) &&
        !string.IsNullOrWhiteSpace(TraderPublicKey) &&
        !string.IsNullOrWhiteSpace(TxType) &&
        !string.IsNullOrWhiteSpace(BondingCurveKey);
}
