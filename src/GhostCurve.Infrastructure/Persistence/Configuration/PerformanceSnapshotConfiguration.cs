using GhostCurve.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhostCurve.Infrastructure.Persistence.Configuration;

public sealed class PerformanceSnapshotConfiguration : IEntityTypeConfiguration<PerformanceSnapshot>
{
    public void Configure(EntityTypeBuilder<PerformanceSnapshot> builder)
    {
        builder.ToTable("performance_snapshots");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(e => e.SessionId)
            .HasColumnName("session_id");

        builder.Property(e => e.SnapshotAtUtc)
            .HasColumnName("snapshot_at_utc");

        builder.Property(e => e.TotalTrades)
            .HasColumnName("total_trades");

        builder.Property(e => e.WinCount)
            .HasColumnName("win_count");

        builder.Property(e => e.LossCount)
            .HasColumnName("loss_count");

        builder.Property(e => e.WinRate)
            .HasColumnName("win_rate")
            .HasPrecision(8, 4);

        builder.Property(e => e.AvgRoiPercent)
            .HasColumnName("avg_roi_percent")
            .HasPrecision(12, 6);

        builder.Property(e => e.TotalRealizedPnl)
            .HasColumnName("total_realized_pnl")
            .HasPrecision(18, 9);

        builder.Property(e => e.TotalUnrealizedPnl)
            .HasColumnName("total_unrealized_pnl")
            .HasPrecision(18, 9);

        builder.Property(e => e.MaxDrawdownPercent)
            .HasColumnName("max_drawdown_percent")
            .HasPrecision(8, 4);

        builder.Property(e => e.SolBalance)
            .HasColumnName("sol_balance")
            .HasPrecision(18, 9);

        builder.Property(e => e.TotalPortfolioValue)
            .HasColumnName("total_portfolio_value")
            .HasPrecision(18, 9);

        // Indexes
        builder.HasIndex(e => new { e.SessionId, e.SnapshotAtUtc })
            .HasDatabaseName("ix_performance_snapshots_session");
    }
}
