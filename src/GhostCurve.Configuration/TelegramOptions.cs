using System.ComponentModel.DataAnnotations;

namespace GhostCurve.Configuration;

/// <summary>
/// Configuration options for Telegram bot notifications.
/// </summary>
public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    /// <summary>
    /// Whether to enable Telegram notifications.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Telegram bot token obtained from @BotFather.
    /// </summary>
    [Required]
    public string BotToken { get; init; } = string.Empty;

    /// <summary>
    /// Telegram chat ID where notifications will be sent.
    /// Can be a user ID, group ID, or channel ID.
    /// </summary>
    [Required]
    public string ChatId { get; init; } = string.Empty;

    /// <summary>
    /// Whether to disable link previews in messages.
    /// </summary>
    public bool DisableWebPagePreview { get; init; } = true;

    /// <summary>
    /// Minimum SOL amount spent for a buy to trigger a notification.
    /// Set to 0 to notify on all buys. Example: 10 SOL ≈ $1500 (if SOL = $150).
    /// </summary>
    public decimal MinBuyAmountSolForNotification { get; init; } = 0m;
}
