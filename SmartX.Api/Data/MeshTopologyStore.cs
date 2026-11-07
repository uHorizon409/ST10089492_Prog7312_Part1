using SmartX.Shared;

namespace SmartX.Api.Data;

// builds the live mesh graph fresh from whichever sensors are currently
// online, an offline sensor gets no edges at all so bfs/dfs and the mst
// both stop reaching it automatically the moment it goes quiet, and pick
// it back up automatically once it starts reporting again
public class MeshTopologyStore
{
    private static readonly TimeSpan DisconnectTimeout = TimeSpan.FromSeconds(30);

    private readonly SensorStore _sensorStore;

    public MeshTopologyStore(SensorStore sensorStore)
    {
        _sensorStore = sensorStore;
    }

    public (MeshNetworkGraph Graph, List<SensorRegistration> Online, List<SensorRegistration> Offline) BuildGraph()
    {
        var graph = new MeshNetworkGraph();
        graph.AddVertex(MeshNetworkGraph.GatewayId);

        var all = _sensorStore.GetAll();
        var online = new List<SensorRegistration>();
        var offline = new List<SensorRegistration>();

        foreach (var sensor in all)
        {
            if (AnomalyChecker.IsDisconnected(sensor.LastSeen, DisconnectTimeout))
            {
                offline.Add(sensor);
                continue;
            }

            online.Add(sensor);
            graph.AddVertex(sensor.MacAddress);
        }

        // every online sensor can reach the gateway directly and can also
        // hop through any other online sensor, a real deployment would only
        // have some of these links, this simplified complete graph just
        // gives prim's a genuine power cost tradeoff to choose between
        for (int i = 0; i < online.Count; i++)
        {
            graph.AddEdge(online[i].MacAddress, MeshNetworkGraph.GatewayId, SignalCost(online[i].MacAddress, MeshNetworkGraph.GatewayId));

            for (int j = i + 1; j < online.Count; j++)
            {
                graph.AddEdge(online[i].MacAddress, online[j].MacAddress, SignalCost(online[i].MacAddress, online[j].MacAddress));
            }
        }

        return (graph, online, offline);
    }

    // stable across runs so the same pair of devices always costs the same
    // to link, the topology only actually changes when a device goes
    // offline or comes back, not every time this gets called
    private static int SignalCost(string macA, string macB)
    {
        var pairKey = string.CompareOrdinal(macA, macB) < 0 ? macA + macB : macB + macA;

        int hash = 0;
        foreach (char c in pairKey)
        {
            hash = hash * 31 + c;
        }

        return Math.Abs(hash % 10) + 1;
    }
}
