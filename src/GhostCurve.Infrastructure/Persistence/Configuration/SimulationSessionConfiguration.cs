using GhostCurve.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhostCurve.Infrastructure.Persistence.Configuration;

public sealed class SimulationSessionConfiguration : IEntityTypeConfiguration<SimulationSession>
{
    public void Configure(EntityTypeBuilder<SimulationSession> builder)
    {
        builder.ToTable("simulation_sessions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.StartedAtUtc)
            .HasColumnName("started_at_utc");

        builder.Property(e => e.EndedAtUtc)
            .HasColumnName("ended_at_utc");

        builder.Property(e => e.Mode)
            .HasColumnName("mode")
            .HasConversion<string>();

        builder.Property(e => e.ConfigJson)
            .HasColumnName("config_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.InitialSolBalance)
            .HasColumnName("initial_sol_balance")
            .HasPrecision(18, 9);

        builder.Property(e => e.FinalSolBalance)
            .HasColumnName("final_sol_balance")
            .HasPrecision(18, 9);
    }
}
