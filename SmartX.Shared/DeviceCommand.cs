namespace SmartX.Shared;

public enum CommandType
{
    TriggerPump,
    ToggleRelay,
    ShutDownAllValves
}

// one manual override command, keeps the previous state of every device it
// touched so the stack can undo it later, even for a bulk command like
// shut down all valves
public class DeviceCommand
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public CommandType Type { get; set; }
    public List<string> TargetDeviceIds { get; set; } = new();

    // one entry per device this command touched, undo just restores these
    public Dictionary<string, bool> PreviousStates { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public override string ToString()
    {
        return $"{Type} on {TargetDeviceIds.Count} device(s) @ {Timestamp:HH:mm:ss}";
    }
}
