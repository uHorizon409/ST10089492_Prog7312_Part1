namespace SmartX.Shared;

public class SensorRegistration
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MacAddress { get; set; } = "";
    public string Location { get; set; } = "";
    public SensorCategory Category { get; set; }
    public double LastReading { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public List<string> AttachmentFileNames { get; set; } = new();

    // for actuators, true means a manual override is on (pump running, relay
    // closed etc), the simulator respects this instead of picking a random value
    public bool OverrideActive { get; set; }
}

// checks readings and last seen time so the dashboard knows what to highlight
public static class AnomalyChecker
{
    // rough normal ranges per category just for the simulation
    private static readonly Dictionary<SensorCategory, (double Min, double Max)> Ranges = new()
    {
        { SensorCategory.Environmental, (0, 100) },
        { SensorCategory.PowerConsumption, (0, 5000) },
        { SensorCategory.Actuator, (0, 1) }
    };

    public static bool IsReadingAnomalous(SensorCategory category, double reading)
    {
        var range = Ranges[category];
        return reading < range.Min || reading > range.Max;
    }

    // sensor counts as disconnected if it hasnt reported in longer than the timeout
    public static bool IsDisconnected(DateTime lastSeen, TimeSpan timeout)
    {
        return DateTime.UtcNow - lastSeen > timeout;
    }
}
