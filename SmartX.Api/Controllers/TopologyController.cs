using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Data;

namespace SmartX.Api.Controllers;

[ApiController]
[Route("api/topology")]
public class TopologyController : ControllerBase
{
    private readonly SensorHistoryTreeStore _historyTreeStore;

    public TopologyController(SensorHistoryTreeStore historyTreeStore)
    {
        _historyTreeStore = historyTreeStore;
    }

    // range query backed by the avl tree, pulls only the readings between
    // the two tick values for this one sensor, nothing else gets touched
    [HttpGet("{sensorId}/history")]
    public IActionResult GetHistoryRange(string sensorId, [FromQuery] long from, [FromQuery] long to)
    {
        if (to < from)
        {
            return BadRequest("to must not be earlier than from");
        }

        var readings = _historyTreeStore.RangeQuery(sensorId, from, to);
        return Ok(readings);
    }
}
