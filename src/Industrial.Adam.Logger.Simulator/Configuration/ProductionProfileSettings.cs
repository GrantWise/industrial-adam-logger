namespace Industrial.Adam.Logger.Simulator.Configuration;

/// <summary>
/// Production profile configuration settings
/// </summary>
public class ProductionProfileSettings
{
    public double BaseRate { get; set; } = 120.0;
    public double RateVariation { get; set; } = 0.1;
    public int JobSizeMin { get; set; } = 1000;
    public int JobSizeMax { get; set; } = 5000;

    public TimingSettings TimingSettings { get; set; } = new();
    public RampSettings RampSettings { get; set; } = new();
    public StoppageSettings StoppageSettings { get; set; } = new();
    public ContinuousOperationSettings ContinuousOperation { get; set; } = new();
}

/// <summary>
/// Timing and duration configuration
/// </summary>
public class TimingSettings
{
    public double SetupDurationMinutes { get; set; } = 15.0;
    public double RampUpDurationSeconds { get; set; } = 30.0;
    public double RampDownDurationSeconds { get; set; } = 10.0;
    public double IdleBetweenJobsSeconds { get; set; } = 45.0;
    public double DigitalPulseWidthMs { get; set; } = 50.0;
}

/// <summary>
/// Production ramp rate configuration
/// </summary>
public class RampSettings
{
    public double RampUpStartPercent { get; set; } = 20.0;
    public double RampUpEndPercent { get; set; } = 100.0;
    public double RampDownStartPercent { get; set; } = 100.0;
    public double RampDownEndPercent { get; set; } = 10.0;
}

/// <summary>
/// Breakdown and stoppage configuration
/// </summary>
public class StoppageSettings
{
    public double MinorStoppageProbability { get; set; } = 0.02;
    public double MajorStoppageProbability { get; set; } = 0.005;
    public int MinorStoppageMinSeconds { get; set; } = 30;
    public int MinorStoppageMaxSeconds { get; set; } = 120;
    public int MajorStoppageMinMinutes { get; set; } = 10;
    public int MajorStoppageMaxMinutes { get; set; } = 30;
}

/// <summary>
/// Continuous operation behavior configuration
/// </summary>
public class ContinuousOperationSettings
{
    public bool Enabled { get; set; } = true;
    public bool AutoRestartAfterJob { get; set; } = true;
    public bool ResetCountersOnNewJob { get; set; } = true;
}
