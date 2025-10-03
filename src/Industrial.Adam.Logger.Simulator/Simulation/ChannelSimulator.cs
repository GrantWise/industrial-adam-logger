namespace Industrial.Adam.Logger.Simulator.Simulation;

/// <summary>
/// Simulates a single ADAM-6051 channel
/// </summary>
public class ChannelSimulator
{
    private readonly ILogger<ChannelSimulator> _logger;
    private readonly ProductionSimulator? _productionSimulator;
    private readonly Random _random = new();

    public int ChannelNumber { get; }
    public string Name { get; }
    public ChannelType Type { get; }
    public bool Enabled { get; set; }

    // Counter state
    private uint _counterValue;
    private bool _digitalInputState;
    private DateTime _lastPulseTime = DateTime.UtcNow;
    private DateTime _lastUpdate = DateTime.UtcNow;

    // For reject counters
    public double RejectRate { get; set; } = 0.05; // 5% reject rate

    public ChannelSimulator(
        int channelNumber,
        string name,
        ChannelType type,
        ILogger<ChannelSimulator> logger,
        ProductionSimulator? productionSimulator = null)
    {
        ChannelNumber = channelNumber;
        Name = name ?? $"Channel {channelNumber}";
        Type = type;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _productionSimulator = productionSimulator;

        // Subscribe to production events if this is a production counter
        if (_productionSimulator != null && type == ChannelType.ProductionCounter)
        {
            _productionSimulator.UnitProduced += OnUnitProduced;
        }
    }

    /// <summary>
    /// Update the channel simulation
    /// </summary>
    public void Update()
    {
        if (!Enabled)
            return;

        var now = DateTime.UtcNow;

        switch (Type)
        {
            case ChannelType.ProductionCounter:
                // Production counter is driven by ProductionSimulator events
                UpdateDigitalInputState();
                break;

            case ChannelType.RejectCounter:
                // Reject counter increments based on production rate and reject percentage
                if (_productionSimulator != null &&
                    _productionSimulator.CurrentState == ProductionState.Running)
                {
                    var timeSinceLastUpdate = now - _lastUpdate;
                    if (timeSinceLastUpdate.TotalSeconds >= 1.0 && _random.NextDouble() < RejectRate)
                    {
                        IncrementCounter();
                        _lastUpdate = now;
                    }
                }
                UpdateDigitalInputState();
                break;

            case ChannelType.IndependentCounter:
                // Independent counter with its own rate
                UpdateIndependentCounter();
                break;

            case ChannelType.Frequency:
                // Frequency measurement (not implemented in this version)
                break;
        }
    }

    /// <summary>
    /// Get current counter value
    /// </summary>
    public uint GetCounterValue() => _counterValue;

    /// <summary>
    /// Get current digital input state
    /// </summary>
    public bool GetDigitalInputState() => _digitalInputState;

    /// <summary>
    /// Reset the counter
    /// </summary>
    public void ResetCounter()
    {
        _counterValue = 0;
        _logger.LogInformation("Channel {Channel} counter reset", ChannelNumber);
    }

    /// <summary>
    /// Force increment counter (for testing)
    /// </summary>
    public void ForceIncrement(uint count = 1)
    {
        _counterValue += count;
        _digitalInputState = true;
        _lastPulseTime = DateTime.UtcNow;
    }

    private void OnUnitProduced(object? sender, UnitProducedEventArgs e)
    {
        if (Enabled && Type == ChannelType.ProductionCounter)
        {
            IncrementCounter();
        }
    }

    private void IncrementCounter()
    {
        _counterValue++;

        // Handle 32-bit overflow
        if (_counterValue == 0)
        {
            _logger.LogWarning("Channel {Channel} counter overflow", ChannelNumber);
        }

        _digitalInputState = true;
        _lastPulseTime = DateTime.UtcNow;
    }

    private void UpdateDigitalInputState()
    {
        // Simulate pulse width - DI goes low after 50ms
        var timeSincePulse = DateTime.UtcNow - _lastPulseTime;
        if (_digitalInputState && timeSincePulse.TotalMilliseconds > 50)
        {
            _digitalInputState = false;
        }
    }

    private void UpdateIndependentCounter()
    {
        // For demo purposes, increment at a fixed rate
        var now = DateTime.UtcNow;
        if ((now - _lastUpdate).TotalSeconds >= 1.0)
        {
            IncrementCounter();
            _lastUpdate = now;
        }
        UpdateDigitalInputState();
    }
}

public enum ChannelType
{
    /// <summary>
    /// Main production counter tied to ProductionSimulator
    /// </summary>
    ProductionCounter,

    /// <summary>
    /// Reject counter that increments based on reject rate
    /// </summary>
    RejectCounter,

    /// <summary>
    /// Independent counter with its own rate
    /// </summary>
    IndependentCounter,

    /// <summary>
    /// Frequency measurement mode
    /// </summary>
    Frequency,

    /// <summary>
    /// Disabled channel
    /// </summary>
    Disabled
}
