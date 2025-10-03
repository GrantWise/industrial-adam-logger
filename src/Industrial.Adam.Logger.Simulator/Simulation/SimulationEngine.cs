using Industrial.Adam.Logger.Simulator.Configuration;
using Industrial.Adam.Logger.Simulator.Modbus;

namespace Industrial.Adam.Logger.Simulator.Simulation;

/// <summary>
/// Main simulation engine that coordinates all channels and updates Modbus registers
/// </summary>
public class SimulationEngine : IHostedService, IDisposable
{
    private readonly Adam6051RegisterMap _registerMap;
    private readonly ProductionSimulator _productionSimulator;
    private readonly List<ChannelSimulator> _channels;
    private readonly ILogger<SimulationEngine> _logger;
    private readonly IConfiguration _configuration;
    private readonly ProductionProfileSettings _productionProfile;

    private Timer? _updateTimer;
    private Timer? _scheduleTimer;
    private readonly object _lock = new();

    public SimulationEngine(
        Adam6051RegisterMap registerMap,
        ILogger<SimulationEngine> logger,
        ILoggerFactory loggerFactory,
        IConfiguration configuration)
    {
        _registerMap = registerMap ?? throw new ArgumentNullException(nameof(registerMap));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Load production profile configuration
        _productionProfile = LoadProductionProfile();

        // Get device ID from configuration
        var deviceId = _configuration["SimulatorSettings:DeviceId"] ?? "SIM001";

        // Create production simulator
        _productionSimulator = new ProductionSimulator(
            deviceId,
            loggerFactory.CreateLogger<ProductionSimulator>());

        // Subscribe to counter reset events
        _productionSimulator.CounterResetRequested += OnCounterResetRequested;

        // Configure production parameters from settings
        ConfigureProduction();

        // Create channel simulators
        _channels = new List<ChannelSimulator>();
        ConfigureChannels(loggerFactory);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting simulation engine");

        // Start update timer (100ms interval for smooth counter updates)
        _updateTimer = new Timer(
            UpdateSimulation,
            null,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(100));

        // Start schedule timer (check every minute)
        _scheduleTimer = new Timer(
            CheckSchedule,
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));

        // Start with a new job
        _productionSimulator.StartNewJob();

        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping simulation engine");

        _updateTimer?.Change(Timeout.Infinite, 0);
        _scheduleTimer?.Change(Timeout.Infinite, 0);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Get current production state
    /// </summary>
    public ProductionState GetProductionState() => _productionSimulator.CurrentState;

    /// <summary>
    /// Get production statistics
    /// </summary>
    public object GetStatistics()
    {
        lock (_lock)
        {
            return new
            {
                DeviceId = _productionSimulator.DeviceId,
                State = _productionSimulator.CurrentState.ToString(),
                TimeInState = _productionSimulator.TimeInCurrentState,
                CurrentJobSize = _productionSimulator.CurrentJobSize,
                UnitsProduced = _productionSimulator.UnitsProducedInJob,
                TotalUnits = _productionSimulator.TotalUnitsProduced,
                Channels = _channels.Select(c => new
                {
                    Channel = c.ChannelNumber,
                    Name = c.Name,
                    Type = c.Type.ToString(),
                    Counter = c.GetCounterValue(),
                    DigitalInput = c.GetDigitalInputState()
                })
            };
        }
    }

    /// <summary>
    /// Force a production stoppage
    /// </summary>
    public void ForceStoppage(ProductionState stoppageType)
    {
        if (stoppageType == ProductionState.MinorStoppage ||
            stoppageType == ProductionState.MajorStoppage)
        {
            _productionSimulator.ForceTransition(stoppageType);
        }
    }

    /// <summary>
    /// Start a new job
    /// </summary>
    public void StartNewJob()
    {
        _productionSimulator.StartNewJob();
    }

    /// <summary>
    /// Reset a specific channel counter
    /// </summary>
    public void ResetChannel(int channelNumber)
    {
        lock (_lock)
        {
            var channel = _channels.FirstOrDefault(c => c.ChannelNumber == channelNumber);
            channel?.ResetCounter();
            _registerMap.ResetCounter(channelNumber);
        }
    }

