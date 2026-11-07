using SmartX.Shared;

namespace SmartX.Api.Data;

// one avl tree per sensor, so a range query on one sensor never has to
// touch another sensor's readings at all, unlike TimestampedLogStore's
// single combined sorted dictionary which filters by sensor on every read
public class SensorHistoryTreeStore
{
    private readonly Dictionary<string, SensorHistoryAvlTree> _trees = new();
    private readonly object _lock = new();

    public void Add(QueuedTelemetry entry)
    {
        SensorHistoryAvlTree tree;

        lock (_lock)
        {
            if (!_trees.TryGetValue(entry.SensorId, out tree!))
            {
                tree = new SensorHistoryAvlTree();
                _trees[entry.SensorId] = tree;
            }
        }

        tree.Insert(entry.QueuedAt.Ticks, entry);
    }

    public List<QueuedTelemetry> RangeQuery(string sensorId, long fromTicks, long toTicks)
    {
        SensorHistoryAvlTree? tree;

        lock (_lock)
        {
            _trees.TryGetValue(sensorId, out tree);
        }

        return tree?.RangeQuery(fromTicks, toTicks) ?? new List<QueuedTelemetry>();
    }
}
