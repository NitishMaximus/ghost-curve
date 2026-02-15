using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using GhostCurve.Configuration;
using GhostCurve.Domain.Enums;
using GhostCurve.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhostCurve.Infrastructure.WebSocket;

/// <summary>
/// WebSocket client for PumpPortal real-time trade data feed.
/// Handles connection, subscription, reconnection with exponential backoff,
/// and mapping raw messages to domain TradeEvent objects.
/// </summary>
public sealed class PumpPortalClient : IDisposable
{
    private readonly WebSocketOptions _options;
    private readonly WalletTrackingOptions _walletOptions;
    private readonly ILogger<PumpPortalClient> _logger;

    private ClientWebSocket? _ws;
    private int _reconnectAttempt;

    // Ring buffer for in-memory signature deduplication
    private readonly string[] _dedupBuffer;
    private int _dedupIndex;

    public PumpPortalClient(
        IOptions<WebSocketOptions> options,
        IOptions<WalletTrackingOptions> walletOptions,
        ILogger<PumpPortalClient> logger)
    {
        _options = options.Value;
        _walletOptions = walletOptions.Value;
        _logger = logger;
        _dedupBuffer = new string[_options.DedupBufferSize];
    }

    /// <summary>
    /// Connect to PumpPortal WebSocket and subscribe to all tracked wallets.
    /// </summary>
    public async Task ConnectAndSubscribeAsync(CancellationToken ct)
    {
        _ws?.Dispose();
        _ws = new ClientWebSocket();
        _ws.Options.SetBuffer(_options.ReceiveBufferSize, _options.ReceiveBufferSize);

        _logger.LogInformation("Connecting to PumpPortal WebSocket at {Url}", _options.Url);
        await _ws.ConnectAsync(new Uri(_options.Url), ct);
        _logger.LogInformation("Connected to PumpPortal WebSocket");

        _reconnectAttempt = 0;

        // Subscribe to all tracked wallets in a single message (per PumpPortal docs)
        if (_walletOptions.TrackedWallets.Count > 0)
        {
            var subscribePayload = JsonSerializer.Serialize(new
            {
                method = "subscribeAccountTrade",
                keys = _walletOptions.TrackedWallets
            });

            var bytes = Encoding.UTF8.GetBytes(subscribePayload);
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);

            _logger.LogInformation("Subscribed to {Count} wallet(s): {Wallets}",
                _walletOptions.TrackedWallets.Count,
                string.Join(", ", _walletOptions.TrackedWallets.Select(w =>
                    _walletOptions.WalletAliases.TryGetValue(w, out var alias)
                        ? $"{alias} ({w[..8]}...)"
                        : $"{w[..8]}...")));
        }
    }

    /// <summary>
    /// Receive and deserialize the next trade event from the WebSocket.
    /// Returns null if the message is invalid, a duplicate, or the connection closes cleanly.
    /// Throws on connection errors (caller should handle reconnection).
    /// </summary>
    public async Task<TradeEvent?> ReceiveTradeEventAsync(CancellationToken ct)
    {
        if (_ws is null || _ws.State != WebSocketState.Open)
            throw new InvalidOperationException("WebSocket is not connected");

        var buffer = ArrayPool<byte>.Shared.Rent(_options.ReceiveBufferSize);
        try
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await _ws.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogWarning("WebSocket received close frame: {Status} {Description}",
                        result.CloseStatus, result.CloseStatusDescription);
                    return null;
                }

                ms.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            var receivedAt = DateTimeOffset.UtcNow;
            var json = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);

            PumpPortalMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<PumpPortalMessage>(json);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize PumpPortal message: {Json}",
                    json.Length > 500 ? json[..500] : json);
                return null;
            }

            if (message is null || !message.IsValid)
            {
                _logger.LogDebug("Received invalid or empty PumpPortal message, skipping");
                return null;
            }

            // In-memory dedup check
            if (IsDuplicate(message.Signature!))
            {
                _logger.LogDebug("Duplicate signature {Signature}, skipping", message.Signature![..16]);
                return null;
            }

            AddToDedup(message.Signature!);

            return MapToTradeEvent(message, receivedAt);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Calculate reconnect delay with exponential backoff and jitter.
    /// </summary>
    public TimeSpan GetReconnectDelay()
    {
        var baseDelay = Math.Min(
            _options.ReconnectBaseDelayMs * (1 << Math.Min(_reconnectAttempt, 10)),
            _options.ReconnectMaxDelayMs);

        var jitter = baseDelay * _options.ReconnectJitterFactor * Random.Shared.NextDouble();
        _reconnectAttempt++;

        return TimeSpan.FromMilliseconds(baseDelay + jitter);
    }

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    private bool IsDuplicate(string signature)
    {
        for (var i = 0; i < _dedupBuffer.Length; i++)
        {
            if (string.Equals(_dedupBuffer[i], signature, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private void AddToDedup(string signature)
    {
        _dedupBuffer[_dedupIndex % _dedupBuffer.Length] = signature;
        _dedupIndex++;
    }

    private static TradeEvent MapToTradeEvent(PumpPortalMessage msg, DateTimeOffset receivedAt)
    {
        return new TradeEvent
        {
            Signature = msg.Signature!,
            Mint = msg.Mint!,
            TraderPublicKey = msg.TraderPublicKey!,
            TxType = msg.TxType!.Equals("buy", StringComparison.OrdinalIgnoreCase)
                ? TradeType.Buy
                : TradeType.Sell,
            TokenAmount = msg.TokenAmount,
            SolAmount = msg.SolAmount,
            NewTokenBalance = msg.NewTokenBalance,
            BondingCurveKey = msg.BondingCurveKey!,
            VTokensInBondingCurve = msg.VTokensInBondingCurve,
            VSolInBondingCurve = msg.VSolInBondingCurve,
            MarketCapSol = msg.MarketCapSol,
            Pool = msg.Pool,
            ReceivedAtUtc = receivedAt,
            Source = TradeSource.Live
        };
    }

    public void Dispose()
    {
        _ws?.Dispose();
    }
}
