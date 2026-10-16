using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Data;
using SmartX.Shared;

namespace SmartX.Api.Controllers;

[ApiController]
[Route("api/actions")]
public class ActionsController : ControllerBase
{
    private readonly ActionPatternEngine _patternEngine;
    private readonly SensorStore _sensorStore;
    private readonly DeviceAlertTracker _alertTracker;

    public ActionsController(ActionPatternEngine patternEngine, SensorStore sensorStore, DeviceAlertTracker alertTracker)
    {
        _patternEngine = patternEngine;
        _sensorStore = sensorStore;
        _alertTracker = alertTracker;
    }

    public class RecordActionRequest
    {
        public string Label { get; set; } = "";
    }

    // called whenever the user searches for something on the command stream page
    [HttpPost("record")]
    public IActionResult Record(RecordActionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return BadRequest("label is required");
        }

        _patternEngine.Record(request.Label);
        return Ok();
    }

    [HttpGet("suggestions")]
    public ActionResult<SuggestionsResult> GetSuggestions()
    {
        var response = new SuggestionsResult();

        var lastAction = _patternEngine.LastAction;
        if (lastAction != null)
        {
            response.PatternSuggestions = _patternEngine.GetPatternSuggestions(lastAction);
        }

        // flag every device currently sitting in an alert state, proactively,
        // before the user has to go looking for the problem themselves, and
        // if the pattern engine has already learned what usually gets done
        // about this exact device, recommend that fix right alongside the flag
        foreach (var sensor in _sensorStore.GetAll())
        {
            if (!_alertTracker.IsActive(sensor.Id))
            {
                continue;
            }

            var insight = $"{sensor.MacAddress} ({sensor.Location}) is out of range, last reading {sensor.LastReading:F1}";

            var learnedFix = _patternEngine.GetPatternSuggestions(sensor.MacAddress, max: 1);
            if (learnedFix.Count > 0)
            {
                insight += $" -> try {learnedFix[0].Text} ({learnedFix[0].Reason})";
            }

            response.AutomatedInsights.Add(insight);
        }

        return Ok(response);
    }
}
