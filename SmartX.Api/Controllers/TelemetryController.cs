using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Data;
using SmartX.Shared;

namespace SmartX.Api.Controllers;

[ApiController]
[Route("api/telemetry")]
public class TelemetryController : ControllerBase
{
    private readonly SensorStore _sensorStore;
    private readonly TelemetryHistoryStore _historyStore;
    private readonly TelemetryIngestionQueue _queue;

    public TelemetryController(SensorStore sensorStore, TelemetryHistoryStore historyStore, TelemetryIngestionQueue queue)
    {
        _sensorStore = sensorStore;
        _historyStore = historyStore;
        _queue = queue;
    }

    public class TelemetryReadingRequest
    {
        public string SensorId { get; set; } = "";
        public double Value { get; set; }
    }

    public class TelemetryBatchRequest
    {
        public string SensorId { get; set; } = "";
        public double[] Readings { get; set; } = Array.Empty<double>();
    }

    [HttpPost]
    public IActionResult Ingest(TelemetryReadingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SensorId))
        {
            return BadRequest("sensor id is required");
        }

        if (double.IsNaN(request.Value) || double.IsInfinity(request.Value))
        {
            return BadRequest("value must be a real number");
        }

        var sensor = _sensorStore.Get(request.SensorId);
        if (sensor == null)
        {
            return NotFound("sensor not registered");
        }

        // dont process inline, drop it on the queue and return straight away,
        // the background service drains it, critical alerts jump the standard queue
        bool critical = CriticalAlertChecker.IsCritical(sensor.Category, request.Value);
        _queue.Enqueue(new QueuedTelemetry { SensorId = request.SensorId, Value = request.Value }, critical);

        return Ok(new { request.SensorId, request.Value, Critical = critical });
    }

    [HttpPost("batch")]
    public IActionResult IngestBatch(TelemetryBatchRequest request)
    {
        var sensor = _sensorStore.Get(request.SensorId);
        if (sensor == null)
        {
            return NotFound("sensor not registered");
        }

        foreach (var value in request.Readings)
        {
            bool critical = CriticalAlertChecker.IsCritical(sensor.Category, value);
            _queue.Enqueue(new QueuedTelemetry { SensorId = request.SensorId, Value = value }, critical);
        }

        return Ok(new { request.SensorId, Count = request.Readings.Length });
    }

    [HttpGet("{sensorId}/history")]
    public ActionResult<List<double>> GetHistory(string sensorId)
    {
        return Ok(_historyStore.GetHistory(sensorId));
    }

    [HttpGet("queue-status")]
    public IActionResult GetQueueStatus()
    {
        return Ok(new { Standard = _queue.StandardCount, Critical = _queue.CriticalCount });
    }
}
