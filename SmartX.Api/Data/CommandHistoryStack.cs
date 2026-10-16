using SmartX.Shared;

namespace SmartX.Api.Data;

// keeps every manual override command so the most recent one can be undone,
// last in first out is exactly what a stack is for here
public class CommandHistoryStack
{
    private readonly Stack<DeviceCommand> _commands = new();
    private readonly object _lock = new();

    public void Push(DeviceCommand command)
    {
        lock (_lock)
        {
            _commands.Push(command);
        }
    }

    public DeviceCommand? Pop()
    {
        lock (_lock)
        {
            return _commands.TryPop(out var command) ? command : null;
        }
    }

    // non destructive, used to show the history list without undoing anything
    public List<DeviceCommand> PeekAll()
    {
        lock (_lock)
        {
            return _commands.ToList();
        }
    }

    public int Count
    {
        get { lock (_lock) { return _commands.Count; } }
    }
}
