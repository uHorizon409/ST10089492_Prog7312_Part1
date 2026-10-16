namespace SmartX.Api.Data;

// tracks which devices are currently in an alert state, a set only stores
// a device once no matter how many times it gets flagged, so the same
// disconnect doesnt generate a fresh alert every processing cycle
public class DeviceAlertTracker
{
    private readonly HashSet<string> _activeAlertDeviceIds = new();
    private readonly object _lock = new();

    // returns true only the first time a device enters alert state,
    // false on every repeat so the caller knows not to alert again
    public bool TryRaise(string deviceId)
    {
        lock (_lock)
        {
            return _activeAlertDeviceIds.Add(deviceId);
        }
    }

    public void Clear(string deviceId)
    {
        lock (_lock)
        {
            _activeAlertDeviceIds.Remove(deviceId);
        }
    }

    public bool IsActive(string deviceId)
    {
        lock (_lock)
        {
            return _activeAlertDeviceIds.Contains(deviceId);
        }
    }

    public int Count
    {
        get { lock (_lock) { return _activeAlertDeviceIds.Count; } }
    }
}
