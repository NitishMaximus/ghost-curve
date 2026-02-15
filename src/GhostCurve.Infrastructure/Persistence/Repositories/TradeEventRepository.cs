using System.Runtime.CompilerServices;
using GhostCurve.Domain.Enums;
using GhostCurve.Domain.Interfaces;
using GhostCurve.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace GhostCurve.Infrastructure.Persistence.Repositories;

/// <summary>
/// High-performance trade event persistence using raw Npgsql for inserts (COPY protocol)
/// and EF Core for reads. Append-only — events are never updated or deleted.
/// </summary>
public sealed class TradeEventRepository : ITradeEventStore
{
    private readonly GhostCurveDbContext _db;
    private readonly ILogger<TradeEventRepository> _logger;

    public TradeEventRepository(GhostCurveDbContext db, ILogger<TradeEventRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Bulk insert trade events using Npgsql binary COPY for maximum throughput.
    /// Duplicates (by signature) are skipped via a temp table + INSERT ON CONFLICT approach.
    /// </summary>
    public async Task<int> InsertBatchAsync(IReadOnlyList<TradeEvent> events, CancellationToken ct)
    {
        if (events.Count == 0)
            return 0;

        var conn = _db.Database.GetDbConnection() as NpgsqlConnection
            ?? throw new InvalidOperationException("Expected NpgsqlConnection");

        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        // Use a temp table to handle ON CONFLICT for bulk inserts
        var tempTable = $"tmp_trade_events_{Guid.NewGuid():N}";
        
        try
        {
            // Create temp table (exclude id column since it's auto-generated)
            await using var cmdCreate = new NpgsqlCommand($"""
                CREATE TEMP TABLE {tempTable} (
                    signature text NOT NULL,
                    mint text NOT NULL,
                    trader_public_key text NOT NULL,
                    tx_type smallint NOT NULL,
                    token_amount numeric(28,12) NOT NULL,
                    sol_amount numeric(18,9) NOT NULL,
                    new_token_balance numeric(28,12) NOT NULL,
                    bonding_curve_key text NOT NULL,
                    v_tokens_in_bonding_curve numeric(28,12) NOT NULL,
                    v_sol_in_bonding_curve numeric(18,9) NOT NULL,
                    market_cap_sol numeric(18,9) NOT NULL,
                    pool text,
                    received_at_utc timestamptz NOT NULL
                )
                """, conn);
            await cmdCreate.ExecuteNonQueryAsync(ct);

            // Binary COPY into temp table - use explicit scope to ensure disposal
            {
                await using var writer = await conn.BeginBinaryImportAsync(
                    $"""COPY {tempTable} (signature, mint, trader_public_key, tx_type, token_amount, sol_amount, new_token_balance, bonding_curve_key, v_tokens_in_bonding_curve, v_sol_in_bonding_curve, market_cap_sol, pool, received_at_utc) FROM STDIN (FORMAT BINARY)""",
                    ct);

                foreach (var e in events)
                {
                    await writer.StartRowAsync(ct);
                    await writer.WriteAsync(e.Signature, NpgsqlDbType.Text, ct);
                    await writer.WriteAsync(e.Mint, NpgsqlDbType.Text, ct);
                    await writer.WriteAsync(e.TraderPublicKey, NpgsqlDbType.Text, ct);
                    await writer.WriteAsync((short)e.TxType, NpgsqlDbType.Smallint, ct);
                    await writer.WriteAsync(e.TokenAmount, NpgsqlDbType.Numeric, ct);
                    await writer.WriteAsync(e.SolAmount, NpgsqlDbType.Numeric, ct);
                    await writer.WriteAsync(e.NewTokenBalance, NpgsqlDbType.Numeric, ct);
                    await writer.WriteAsync(e.BondingCurveKey, NpgsqlDbType.Text, ct);
                    await writer.WriteAsync(e.VTokensInBondingCurve, NpgsqlDbType.Numeric, ct);
                    await writer.WriteAsync(e.VSolInBondingCurve, NpgsqlDbType.Numeric, ct);
                    await writer.WriteAsync(e.MarketCapSol, NpgsqlDbType.Numeric, ct);
                    if (e.Pool is not null)
                        await writer.WriteAsync(e.Pool, NpgsqlDbType.Text, ct);
                    else
                        await writer.WriteNullAsync(ct);
                    await writer.WriteAsync(e.ReceivedAtUtc, NpgsqlDbType.TimestampTz, ct);
                }

                await writer.CompleteAsync(ct);
            } // Writer is fully disposed here before next command

            // Move from temp to real table, skipping conflicts
            await using var cmdInsert = new NpgsqlCommand($"""
                INSERT INTO trade_events (signature, mint, trader_public_key, tx_type, token_amount, sol_amount, new_token_balance, bonding_curve_key, v_tokens_in_bonding_curve, v_sol_in_bonding_curve, market_cap_sol, pool, received_at_utc)
                SELECT signature, mint, trader_public_key, tx_type, token_amount, sol_amount, new_token_balance, bonding_curve_key, v_tokens_in_bonding_curve, v_sol_in_bonding_curve, market_cap_sol, pool, received_at_utc
                FROM {tempTable}
                ON CONFLICT (signature) DO NOTHING
                """, conn);

            var inserted = await cmdInsert.ExecuteNonQueryAsync(ct);

            if (inserted < events.Count)
                _logger.LogDebug("Inserted {Inserted}/{Total} trade events ({Dupes} duplicates skipped)",
                    inserted, events.Count, events.Count - inserted);

            return inserted;
        }
        finally
        {
            // Clean up temp table
            try
            {
                await using var cmdDrop = new NpgsqlCommand($"DROP TABLE IF EXISTS {tempTable}", conn);
                await cmdDrop.ExecuteNonQueryAsync(CancellationToken.None);
            }
            catch
            {
                // Temp tables auto-drop on connection close anyway, so failures here are non-critical
            }
        }
    }

    /// <summary>
    /// Stream all trade events in a time range, ordered deterministically by (received_at_utc, id).
    /// Uses AsNoTracking + AsAsyncEnumerable for memory-efficient streaming.
    /// </summary>
    public async IAsyncEnumerable<TradeEvent> GetEventsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var query = _db.TradeEvents
            .AsNoTracking()
            .Where(e => e.ReceivedAtUtc >= from && e.ReceivedAtUtc <= to)
            .OrderBy(e => e.ReceivedAtUtc)
            .ThenBy(e => e.Id)
            .AsAsyncEnumerable();

        await foreach (var tradeEvent in query.WithCancellation(ct))
        {
            yield return tradeEvent;
        }
    }

    /// <summary>
    /// Stream trade events for a specific trader in a time range.
    /// </summary>
    public async IAsyncEnumerable<TradeEvent> GetEventsByTraderAsync(
        string traderPublicKey,
        DateTimeOffset from,
        DateTimeOffset to,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var query = _db.TradeEvents
            .AsNoTracking()
            .Where(e => e.TraderPublicKey == traderPublicKey
                        && e.ReceivedAtUtc >= from
                        && e.ReceivedAtUtc <= to)
            .OrderBy(e => e.ReceivedAtUtc)
            .ThenBy(e => e.Id)
            .AsAsyncEnumerable();

        await foreach (var tradeEvent in query.WithCancellation(ct))
        {
            yield return tradeEvent;
        }
    }
}
