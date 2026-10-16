using SmartX.Shared;

namespace SmartX.Api.Data;

// keyed by mac address instead of the generated id, this is what the
// processing engine uses to find a device the instant a packet arrives
// rather than scanning every registered sensor
public class DeviceRegistry
{
    private readonly Dictionary<string, SensorRegistration> _byMac = new();
    private readonly object _lock = new();

    public void Register(SensorRegistration sensor)
    {
        lock (_lock)
        {
            _byMac[sensor.MacAddress] = sensor;
        }
    }

    public SensorRegistration? FindByMac(string macAddress)
    {
        lock (_lock)
        {
            return _byMac.TryGetValue(macAddress, out var sensor) ? sensor : null;
        }
    }

    public int Count
    {
        get { lock (_lock) { return _byMac.Count; } }
    }
}
