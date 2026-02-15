using GhostCurve.Domain.Enums;

namespace GhostCurve.Domain.Models;

/// <summary>
/// Represents a simulation session — either live or replay.
/// Captures the frozen configuration snapshot used for that run.
/// </summary>
public sealed class SimulationSession
{
    public Guid Id { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset? EndedAtUtc { get; set; }

    public SimulationMode Mode { get; init; }

    /// <summary>JSON snapshot of the SimulationOptions active during this session.</summary>
    public required string ConfigJson { get; init; }

    public decimal InitialSolBalance { get; init; }

    public decimal? FinalSolBalance { get; set; }
}
