namespace Industrial.Adam.Logger.Simulator.Simulation;

/// <summary>
/// Production states for the simulation
/// </summary>
public enum ProductionState
{
    /// <summary>
    /// No production - machine is off
    /// </summary>
    Idle,

    /// <summary>
    /// Setting up for a new job (changeover)
    /// </summary>
    Setup,

    /// <summary>
    /// Gradually increasing speed after setup or stoppage
    /// </summary>
    RampUp,

    /// <summary>
    /// Normal production at target rate
    /// </summary>
    Running,

    /// <summary>
    /// Slowing down before stop
    /// </summary>
    RampDown,

    /// <summary>
    /// Short pause (jam, material refill, minor adjustment)
    /// </summary>
    MinorStoppage,

    /// <summary>
    /// Long stop (breakdown, major issue)
    /// </summary>
    MajorStoppage,

    /// <summary>
    /// Scheduled break (lunch, shift change)
    /// </summary>
    ScheduledBreak
}
