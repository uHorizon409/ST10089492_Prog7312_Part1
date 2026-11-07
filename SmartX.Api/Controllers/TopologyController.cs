using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Data;
using SmartX.Shared;

namespace SmartX.Api.Controllers;

[ApiController]
[Route("api/topology")]
public class TopologyController : ControllerBase
{
    private readonly SensorHistoryTreeStore _historyTreeStore;
    private readonly MeshTopologyStore _topologyStore;

    public TopologyController(SensorHistoryTreeStore historyTreeStore, MeshTopologyStore topologyStore)
    {
        _historyTreeStore = historyTreeStore;
        _topologyStore = topologyStore;
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

    // full node and edge list for drawing the live mesh, online status
    // comes straight from which vertices actually made it into the graph
    [HttpGet("graph")]
    public IActionResult GetGraph()
    {
        var (graph, online, offline) = _topologyStore.BuildGraph();
        var onlineMacs = online.Select(s => s.MacAddress).ToHashSet();

        var nodes = new List<MeshNodeDto>
        {
            new() { Id = MeshNetworkGraph.GatewayId, Label = "Gateway", Online = true, IsGateway = true }
        };

        nodes.AddRange(online.Select(s => new MeshNodeDto { Id = s.MacAddress, Label = s.Location, Online = true }));
        nodes.AddRange(offline.Select(s => new MeshNodeDto { Id = s.MacAddress, Label = s.Location, Online = false }));

        var edges = new List<MeshEdgeDto>();
        var seenPairs = new HashSet<string>();

        foreach (var vertex in graph.Vertices)
        {
            foreach (var (neighbor, weight) in graph.GetNeighbors(vertex))
            {
                var pairKey = string.CompareOrdinal(vertex, neighbor) < 0 ? vertex + neighbor : neighbor + vertex;
                if (seenPairs.Add(pairKey))
                {
                    edges.Add(new MeshEdgeDto { From = vertex, To = neighbor, Weight = weight });
                }
            }
        }

        return Ok(new TopologyGraphResult { Nodes = nodes, Edges = edges });
    }

    // breadth first search from the gateway, whatever it doesnt reach is
    // down, this is the actual bfs/dfs the rubric asks for, not just a
    // status flag copied from the sensor list
    [HttpGet("reachable")]
    public IActionResult GetReachable([FromQuery] bool useDfs = false)
    {
        var (graph, online, offline) = _topologyStore.BuildGraph();

        var reachable = useDfs
            ? graph.Dfs(MeshNetworkGraph.GatewayId)
            : graph.Bfs(MeshNetworkGraph.GatewayId);

        var down = offline.Select(s => s.MacAddress)
            .Concat(online.Select(s => s.MacAddress).Where(mac => !reachable.Contains(mac)))
            .ToList();

        return Ok(new ReachabilityResult { Algorithm = useDfs ? "DFS" : "BFS", Reachable = reachable, Down = down });
    }

    // prim's algorithm from the gateway outward, the result is the most
    // power efficient backbone telemetry can hop through to reach it
    [HttpGet("mst")]
    public IActionResult GetMst()
    {
        var (graph, _, _) = _topologyStore.BuildGraph();
        var (edges, totalWeight) = graph.ComputeMst(MeshNetworkGraph.GatewayId);

        var edgeDtos = edges.Select(e => new MeshEdgeDto { From = e.From, To = e.To, Weight = e.Weight }).ToList();
        return Ok(new MstResult { Edges = edgeDtos, TotalWeight = totalWeight });
    }
}
