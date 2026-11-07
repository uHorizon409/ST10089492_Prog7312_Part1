namespace SmartX.Shared;

public class TopologyGraphResult
{
    public List<MeshNodeDto> Nodes { get; set; } = new();
    public List<MeshEdgeDto> Edges { get; set; } = new();
}
