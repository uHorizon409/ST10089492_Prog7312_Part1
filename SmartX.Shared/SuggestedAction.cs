namespace SmartX.Shared;

public class SuggestedAction
{
    public string Text { get; set; } = "";
    public string Reason { get; set; } = "";
}

// what the suggestions endpoint hands back, pattern based guesses plus
// anything currently flagged as an anomaly
public class SuggestionsResult
{
    public List<SuggestedAction> PatternSuggestions { get; set; } = new();
    public List<string> AutomatedInsights { get; set; } = new();
}
