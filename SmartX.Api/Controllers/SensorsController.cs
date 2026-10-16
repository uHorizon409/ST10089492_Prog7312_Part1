using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Data;
using SmartX.Shared;

namespace SmartX.Api.Controllers;

[ApiController]
[Route("api/sensors")]
public partial class SensorsController : ControllerBase
{
    private readonly SensorStore _store;
    private readonly DeviceRegistry _deviceRegistry;

    public SensorsController(SensorStore store, DeviceRegistry deviceRegistry)
    {
        _store = store;
        _deviceRegistry = deviceRegistry;
    }

    [GeneratedRegex(@"^([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$")]
    private static partial Regex MacAddressPattern();

    public class RegisterSensorRequest
    {
        public string MacAddress { get; set; } = "";
        public string Location { get; set; } = "";
        public SensorCategory Category { get; set; }
    }

    [HttpPost]
    public ActionResult<SensorRegistration> Register(RegisterSensorRequest request)
    {
        var mac = request.MacAddress.Trim();
        var location = request.Location.Trim();

        if (string.IsNullOrWhiteSpace(mac) || string.IsNullOrWhiteSpace(location))
        {
            return BadRequest("mac address and location are required");
        }

        if (!MacAddressPattern().IsMatch(mac))
        {
            return BadRequest("mac address must look like AA:BB:CC:DD:EE:FF");
        }

        if (!Enum.IsDefined(request.Category))
        {
            return BadRequest("category is not a valid sensor category");
        }

        if (_store.GetAll().Any(s => s.MacAddress.Equals(mac, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest("a sensor with this mac address is already registered");
        }

        var sensor = new SensorRegistration
        {
            MacAddress = mac,
            Location = location,
            Category = request.Category
        };

        _store.Add(sensor);
        _deviceRegistry.Register(sensor);
        return Ok(sensor);
    }

    [HttpGet]
    public ActionResult<List<SensorRegistration>> GetAll()
    {
        return Ok(_store.GetAll());
    }

    [HttpGet("{id}")]
    public ActionResult<SensorRegistration> Get(string id)
    {
        var sensor = _store.Get(id);
        if (sensor == null)
        {
            return NotFound();
        }
        return Ok(sensor);
    }
}
