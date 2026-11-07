namespace SmartX.Api.Data;

// weighted undirected graph modelling the mesh, vertices are sensor mac
// addresses plus one virtual gateway vertex, edges are simulated wireless
// links weighted by power cost
public class MeshNetworkGraph
{
    public const string GatewayId = "GATEWAY";

    private readonly Dictionary<string, List<(string Neighbor, int Weight)>> _adjacency = new();

    public IReadOnlyCollection<string> Vertices => _adjacency.Keys;

    public void AddVertex(string id)
    {
        if (!_adjacency.ContainsKey(id))
        {
            _adjacency[id] = new List<(string, int)>();
        }
    }

    public void AddEdge(string a, string b, int weight)
    {
        AddVertex(a);
        AddVertex(b);
        _adjacency[a].Add((b, weight));
        _adjacency[b].Add((a, weight));
    }

    public IReadOnlyList<(string Neighbor, int Weight)> GetNeighbors(string id)
    {
        return _adjacency.TryGetValue(id, out var neighbors) ? neighbors : new List<(string, int)>();
    }

    // every vertex this reaches from the gateway counts as up, anything
    // left out never got an edge when the graph was built, which is what
    // happens to a sensor thats currently offline
    public List<string> Bfs(string start)
    {
        var visited = new List<string>();
        if (!_adjacency.ContainsKey(start))
        {
            return visited;
        }

        var seen = new HashSet<string> { start };
        var queue = new Queue<string>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            visited.Add(current);

            foreach (var (neighbor, _) in GetNeighbors(current))
            {
                if (seen.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        return visited;
    }

    public List<string> Dfs(string start)
    {
        var visited = new List<string>();
        if (!_adjacency.ContainsKey(start))
        {
            return visited;
        }

        DfsVisit(start, new HashSet<string>(), visited);
        return visited;
    }

    private void DfsVisit(string current, HashSet<string> seen, List<string> visited)
    {
        if (!seen.Add(current))
        {
            return;
        }

        visited.Add(current);

        foreach (var (neighbor, _) in GetNeighbors(current))
        {
            DfsVisit(neighbor, seen, visited);
        }
    }

    // prim's algorithm starting from the gateway, always grows the tree by
    // the cheapest edge reachable from whats already in it, this is the
    // most power efficient backbone for telemetry to hop through
    public (List<(string From, string To, int Weight)> Edges, int TotalWeight) ComputeMst(string start)
    {
        var edges = new List<(string From, string To, int Weight)>();

        if (!_adjacency.ContainsKey(start))
        {
            return (edges, 0);
        }

        var inTree = new HashSet<string> { start };
        int totalWeight = 0;

        while (inTree.Count < Vertices.Count)
        {
            string? bestFrom = null;
            string? bestTo = null;
            int bestWeight = int.MaxValue;

            foreach (var vertex in inTree)
            {
                foreach (var (neighbor, weight) in GetNeighbors(vertex))
                {
                    if (!inTree.Contains(neighbor) && weight < bestWeight)
                    {
                        bestWeight = weight;
                        bestFrom = vertex;
                        bestTo = neighbor;
                    }
                }
            }

            if (bestTo == null)
            {
                // nothing left reachable from the current tree, the
                // remaining vertices are disconnected from the gateway
                break;
            }

            edges.Add((bestFrom!, bestTo, bestWeight));
            inTree.Add(bestTo);
            totalWeight += bestWeight;
        }

        return (edges, totalWeight);
    }
}
