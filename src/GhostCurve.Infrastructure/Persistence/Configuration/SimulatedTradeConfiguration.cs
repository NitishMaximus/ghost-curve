using GhostCurve.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhostCurve.Infrastructure.Persistence.Configuration;

public sealed class SimulatedTradeConfiguration : IEntityTypeConfiguration<SimulatedTrade>
{
    public void Configure(EntityTypeBuilder<SimulatedTrade> builder)
    {
        builder.ToTable("simulated_trades");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(e => e.SourceTradeEventId)
            .HasColumnName("source_trade_event_id");

        builder.Property(e => e.SessionId)
            .HasColumnName("session_id");

        builder.Property(e => e.Mint)
            .HasColumnName("mint")
            .IsRequired();

        builder.Property(e => e.Side)
            .HasColumnName("side")
            .HasConversion<short>();

        builder.Property(e => e.SolAmount)
            .HasColumnName("sol_amount")
            .HasPrecision(18, 9);

        builder.Property(e => e.TokenAmount)
            .HasColumnName("token_amount")
            .HasPrecision(28, 12);

        builder.Property(e => e.SimulatedPrice)
            .HasColumnName("simulated_price")
            .HasPrecision(28, 18);

        builder.Property(e => e.SlippageBps)
            .HasColumnName("slippage_bps")
            .HasPrecision(8, 2);

        builder.Property(e => e.DelayMs)
            .HasColumnName("delay_ms");

        builder.Property(e => e.ExecutedAtUtc)
            .HasColumnName("executed_at_utc");

        builder.Property(e => e.VTokensAtExecution)
            .HasColumnName("v_tokens_at_execution")
            .HasPrecision(28, 12);

        builder.Property(e => e.VSolAtExecution)
            .HasColumnName("v_sol_at_execution")
            .HasPrecision(18, 9);

        builder.Property(e => e.RealizedPnl)
            .HasColumnName("realized_pnl")
            .HasPrecision(18, 9);

        // Indexes
        builder.HasIndex(e => new { e.SessionId, e.Mint, e.ExecutedAtUtc })
            .HasDatabaseName("ix_simulated_trades_session_mint");

        builder.HasIndex(e => e.SourceTradeEventId)
            .HasDatabaseName("ix_simulated_trades_source_event");
    }
}
