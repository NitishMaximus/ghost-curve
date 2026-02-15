namespace GhostCurve.Configuration;

/// <summary>
/// Configuration for the PumpPortal WebSocket connection.
/// </summary>
public sealed class WebSocketOptions
{
    public const string SectionName = "WebSocket";

    /// <summary>PumpPortal WebSocket endpoint.</summary>
    public string Url { get; set; } = "wss://pumpportal.fun/api/data";

    /// <summary>Initial reconnect delay in milliseconds.</summary>
    public int ReconnectBaseDelayMs { get; set; } = 1_000;

    /// <summary>Maximum reconnect delay in milliseconds (exponential backoff cap).</summary>
    public int ReconnectMaxDelayMs { get; set; } = 30_000;

    /// <summary>
    /// Jitter factor applied to reconnect delay (0.0–1.0).
    /// Actual delay = baseDelay * (1 + random(0, jitter)).
    /// </summary>
    public double ReconnectJitterFactor { get; set; } = 0.2;

    /// <summary>WebSocket receive buffer size in bytes.</summary>
    public int ReceiveBufferSize { get; set; } = 8192;

    /// <summary>Size of the in-memory dedup ring buffer for recent signatures.</summary>
    public int DedupBufferSize { get; set; } = 10_000;
}
