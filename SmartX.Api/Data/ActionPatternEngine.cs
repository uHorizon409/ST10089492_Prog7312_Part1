using SmartX.Shared;

namespace SmartX.Api.Data;

// one thing the user did, either a search term they typed or a command
// type they issued, used to spot patterns like "pump a" always following
// "moisture sensor b"
public class RecordedAction
{
    public string Label { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

// tracks what the user searches for or triggers, and how often one action
// is quickly followed by another, so it can suggest the likely next action
// before the user goes looking for it
public class ActionPatternEngine
{
    private readonly List<RecordedAction> _history = new();
    private readonly Dictionary<string, Dictionary<string, int>> _followedBy = new();
    private readonly object _lock = new();

    // actions more than this far apart dont count as related
    private static readonly TimeSpan FollowWindow = TimeSpan.FromSeconds(30);

    public void Record(string label)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            if (_history.Count > 0)
            {
                var previous = _history[^1];
                if (now - previous.At <= FollowWindow && previous.Label != label)
                {
                    if (!_followedBy.TryGetValue(previous.Label, out var followers))
                    {
                        followers = new Dictionary<string, int>();
                        _followedBy[previous.Label] = followers;
                    }
                    followers[label] = followers.GetValueOrDefault(label) + 1;
                }
            }

            _history.Add(new RecordedAction { Label = label, At = now });

            // keep the history from growing forever during a long demo
            if (_history.Count > 200)
            {
                _history.RemoveAt(0);
            }
        }
    }

    // the most frequent action that has historically followed the given one
    public List<SuggestedAction> GetPatternSuggestions(string lastAction, int max = 3)
    {
        lock (_lock)
        {
            if (!_followedBy.TryGetValue(lastAction, out var followers))
            {
                return new List<SuggestedAction>();
            }

            return followers
                .OrderByDescending(kv => kv.Value)
                .Take(max)
                .Select(kv => new SuggestedAction
                {
                    Text = kv.Key,
                    Reason = $"followed \"{lastAction}\" {kv.Value} time(s) before"
                })
                .ToList();
        }
    }

    public string? LastAction
    {
        get
        {
            lock (_lock)
            {
                return _history.Count > 0 ? _history[^1].Label : null;
            }
        }
    }
}
