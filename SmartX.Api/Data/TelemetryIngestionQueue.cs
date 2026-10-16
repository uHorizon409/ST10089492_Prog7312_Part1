using SmartX.Shared;

namespace SmartX.Api.Data;

// standard readings go in the normal queue, critical ones skip straight to
// the priority queue so a severe spike never waits behind a backlog of
// ordinary telemetry
public class TelemetryIngestionQueue
{
    private readonly Queue<QueuedTelemetry> _standardQueue = new();
    private readonly PriorityQueue<QueuedTelemetry, int> _criticalQueue = new();
    private readonly object _lock = new();

    public void Enqueue(QueuedTelemetry item, bool isCritical)
    {
        lock (_lock)
        {
            if (isCritical)
            {
                // earlier queued items get a lower priority number so they
                // still come out first among other critical alerts
                _criticalQueue.Enqueue(item, _criticalQueue.Count);
            }
            else
            {
                _standardQueue.Enqueue(item);
            }
        }
    }

    // critical queue always drains first, this is the actual bypass
    public bool TryDequeue(out QueuedTelemetry? item)
    {
        lock (_lock)
        {
            if (_criticalQueue.TryDequeue(out var critical, out _))
            {
                item = critical;
                return true;
            }

            if (_standardQueue.TryDequeue(out var standard))
            {
                item = standard;
                return true;
            }

            item = null;
            return false;
        }
    }

    public int StandardCount
    {
        get { lock (_lock) { return _standardQueue.Count; } }
    }

    public int CriticalCount
    {
        get { lock (_lock) { return _criticalQueue.Count; } }
    }
}
