namespace SmartX.Shared;

// one vertex in the mesh graph, either a real sensor or the virtual gateway
public class MeshNodeDto
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public bool Online { get; set; }
    public bool IsGateway { get; set; }
}
