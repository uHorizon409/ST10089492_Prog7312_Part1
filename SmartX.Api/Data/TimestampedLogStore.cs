using SmartX.Shared;

namespace SmartX.Api.Data;

// keeps every reading in timestamp order automatically, so the dashboard
// can pull a sensor's timeline straight out already sorted instead of
// sorting a list every time it renders a graph
public class TimestampedLogStore
{
    private readonly SortedDictionary<long, QueuedTelemetry> _log = new();
    private readonly object _lock = new();

    public void Add(QueuedTelemetry entry)
    {
        lock (_lock)
        {
            // ticks are precise enough that two entries landing on the exact
            // same tick is practically impossible, bump by one if it happens
            var key = entry.QueuedAt.Ticks;
            while (_log.ContainsKey(key))
            {
                key++;
            }
            _log[key] = entry;
        }
    }

    // already sorted oldest to newest because of SortedDictionary, no sort needed
    public List<QueuedTelemetry> GetTimelineFor(string sensorId)
    {
        lock (_lock)
        {
            return _log.Values.Where(e => e.SensorId == sensorId).ToList();
        }
    }

    public int Count
    {
        get { lock (_lock) { return _log.Count; } }
    }
}
