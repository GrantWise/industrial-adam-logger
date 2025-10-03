namespace Industrial.Adam.Logger.Simulator.Simulation;

/// <summary>
/// Simulates realistic production patterns with state transitions
/// </summary>
public class ProductionSimulator
{
    private readonly ILogger<ProductionSimulator> _logger;
    private readonly Random _random = new();

    // State management
    private ProductionState _currentState = ProductionState.Idle;
    private DateTime _stateChangeTime = DateTime.UtcNow;
    private DateTime _lastProductionTime = DateTime.UtcNow;

    // Production parameters
    public string DeviceId { get; }
    public double BaseRate { get; set; } = 120.0;        // units per minute
    public double RateVariation { get; set; } = 0.1;     // ±10% random variation
    public int JobSizeMin { get; set; } = 1000;
    public int JobSizeMax { get; set; } = 5000;
    public TimeSpan SetupDuration { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan RampUpDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan RampDownDuration { get; set; } = TimeSpan.FromSeconds(10);

    // Stoppage probabilities (per minute)
    public double MinorStoppageProbability { get; set; } = 0.02;
    public double MajorStoppageProbability { get; set; } = 0.005;

    // Stoppage duration ranges
    public int MinorStoppageMinSeconds { get; set; } = 30;
    public int MinorStoppageMaxSeconds { get; set; } = 120;
    public int MajorStoppageMinMinutes { get; set; } = 10;
    public int MajorStoppageMaxMinutes { get; set; } = 30;

    // Ramp rate percentages
    public double RampUpStartPercent { get; set; } = 20.0;
    public double RampUpEndPercent { get; set; } = 100.0;
    public double RampDownStartPercent { get; set; } = 100.0;
    public double RampDownEndPercent { get; set; } = 10.0;

    // Current job tracking
    public int CurrentJobSize { get; private set; }
    public int UnitsProducedInJob { get; private set; }
    public int TotalUnitsProduced { get; private set; }

    // Events
    public event EventHandler<ProductionStateChangedEventArgs>? StateChanged;
    public event EventHandler<UnitProducedEventArgs>? UnitProduced;
    public event EventHandler? CounterResetRequested;

    // Continuous operation settings
    public bool ContinuousOperationEnabled { get; set; } = true;
    public bool AutoRestartAfterJob { get; set; } = true;
    public bool ResetCountersOnNewJob { get; set; } = true;
    public TimeSpan IdleBetweenJobs { get; set; } = TimeSpan.FromSeconds(45);

    public ProductionSimulator(string deviceId, ILogger<ProductionSimulator> logger)
    {
        DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get the current production state
    /// </summary>
    public ProductionState CurrentState => _currentState;

    /// <summary>
    /// Get time in current state
    /// </summary>
    public TimeSpan TimeInCurrentState => DateTime.UtcNow - _stateChangeTime;

    /// <summary>
    /// Start a new production job
    /// </summary>
    public void StartNewJob()
    {
        CurrentJobSize = _random.Next(JobSizeMin, JobSizeMax);
        UnitsProducedInJob = 0;

        // Trigger counter reset if enabled
        if (ResetCountersOnNewJob)
        {
            _logger.LogInformation("Resetting counters for new job on {DeviceId}", DeviceId);
            CounterResetRequested?.Invoke(this, EventArgs.Empty);
        }

        TransitionTo(ProductionState.Setup);
        _logger.LogInformation("Starting new job on {DeviceId}: {JobSize} units", DeviceId, CurrentJobSize);
    }

    /// <summary>
    /// Update the simulation state
    /// </summary>
    public void Update()
    {
        var now = DateTime.UtcNow;
        var timeInState = now - _stateChangeTime;

        switch (_currentState)
        {
            case ProductionState.Idle:
                // Check for continuous operation auto-restart
                if (ContinuousOperationEnabled && AutoRestartAfterJob &&
                    timeInState >= IdleBetweenJobs)
                {
                    _logger.LogInformation("{DeviceId} auto-starting new job after idle period", DeviceId);
                    StartNewJob();
                }
                break;

            case ProductionState.Setup:
                if (timeInState >= SetupDuration)
                {
                    TransitionTo(ProductionState.RampUp);
                }
                break;

            case ProductionState.RampUp:
                ProduceUnitsAtRate(GetRampUpRate(timeInState));
                if (timeInState >= RampUpDuration)
                {
                    TransitionTo(ProductionState.Running);
                }
                break;

            case ProductionState.Running:
                ProduceUnitsAtRate(GetRunningRate());
                CheckForStoppages();
                CheckJobCompletion();
                break;

            case ProductionState.RampDown:
                ProduceUnitsAtRate(GetRampDownRate(timeInState));
                if (timeInState >= RampDownDuration)
                {
                    TransitionTo(ProductionState.Idle);
                }
                break;

            case ProductionState.MinorStoppage:
                if (timeInState >= TimeSpan.FromSeconds(_random.Next(MinorStoppageMinSeconds, MinorStoppageMaxSeconds)))
                {
                    TransitionTo(ProductionState.RampUp);
                }
                break;

            case ProductionState.MajorStoppage:
                if (timeInState >= TimeSpan.FromMinutes(_random.Next(MajorStoppageMinMinutes, MajorStoppageMaxMinutes)))
                {
                    TransitionTo(ProductionState.RampUp);
                }
                break;

            case ProductionState.ScheduledBreak:
                // Handled externally by schedule
                break;
        }
    }

    /// <summary>
    /// Force a transition to a specific state
    /// </summary>
    public void ForceTransition(ProductionState newState)
    {
        TransitionTo(newState);
    }

    /// <summary>
    /// Take a scheduled break
    /// </summary>
    public void TakeScheduledBreak(TimeSpan duration)
    {
        TransitionTo(ProductionState.ScheduledBreak);
        Task.Delay(duration).ContinueWith(_ =>
        {
            if (_currentState == ProductionState.ScheduledBreak)
            {
                TransitionTo(ProductionState.RampUp);
            }
        });
    }

    private void TransitionTo(ProductionState newState)
    {
        var oldState = _currentState;
        _currentState = newState;
        _stateChangeTime = DateTime.UtcNow;

        _logger.LogInformation("{DeviceId} state changed: {OldState} -> {NewState}",
            DeviceId, oldState, newState);

        StateChanged?.Invoke(this, new ProductionStateChangedEventArgs(oldState, newState));
    }

    private double GetRampUpRate(TimeSpan timeInRampUp)
    {
        // Linear ramp from start% to end% of base rate
        var progress = Math.Min(1.0, timeInRampUp.TotalSeconds / RampUpDuration.TotalSeconds);
        var startFraction = RampUpStartPercent / 100.0;
        var endFraction = RampUpEndPercent / 100.0;
        return BaseRate * (startFraction + (endFraction - startFraction) * progress);
    }

    private double GetRunningRate()
    {
        // Base rate with random variation
        var variation = (_random.NextDouble() - 0.5) * 2 * RateVariation;
        return BaseRate * (1 + variation);
    }

    private double GetRampDownRate(TimeSpan timeInRampDown)
    {
        // Linear ramp from start% to end% of base rate
        var progress = Math.Min(1.0, timeInRampDown.TotalSeconds / RampDownDuration.TotalSeconds);
        var startFraction = RampDownStartPercent / 100.0;
        var endFraction = RampDownEndPercent / 100.0;
        return BaseRate * (startFraction + (endFraction - startFraction) * progress);
    }

    private void ProduceUnitsAtRate(double unitsPerMinute)
    {
        if (unitsPerMinute <= 0)
            return;

        var now = DateTime.UtcNow;
        var timeSinceLastProduction = now - _lastProductionTime;
        var secondsPerUnit = 60.0 / unitsPerMinute;

        if (timeSinceLastProduction.TotalSeconds >= secondsPerUnit)
        {
            UnitsProducedInJob++;
            TotalUnitsProduced++;
            _lastProductionTime = now;

            UnitProduced?.Invoke(this, new UnitProducedEventArgs(1));
        }
    }

    private void CheckForStoppages()
    {
        // Check once per minute
        var now = DateTime.UtcNow;
        if ((now - _lastProductionTime).TotalSeconds < 60)
            return;

        var roll = _random.NextDouble();

        if (roll < MajorStoppageProbability)
        {
            _logger.LogWarning("{DeviceId} major stoppage occurred", DeviceId);
            TransitionTo(ProductionState.MajorStoppage);
        }
        else if (roll < MinorStoppageProbability)
        {
            _logger.LogInformation("{DeviceId} minor stoppage occurred", DeviceId);
            TransitionTo(ProductionState.MinorStoppage);
        }
    }

    private void CheckJobCompletion()
    {
        if (UnitsProducedInJob >= CurrentJobSize)
        {
            _logger.LogInformation("{DeviceId} completed job: {Units} units produced",
                DeviceId, UnitsProducedInJob);
            TransitionTo(ProductionState.RampDown);
        }
    }
}

public class ProductionStateChangedEventArgs : EventArgs
{
    public ProductionState OldState { get; }
    public ProductionState NewState { get; }

    public ProductionStateChangedEventArgs(ProductionState oldState, ProductionState newState)
    {
        OldState = oldState;
        NewState = newState;
    }
}

public class UnitProducedEventArgs : EventArgs
{
    public int Count { get; }

    public UnitProducedEventArgs(int count)
    {
        Count = count;
    }
}
