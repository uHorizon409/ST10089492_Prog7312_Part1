namespace SmartX.Shared;

public class ReachabilityResult
{
    public string Algorithm { get; set; } = "";
    public List<string> Reachable { get; set; } = new();
    public List<string> Down { get; set; } = new();
}
