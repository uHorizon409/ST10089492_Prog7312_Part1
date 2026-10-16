using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Data;
using SmartX.Shared;

namespace SmartX.Api.Controllers;

[ApiController]
[Route("api/commands")]
public class CommandsController : ControllerBase
{
    private readonly SensorStore _sensorStore;
    private readonly DeviceRegistry _deviceRegistry;
    private readonly CommandHistoryStack _history;
    private readonly ActionPatternEngine _patternEngine;

    public CommandsController(
        SensorStore sensorStore,
        DeviceRegistry deviceRegistry,
        CommandHistoryStack history,
        ActionPatternEngine patternEngine)
    {
        _sensorStore = sensorStore;
        _deviceRegistry = deviceRegistry;
        _history = history;
        _patternEngine = patternEngine;
    }

    public class IssueCommandRequest
    {
        public CommandType Type { get; set; }

        // not needed for shut down all valves, required for the single device commands
        public string? TargetMacAddress { get; set; }
    }

    [HttpPost]
    public ActionResult<DeviceCommand> Issue(IssueCommandRequest request)
    {
        List<SensorRegistration> targets;

        if (request.Type == CommandType.ShutDownAllValves)
        {
            // bulk command, hits every actuator currently registered
            targets = _sensorStore.GetAll().Where(s => s.Category == SensorCategory.Actuator).ToList();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.TargetMacAddress))
            {
                return BadRequest("target mac address is required for this command");
            }

            // mac keyed lookup, this is the device registry actually earning its place
            var device = _deviceRegistry.FindByMac(request.TargetMacAddress);
            if (device == null)
            {
                return NotFound("no device registered with that mac address");
            }

            if (device.Category != SensorCategory.Actuator)
            {
                return BadRequest("override commands only apply to actuator devices");
            }

            targets = new List<SensorRegistration> { device };
        }

        if (targets.Count == 0)
        {
            return BadRequest("no matching devices to command");
        }

        var command = new DeviceCommand { Type = request.Type };

        foreach (var device in targets)
        {
            command.TargetDeviceIds.Add(device.Id);
            command.PreviousStates[device.Id] = device.OverrideActive;

            device.OverrideActive = request.Type switch
            {
                CommandType.TriggerPump => true,
                CommandType.ToggleRelay => !device.OverrideActive,
                CommandType.ShutDownAllValves => false,
                _ => device.OverrideActive
            };
        }

        _history.Push(command);

        // feeds the predictive engine, so a command counts as an action just
        // like a search does for working out what tends to follow what
        _patternEngine.Record(request.Type.ToString());

        return Ok(command);
    }

    [HttpPost("undo")]
    public ActionResult<DeviceCommand> Undo()
    {
        var command = _history.Pop();
        if (command == null)
        {
            return NotFound("no commands to undo");
        }

        foreach (var (deviceId, previousState) in command.PreviousStates)
        {
            var device = _sensorStore.Get(deviceId);
            if (device != null)
            {
                device.OverrideActive = previousState;
            }
        }

        return Ok(command);
    }

    [HttpGet("history")]
    public ActionResult<List<DeviceCommand>> GetHistory()
    {
        return Ok(_history.PeekAll());
    }
}
