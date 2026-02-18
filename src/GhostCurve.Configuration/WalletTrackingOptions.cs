namespace GhostCurve.Configuration;

/// <summary>
/// Configuration for which wallets to track and copy trade.
/// </summary>
public sealed class WalletTrackingOptions
{
    public const string SectionName = "WalletTracking";

    /// <summary>
    /// Dictionary of Solana wallet addresses to track with their friendly display names.
    /// Key: wallet address, Value: alias/display name.
    /// The wallet addresses (keys) are automatically subscribed via PumpPortal.
    /// </summary>
    public Dictionary<string, string> WalletAliases { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the list of wallet addresses to track (derived from WalletAliases keys).
    /// </summary>
    public List<string> TrackedWallets => WalletAliases.Keys.ToList();
}
