namespace SmartX.Shared;

// one telemetry reading waiting to be processed, this is what actually
// sits in the queue/priority queue rather than the raw http request
public class QueuedTelemetry
{
    public string SensorId { get; set; } = "";
    public double Value { get; set; }
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
}

// decides whether a reading is bad enough to skip the normal queue
public static class CriticalAlertChecker
{
    // a reading this far outside the normal range counts as severe,
    // e.g. a power spike or a moisture sensor crashing toward zero
    public static bool IsCritical(SensorCategory category, double reading)
    {
        return category switch
        {
            SensorCategory.Environmental => reading < 5 || reading > 150,
            SensorCategory.PowerConsumption => reading > 4000,
            SensorCategory.Actuator => false,
            _ => false
        };
    }
}
