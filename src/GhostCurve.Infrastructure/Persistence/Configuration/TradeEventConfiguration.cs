using GhostCurve.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhostCurve.Infrastructure.Persistence.Configuration;

public sealed class TradeEventConfiguration : IEntityTypeConfiguration<TradeEvent>
{
    public void Configure(EntityTypeBuilder<TradeEvent> builder)
    {
        builder.ToTable("trade_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(e => e.Signature)
            .HasColumnName("signature")
            .IsRequired();

        builder.Property(e => e.Mint)
            .HasColumnName("mint")
            .IsRequired();

        builder.Property(e => e.TraderPublicKey)
            .HasColumnName("trader_public_key")
            .IsRequired();

        builder.Property(e => e.TxType)
            .HasColumnName("tx_type")
            .HasConversion<short>();

        builder.Property(e => e.TokenAmount)
            .HasColumnName("token_amount")
            .HasPrecision(28, 12);

        builder.Property(e => e.SolAmount)
            .HasColumnName("sol_amount")
            .HasPrecision(18, 9);

        builder.Property(e => e.NewTokenBalance)
            .HasColumnName("new_token_balance")
            .HasPrecision(28, 12);

        builder.Property(e => e.BondingCurveKey)
            .HasColumnName("bonding_curve_key")
            .IsRequired();

        builder.Property(e => e.VTokensInBondingCurve)
            .HasColumnName("v_tokens_in_bonding_curve")
            .HasPrecision(28, 12);

        builder.Property(e => e.VSolInBondingCurve)
            .HasColumnName("v_sol_in_bonding_curve")
            .HasPrecision(18, 9);

        builder.Property(e => e.MarketCapSol)
            .HasColumnName("market_cap_sol")
            .HasPrecision(18, 9);

        builder.Property(e => e.Pool)
            .HasColumnName("pool");

        builder.Property(e => e.ReceivedAtUtc)
            .HasColumnName("received_at_utc");

        builder.Property(e => e.IngestedAtUtc)
            .HasColumnName("ingested_at_utc")
            .HasDefaultValueSql("now()");

        // Source is not persisted — it's a runtime-only field
        builder.Ignore(e => e.Source);

        // Indexes
        builder.HasIndex(e => e.Signature).IsUnique();
        builder.HasIndex(e => new { e.TraderPublicKey, e.ReceivedAtUtc })
            .HasDatabaseName("ix_trade_events_trader_received");
        builder.HasIndex(e => e.Mint)
            .HasDatabaseName("ix_trade_events_mint");
    }
}
