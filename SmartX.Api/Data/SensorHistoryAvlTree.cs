namespace SmartX.Api.Data;

// avl tree keyed by timestamp ticks so a single sensor's history can be
// range queried in log time instead of scanning every reading ever stored,
// self balances on every insert so it never degrades into a linked list
public class SensorHistoryAvlTree
{
    private class Node
    {
        public long Key;
        public SmartX.Shared.QueuedTelemetry Value = null!;
        public Node? Left;
        public Node? Right;
        public int Height = 1;
    }

    private Node? _root;
    private readonly object _lock = new();

    public void Insert(long key, SmartX.Shared.QueuedTelemetry value)
    {
        lock (_lock)
        {
            _root = Insert(_root, key, value);
        }
    }

    private static Node Insert(Node? node, long key, SmartX.Shared.QueuedTelemetry value)
    {
        if (node == null)
        {
            return new Node { Key = key, Value = value };
        }

        // ticks colliding on the same key is practically impossible, bump
        // by one the same way TimestampedLogStore does if it ever happens
        if (key == node.Key)
        {
            key++;
        }

        if (key < node.Key)
        {
            node.Left = Insert(node.Left, key, value);
        }
        else
        {
            node.Right = Insert(node.Right, key, value);
        }

        return Rebalance(node);
    }

    private static Node Rebalance(Node node)
    {
        node.Height = 1 + Math.Max(Height(node.Left), Height(node.Right));
        int balance = Height(node.Left) - Height(node.Right);

        if (balance > 1)
        {
            if (Height(node.Left!.Left) < Height(node.Left.Right))
            {
                node.Left = RotateLeft(node.Left);
            }
            return RotateRight(node);
        }

        if (balance < -1)
        {
            if (Height(node.Right!.Right) < Height(node.Right.Left))
            {
                node.Right = RotateRight(node.Right);
            }
            return RotateLeft(node);
        }

        return node;
    }

    private static Node RotateRight(Node node)
    {
        var newRoot = node.Left!;
        node.Left = newRoot.Right;
        newRoot.Right = node;
        node.Height = 1 + Math.Max(Height(node.Left), Height(node.Right));
        newRoot.Height = 1 + Math.Max(Height(newRoot.Left), Height(newRoot.Right));
        return newRoot;
    }

    private static Node RotateLeft(Node node)
    {
        var newRoot = node.Right!;
        node.Right = newRoot.Left;
        newRoot.Left = node;
        node.Height = 1 + Math.Max(Height(node.Left), Height(node.Right));
        newRoot.Height = 1 + Math.Max(Height(newRoot.Left), Height(newRoot.Right));
        return newRoot;
    }

    private static int Height(Node? node) => node?.Height ?? 0;

    // in-order traversal that skips subtrees entirely outside the range
    // instead of visiting every node, this is the actual point of the tree
    public List<SmartX.Shared.QueuedTelemetry> RangeQuery(long fromKey, long toKey)
    {
        lock (_lock)
        {
            var results = new List<SmartX.Shared.QueuedTelemetry>();
            RangeQuery(_root, fromKey, toKey, results);
            return results;
        }
    }

    private static void RangeQuery(Node? node, long fromKey, long toKey, List<SmartX.Shared.QueuedTelemetry> results)
    {
        if (node == null)
        {
            return;
        }

        if (node.Key > fromKey)
        {
            RangeQuery(node.Left, fromKey, toKey, results);
        }

        if (node.Key >= fromKey && node.Key <= toKey)
        {
            results.Add(node.Value);
        }

        if (node.Key < toKey)
        {
            RangeQuery(node.Right, fromKey, toKey, results);
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return CountNodes(_root);
            }
        }
    }

    private static int CountNodes(Node? node) => node == null ? 0 : 1 + CountNodes(node.Left) + CountNodes(node.Right);
}
