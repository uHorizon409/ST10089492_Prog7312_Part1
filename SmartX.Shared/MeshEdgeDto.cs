namespace SmartX.Shared;

// one wireless link in the mesh graph, weight is the simulated power cost
// of a packet hopping across this specific link
public class MeshEdgeDto
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public int Weight { get; set; }
}
