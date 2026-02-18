using GhostCurve.Configuration;
using GhostCurve.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace GhostCurve.Infrastructure.Notifications;

/// <summary>
/// Sends trade notifications to Telegram using HTML-formatted messages.
/// </summary>
public sealed class TelegramNotifier : ITelegramNotifier
{
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramNotifier> _logger;
    private readonly TelegramBotClient? _botClient;

    public TelegramNotifier(IOptions<TelegramOptions> options, ILogger<TelegramNotifier> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (_options.Enabled && !string.IsNullOrWhiteSpace(_options.BotToken))
        {
            _botClient = new TelegramBotClient(_options.BotToken);
            _logger.LogInformation("Telegram notifier initialized");
        }
        else
        {
            _logger.LogInformation("Telegram notifications disabled");
        }
    }

    public async Task NotifyBuyAsync(
        string mint,
        string traderWallet,
        decimal solAmount,
        decimal tokenAmount,
        decimal price,
        decimal slippageBps,
        decimal vSolInBondingCurve,
        CancellationToken cancellationToken = default)
    {
        if (_botClient is null || !_options.Enabled)
            return;

        try
        {
            var message = FormatBuyMessage(mint, traderWallet, solAmount, tokenAmount, price, slippageBps, vSolInBondingCurve);

            await _botClient.SendMessage(
                chatId: _options.ChatId,
                text: message,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);

            _logger.LogDebug("Sent buy notification for {Mint}", mint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram buy notification for {Mint}", mint);
        }
    }

    public async Task NotifySellAsync(
        string mint,
        string traderWallet,
        decimal tokenAmount,
        decimal solReceived,
        decimal realizedPnl,
        decimal vSolInBondingCurveAtBuy,
        decimal vSolInBondingCurveAtSell,
        CancellationToken cancellationToken = default)
    {
        if (_botClient is null || !_options.Enabled)
            return;

        try
        {
            var mcapMultiplier = vSolInBondingCurveAtBuy > 0
                ? vSolInBondingCurveAtSell / vSolInBondingCurveAtBuy
                : 0m;

            var message = FormatSellMessage(
                mint,
                traderWallet,
                tokenAmount,
                solReceived,
                realizedPnl,
                mcapMultiplier);

            await _botClient.SendMessage(
                chatId: _options.ChatId,
                text: message,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);

            _logger.LogDebug("Sent sell notification for {Mint}", mint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram sell notification for {Mint}", mint);
        }
    }

    private static string FormatBuyMessage(
        string mint,
        string traderWallet,
        decimal solAmount,
        decimal tokenAmount,
        decimal price,
        decimal slippageBps,
        decimal vSolInBondingCurve)
    {
        var solscanLink = $"https://solscan.io/token/{mint}";
        var pumpLink = $"https://pump.fun/{mint}";

        return $"""
               🟢 <b>BUY SIGNAL</b>
               
               <b>Token:</b> <a href="{solscanLink}">{TruncateMint(mint)}</a>
               <b>Trader:</b> <code>{traderWallet}</code>
               
               💰 <b>Trade Details</b>
               • Spent: <b>{solAmount:F6} SOL</b>
               • Received: <b>{FormatTokenAmount(tokenAmount)} tokens</b>
               • Price: <b>{price:F9} SOL</b>
               • Slippage: <b>{slippageBps / 100:F2}%</b>
               
               📊 <b>Market Cap</b>
               • Bonding Curve TVL: <b>{vSolInBondingCurve:F2} SOL</b>
               
               🔗 <a href="{pumpLink}">View on Pump.fun</a>
               """;
    }

    private static string FormatSellMessage(
        string mint,
        string traderWallet,
        decimal tokenAmount,
        decimal solReceived,
        decimal realizedPnl,
        decimal mcapMultiplier)
    {
        var solscanLink = $"https://solscan.io/token/{mint}";
        var pnlEmoji = realizedPnl >= 0 ? "✅" : "❌";
        var pnlSign = realizedPnl >= 0 ? "+" : "";
        var mcapDirection = mcapMultiplier >= 1 ? "📈" : "📉";

        return $"""
               🔴 <b>SELL SIGNAL</b>
               
               <b>Token:</b> <a href="{solscanLink}">{TruncateMint(mint)}</a>
               <b>Trader:</b> <code>{traderWallet}</code>
               
               💰 <b>Trade Details</b>
               • Sold: <b>{FormatTokenAmount(tokenAmount)} tokens</b>
               • Received: <b>{solReceived:F6} SOL</b>
               
               {pnlEmoji} <b>Realized P&L: {pnlSign}{realizedPnl:F6} SOL ({GetPnlPercent(realizedPnl, solReceived):F2}%)</b>
               
               {mcapDirection} <b>Market Cap Change: {mcapMultiplier:F2}x</b>
               {GetMcapDescription(mcapMultiplier)}
               """;
    }

    private static string TruncateMint(string mint)
    {
        if (mint.Length <= 12)
            return mint;

        return $"{mint[..6]}...{mint[^4..]}";
    }

    private static string FormatTokenAmount(decimal amount)
    {
        return amount >= 1000 ? $"{amount / 1000:F2}K" : $"{amount:F2}";
    }

    private static decimal GetPnlPercent(decimal pnl, decimal solReceived)
    {
        if (solReceived == 0)
            return 0;

        var costBasis = solReceived - pnl;
        return costBasis != 0 ? (pnl / costBasis) * 100 : 0;
    }

    private static string GetMcapDescription(decimal multiplier)
    {
        return multiplier switch
        {
            >= 5.0m => "🚀 Sold at 5x+ market cap!",
            >= 3.0m => "🔥 Sold at 3x+ market cap",
            >= 2.0m => "💪 Sold at 2x+ market cap",
            >= 1.5m => "📊 Sold at 1.5x+ market cap",
            >= 1.0m => "➡️ Market cap stable",
            >= 0.5m => "⚠️ Sold at lower market cap (-50%)",
            _ => "🔻 Sold at significantly lower market cap"
        };
    }
}
