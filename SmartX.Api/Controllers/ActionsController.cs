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
    private readonly TimestampedLogStore _logStore;

    public ActionsController(
        ActionPatternEngine patternEngine,
        SensorStore sensorStore,
        DeviceAlertTracker alertTracker,
        TimestampedLogStore logStore)
    {
        _patternEngine = patternEngine;
        _sensorStore = sensorStore;
        _alertTracker = alertTracker;
        _logStore = logStore;
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

        foreach (var sensor in _sensorStore.GetAll())
        {
            if (_alertTracker.IsActive(sensor.Id))
            {
                // flag every device currently sitting in an alert state, proactively,
                // before the user has to go looking for the problem themselves, and
                // if the pattern engine has already learned what usually gets done
                // about this exact device, recommend that fix right alongside the flag
                var insight = $"{sensor.MacAddress} ({sensor.Location}) is out of range, last reading {sensor.LastReading:F1}";

                var learnedFix = _patternEngine.GetPatternSuggestions(sensor.MacAddress, max: 1);
                if (learnedFix.Count > 0)
                {
                    insight += $" -> try {learnedFix[0].Text} ({learnedFix[0].Reason})";
                }

                response.AutomatedInsights.Add(insight);
                continue;
            }

            // not anomalous yet, but check if its last few readings are
            // heading toward the edge of the normal range, this is what
            // makes an insight proactive instead of only reacting after
            // something has already gone wrong
            var recent = _logStore.GetTimelineFor(sensor.Id).Select(r => r.Value).ToList();
            if (AnomalyChecker.IsTrendingTowardAnomaly(sensor.Category, recent))
            {
                response.AutomatedInsights.Add(
                    $"{sensor.MacAddress} ({sensor.Location}) is trending toward its limit, last reading {sensor.LastReading:F1}, worth checking before it becomes a full alert");
            }
        }

        return Ok(response);
    }
}
