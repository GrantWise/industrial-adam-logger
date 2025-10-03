namespace Industrial.Adam.Logger.Simulator.Modbus;

/// <summary>
/// ADAM-6051 register mapping according to the user manual
/// </summary>
public class Adam6051RegisterMap
{
    // Register addresses based on ADAM-6051 documentation
    public const int CounterStartAddress = 0;      // 32-bit counters (0-31)
    public const int DiStatusStartAddress = 32;    // Digital input status (32-47)
    public const int FrequencyStartAddress = 112;  // Frequency values
    public const int TotalChannels = 16;

    private readonly ushort[] _holdingRegisters = new ushort[256];
    private readonly object _lock = new object();

    /// <summary>
    /// Update a 32-bit counter value (stored in 2 consecutive registers)
    /// </summary>
    public void UpdateCounter(int channel, uint counterValue)
    {
        if (channel < 0 || channel >= TotalChannels)
            throw new ArgumentOutOfRangeException(nameof(channel));

        lock (_lock)
        {
            int baseAddress = CounterStartAddress + (channel * 2);
            _holdingRegisters[baseAddress] = (ushort)(counterValue & 0xFFFF);      // Low word
            _holdingRegisters[baseAddress + 1] = (ushort)(counterValue >> 16);     // High word
        }
    }

    /// <summary>
    /// Get a 32-bit counter value from 2 consecutive registers
    /// </summary>
    public uint GetCounter(int channel)
    {
        if (channel < 0 || channel >= TotalChannels)
            throw new ArgumentOutOfRangeException(nameof(channel));

        lock (_lock)
        {
            int baseAddress = CounterStartAddress + (channel * 2);
            uint lowWord = _holdingRegisters[baseAddress];
            uint highWord = _holdingRegisters[baseAddress + 1];
            return (highWord << 16) | lowWord;
        }
    }

    /// <summary>
    /// Update digital input status for a channel
    /// </summary>
    public void UpdateDigitalInput(int channel, bool state)
    {
        if (channel < 0 || channel >= TotalChannels)
            throw new ArgumentOutOfRangeException(nameof(channel));

        lock (_lock)
        {
            int address = DiStatusStartAddress + channel;
            _holdingRegisters[address] = (ushort)(state ? 1 : 0);
        }
    }

    /// <summary>
    /// Update frequency value for a channel
    /// </summary>
    public void UpdateFrequency(int channel, ushort frequency)
    {
        if (channel < 0 || channel >= TotalChannels)
            throw new ArgumentOutOfRangeException(nameof(channel));

        lock (_lock)
        {
            int address = FrequencyStartAddress + channel;
            _holdingRegisters[address] = frequency;
        }
    }

    /// <summary>
    /// Read holding registers (Modbus function 03)
    /// </summary>
    public ushort[] ReadHoldingRegisters(ushort startAddress, ushort quantity)
    {
        if (startAddress + quantity > _holdingRegisters.Length)
            throw new ArgumentOutOfRangeException("Invalid register range");

        lock (_lock)
        {
            var result = new ushort[quantity];
            Array.Copy(_holdingRegisters, startAddress, result, 0, quantity);
            return result;
        }
    }

    /// <summary>
    /// Read input registers (Modbus function 04) - same as holding registers for ADAM-6051
    /// </summary>
    public ushort[] ReadInputRegisters(ushort startAddress, ushort quantity)
    {
        return ReadHoldingRegisters(startAddress, quantity);
    }

    /// <summary>
    /// Reset all counters to zero
    /// </summary>
    public void ResetAllCounters()
    {
        lock (_lock)
        {
            for (int i = CounterStartAddress; i < CounterStartAddress + (TotalChannels * 2); i++)
            {
                _holdingRegisters[i] = 0;
            }
        }
    }

    /// <summary>
    /// Reset a specific counter
    /// </summary>
    public void ResetCounter(int channel)
    {
        UpdateCounter(channel, 0);
    }
}