    private void UpdateSimulation(object? state)
    {
        try
        {
            lock (_lock)
            {
                // Update production simulator
                _productionSimulator.Update();

                // Update all channels
                foreach (var channel in _channels)
                {
                    channel.Update();

                    // Update Modbus registers
                    _registerMap.UpdateCounter(channel.ChannelNumber, channel.GetCounterValue());
                    _registerMap.UpdateDigitalInput(channel.ChannelNumber, channel.GetDigitalInputState());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating simulation");
        }
    }

    private void CheckSchedule(object? state)
    {
        try
        {
            var schedule = _configuration.GetSection("Schedule");
            var now = DateTime.Now.TimeOfDay;

            // Check for scheduled breaks
            var breaks = schedule.GetSection("Breaks").GetChildren();
            foreach (var breakConfig in breaks)
            {
                var breakTime = TimeSpan.Parse(breakConfig["Time"] ?? "12:00");
                var duration = int.Parse(breakConfig["Duration"] ?? "30");

                // If we're within a minute of break time and running
                if (Math.Abs((now - breakTime).TotalMinutes) < 1 &&
                    _productionSimulator.CurrentState == ProductionState.Running)
                {
                    _logger.LogInformation("Taking scheduled break at {Time} for {Duration} minutes",
                        breakTime, duration);
                    _productionSimulator.TakeScheduledBreak(TimeSpan.FromMinutes(duration));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking schedule");
        }
    }

    private void ConfigureProduction()
    {
        // Apply production profile settings to simulator
        _productionSimulator.BaseRate = _productionProfile.BaseRate;
        _productionSimulator.RateVariation = _productionProfile.RateVariation;
        _productionSimulator.JobSizeMin = _productionProfile.JobSizeMin;
        _productionSimulator.JobSizeMax = _productionProfile.JobSizeMax;

        // Timing settings
        _productionSimulator.SetupDuration = TimeSpan.FromMinutes(_productionProfile.TimingSettings.SetupDurationMinutes);
        _productionSimulator.RampUpDuration = TimeSpan.FromSeconds(_productionProfile.TimingSettings.RampUpDurationSeconds);
        _productionSimulator.RampDownDuration = TimeSpan.FromSeconds(_productionProfile.TimingSettings.RampDownDurationSeconds);
        _productionSimulator.IdleBetweenJobs = TimeSpan.FromSeconds(_productionProfile.TimingSettings.IdleBetweenJobsSeconds);

        // Stoppage settings
        _productionSimulator.MinorStoppageProbability = _productionProfile.StoppageSettings.MinorStoppageProbability;
        _productionSimulator.MajorStoppageProbability = _productionProfile.StoppageSettings.MajorStoppageProbability;
        _productionSimulator.MinorStoppageMinSeconds = _productionProfile.StoppageSettings.MinorStoppageMinSeconds;
        _productionSimulator.MinorStoppageMaxSeconds = _productionProfile.StoppageSettings.MinorStoppageMaxSeconds;
        _productionSimulator.MajorStoppageMinMinutes = _productionProfile.StoppageSettings.MajorStoppageMinMinutes;
        _productionSimulator.MajorStoppageMaxMinutes = _productionProfile.StoppageSettings.MajorStoppageMaxMinutes;

        // Ramp settings
        _productionSimulator.RampUpStartPercent = _productionProfile.RampSettings.RampUpStartPercent;
        _productionSimulator.RampUpEndPercent = _productionProfile.RampSettings.RampUpEndPercent;
        _productionSimulator.RampDownStartPercent = _productionProfile.RampSettings.RampDownStartPercent;
        _productionSimulator.RampDownEndPercent = _productionProfile.RampSettings.RampDownEndPercent;

        // Continuous operation settings
        _productionSimulator.ContinuousOperationEnabled = _productionProfile.ContinuousOperation.Enabled;
        _productionSimulator.AutoRestartAfterJob = _productionProfile.ContinuousOperation.AutoRestartAfterJob;
        _productionSimulator.ResetCountersOnNewJob = _productionProfile.ContinuousOperation.ResetCountersOnNewJob;

        _logger.LogInformation("Production configured: BaseRate={BaseRate}, ContinuousOperation={Continuous}, CounterReset={Reset}",
            _productionProfile.BaseRate, _productionProfile.ContinuousOperation.Enabled, _productionProfile.ContinuousOperation.ResetCountersOnNewJob);
    }

    private void ConfigureChannels(ILoggerFactory loggerFactory)
    {
        var channelsConfig = _configuration.GetSection("Channels").GetChildren();

        foreach (var channelConfig in channelsConfig)
        {
            var number = int.Parse(channelConfig["Number"] ?? "0");
            var name = channelConfig["Name"] ?? $"Channel {number}";
            var typeStr = channelConfig["Type"] ?? "Disabled";
            var enabled = bool.Parse(channelConfig["Enabled"] ?? "false");

            if (!Enum.TryParse<ChannelType>(typeStr, out var type))
            {
                type = ChannelType.Disabled;
            }

            var channel = new ChannelSimulator(
                number,
                name,
                type,
                loggerFactory.CreateLogger<ChannelSimulator>(),
                type == ChannelType.ProductionCounter ? _productionSimulator : null)
            {
                Enabled = enabled
            };

            // Configure reject rate if it's a reject counter
            if (type == ChannelType.RejectCounter)
            {
                channel.RejectRate = double.Parse(channelConfig["RejectRate"] ?? "0.05");
            }

            _channels.Add(channel);
        }

        _logger.LogInformation("Configured {Count} channels", _channels.Count);
    }

    private ProductionProfileSettings LoadProductionProfile()
    {
        var productionProfile = new ProductionProfileSettings();

        try
        {
            // Try to load from production profile file
            var configPath = Path.Combine(AppContext.BaseDirectory, "config", "production-profile.json");

            if (File.Exists(configPath))
            {
                var configBuilder = new ConfigurationBuilder()
                    .AddJsonFile(configPath, optional: true, reloadOnChange: false);
                var profileConfig = configBuilder.Build();

                profileConfig.GetSection("ProductionProfile").Bind(productionProfile);
                _logger.LogInformation("Loaded production profile from {ConfigPath}", configPath);
            }
            else
            {
                // Fallback to existing configuration structure
                var settings = _configuration.GetSection("ProductionSettings");
                if (settings.Exists())
                {
                    productionProfile.BaseRate = double.Parse(settings["BaseRate"] ?? "120");
                    productionProfile.RateVariation = double.Parse(settings["RateVariation"] ?? "0.1");
                    productionProfile.JobSizeMin = int.Parse(settings["JobSizeMin"] ?? "1000");
                    productionProfile.JobSizeMax = int.Parse(settings["JobSizeMax"] ?? "5000");
                    productionProfile.TimingSettings.SetupDurationMinutes = double.Parse(settings["SetupDurationMinutes"] ?? "15");
                    productionProfile.StoppageSettings.MinorStoppageProbability = double.Parse(settings["MinorStoppageProbability"] ?? "0.02");
                    productionProfile.StoppageSettings.MajorStoppageProbability = double.Parse(settings["MajorStoppageProbability"] ?? "0.005");
                    _logger.LogInformation("Loaded production profile from legacy appsettings");
                }
                else
                {
                    _logger.LogInformation("Using default production profile settings");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading production profile, using defaults");
        }

        return productionProfile;
    }

    private void OnCounterResetRequested(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            foreach (var channel in _channels)
            {
                if (channel.Type == ChannelType.ProductionCounter || channel.Type == ChannelType.RejectCounter)
                {
                    channel.ResetCounter();
                }
            }
            _logger.LogInformation("Reset counters for new job");
        }
    }

    public void Dispose()
    {
        _productionSimulator.CounterResetRequested -= OnCounterResetRequested;
        _updateTimer?.Dispose();
        _scheduleTimer?.Dispose();
    }
}
