namespace GhostCurve.Configuration;

/// <summary>
/// Configuration for historical replay mode.
/// When enabled, the worker replays stored trade events instead of connecting to live WebSocket.
/// </summary>
public sealed class ReplayOptions
{
    public const string SectionName = "Replay";

    /// <summary>Whether replay mode is active.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Start of the replay time window (UTC).</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>End of the replay time window (UTC).</summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>
    /// Optional: filter replay to specific trader wallets.
    /// If empty, replays all stored events in the time range.
    /// </summary>
    public List<string> FilterWallets { get; set; } = [];

    /// <summary>Batch size for cursor-based pagination during replay.</summary>
    public int BatchSize { get; set; } = 500;
}
