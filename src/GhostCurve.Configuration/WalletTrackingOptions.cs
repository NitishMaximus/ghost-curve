namespace GhostCurve.Configuration;

/// <summary>
/// Configuration for which wallets to track and copy trade.
/// </summary>
public sealed class WalletTrackingOptions
{
    public const string SectionName = "WalletTracking";

    /// <summary>List of Solana wallet addresses to subscribe to via PumpPortal.</summary>
    public List<string> TrackedWallets { get; set; } = [];

    /// <summary>
    /// Optional friendly names for wallets, keyed by wallet address.
    /// Used for logging and reporting only.
    /// </summary>
    public Dictionary<string, string> WalletAliases { get; set; } = new(StringComparer.Ordinal);
}
