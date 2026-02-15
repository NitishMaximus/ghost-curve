using GhostCurve.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace GhostCurve.Infrastructure.Persistence;

public sealed class GhostCurveDbContext : DbContext
{
    public GhostCurveDbContext(DbContextOptions<GhostCurveDbContext> options)
        : base(options)
    {
    }

    public DbSet<TradeEvent> TradeEvents => Set<TradeEvent>();
    public DbSet<SimulatedTrade> SimulatedTrades => Set<SimulatedTrade>();
    public DbSet<SimulationSession> SimulationSessions => Set<SimulationSession>();
    public DbSet<PerformanceSnapshot> PerformanceSnapshots => Set<PerformanceSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GhostCurveDbContext).Assembly);
    }
}
