using SmartX.Shared;

namespace SmartX.Client;

// the mesh pillar, draws the live graph from the api, tracks a chosen
// sensor's route to the gateway along the mst, and pulls a sensor's
// historical readings for a chosen time window out of the avl tree
public class NetworkTopologyForm : Form
{
    private readonly ApiClient _apiClient;
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 2000 };

    private ComboBox _routeSensorCombo = null!;
    private Button _trackButton = null!;
    private Label _routeResultLabel = null!;
    private Panel _diagramPanel = null!;
    private Label _summaryLabel = null!;

    private ComboBox _historySensorCombo = null!;
    private DateTimePicker _fromPicker = null!;
    private DateTimePicker _toPicker = null!;
    private Button _loadHistoryButton = null!;
    private DataGridView _historyGrid = null!;

    private List<SensorRegistration> _sensors = new();
    private List<MeshNodeDto> _nodes = new();
    private List<MeshEdgeDto> _edges = new();
    private List<MeshEdgeDto> _mstEdges = new();
    private string? _trackedNodeId;
    private List<string>? _highlightedPath;

    public NetworkTopologyForm(ApiClient apiClient)
    {
        _apiClient = apiClient;
        InitializeComponent();

        _refreshTimer.Tick += async (s, e) => await RefreshTopologyAsync();
        FormClosed += (s, e) => _refreshTimer.Stop();
        Load += async (s, e) =>
        {
            await RefreshTopologyAsync();
            _refreshTimer.Start();
        };
    }

    private void InitializeComponent()
    {
        Text = "Network Topology and Mesh Routing";
        Width = 1000;
        Height = 760;
        MinimumSize = new Size(860, 640);
        StartPosition = FormStartPosition.CenterParent;

        var routeLabel = new Label { Text = "Track Routing Path", Location = new Point(10, 15), AutoSize = true };
        _routeSensorCombo = new ComboBox { Location = new Point(10, 35), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };

        _trackButton = new Button { Text = "Track", Location = new Point(240, 34), Width = 90 };
        _trackButton.Click += (s, e) => TrackSelectedRoute();

        _routeResultLabel = new Label { Location = new Point(340, 15), Width = 630, Height = 55, AutoSize = false };

        _summaryLabel = new Label { Location = new Point(10, 70), Width = 960, AutoSize = true, ForeColor = Color.DimGray };

        _diagramPanel = new Panel
        {
            Location = new Point(10, 95),
            Width = 960,
            Height = 330,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _diagramPanel.Paint += DiagramPanel_Paint;

        var historyLabel = new Label { Text = "Historical Log", Location = new Point(10, 435), AutoSize = true };
        _historySensorCombo = new ComboBox { Location = new Point(10, 455), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };

        var fromLabel = new Label { Text = "From", Location = new Point(240, 435), AutoSize = true };
        _fromPicker = new DateTimePicker
        {
            Location = new Point(240, 455),
            Width = 170,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd HH:mm:ss",
            Value = DateTime.Now.AddHours(-1)
        };

        var toLabel = new Label { Text = "To", Location = new Point(420, 435), AutoSize = true };
        _toPicker = new DateTimePicker
        {
            Location = new Point(420, 455),
            Width = 170,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd HH:mm:ss",
            Value = DateTime.Now
        };

        _loadHistoryButton = new Button { Text = "Load History", Location = new Point(600, 453), Width = 120 };
        _loadHistoryButton.Click += async (s, e) => await LoadHistoryAsync();

        _historyGrid = new DataGridView
        {
            Location = new Point(10, 490),
            Width = 960,
            Height = 220,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        _historyGrid.Columns.Add("Timestamp", "Timestamp");
        _historyGrid.Columns.Add("Value", "Value");

        Controls.Add(routeLabel);
        Controls.Add(_routeSensorCombo);
        Controls.Add(_trackButton);
        Controls.Add(_routeResultLabel);
        Controls.Add(_summaryLabel);
        Controls.Add(_diagramPanel);
        Controls.Add(historyLabel);
        Controls.Add(_historySensorCombo);
        Controls.Add(fromLabel);
        Controls.Add(_fromPicker);
        Controls.Add(toLabel);
        Controls.Add(_toPicker);
        Controls.Add(_loadHistoryButton);
        Controls.Add(_historyGrid);
    }

    private async Task RefreshTopologyAsync()
    {
        _sensors = await _apiClient.GetSensorsAsync();
        RefreshSensorCombo(_routeSensorCombo);
        RefreshSensorCombo(_historySensorCombo);

        var graph = await _apiClient.GetTopologyGraphAsync();
        _nodes = graph.Nodes;
        _edges = graph.Edges;

        var mst = await _apiClient.GetMstAsync();
        _mstEdges = mst.Edges;

        var reachability = await _apiClient.GetReachabilityAsync();
        _summaryLabel.Text = $"{reachability.Reachable.Count - 1} sensor(s) up, {reachability.Down.Count} down, mst power cost {mst.TotalWeight}";

        // keep tracking the same node across a refresh if its still selected,
        // the route can genuinely change if the mst recomputed differently
        if (_trackedNodeId != null)
        {
            TrackRoute(_trackedNodeId);
        }

        _diagramPanel.Invalidate();
    }

    private void RefreshSensorCombo(ComboBox combo)
    {
        var selected = combo.SelectedItem as string;
        combo.Items.Clear();

        foreach (var sensor in _sensors)
        {
            combo.Items.Add(sensor.MacAddress);
        }

        if (selected != null && combo.Items.Contains(selected))
        {
            combo.SelectedItem = selected;
        }
        else if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
    }

    private void TrackSelectedRoute()
    {
        if (_routeSensorCombo.SelectedItem is not string mac)
        {
            _routeResultLabel.Text = "pick a sensor first";
            return;
        }

        TrackRoute(mac);
    }

    private void TrackRoute(string mac)
    {
        _trackedNodeId = mac;
        var parentMap = BuildMstParentMap(_mstEdges, MeshTopologyConstants.GatewayId);
        var path = GetPathToGateway(parentMap, mac);

        if (path == null)
        {
            _routeResultLabel.Text = $"{mac} is currently offline, no route to the gateway";
            _highlightedPath = null;
            _diagramPanel.Invalidate();
            return;
        }

        int totalCost = 0;
        for (int i = 0; i < path.Count - 1; i++)
        {
            totalCost += GetEdgeWeight(_mstEdges, path[i], path[i + 1]);
        }

        _routeResultLabel.Text = $"{string.Join(" -> ", path)}  (power cost {totalCost})";
        _highlightedPath = path;
        _diagramPanel.Invalidate();
    }

    private static Dictionary<string, string?> BuildMstParentMap(List<MeshEdgeDto> mstEdges, string root)
    {
        var adjacency = new Dictionary<string, List<string>>();

        foreach (var edge in mstEdges)
        {
            if (!adjacency.TryGetValue(edge.From, out var fromList))
            {
                fromList = new List<string>();
                adjacency[edge.From] = fromList;
            }
            if (!adjacency.TryGetValue(edge.To, out var toList))
            {
                toList = new List<string>();
                adjacency[edge.To] = toList;
            }
            fromList.Add(edge.To);
            toList.Add(edge.From);
        }

        var parent = new Dictionary<string, string?> { [root] = null };
        var queue = new Queue<string>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var neighbors))
            {
                continue;
            }

            foreach (var neighbor in neighbors)
            {
                if (!parent.ContainsKey(neighbor))
                {
                    parent[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return parent;
    }

    private static List<string>? GetPathToGateway(Dictionary<string, string?> parentMap, string nodeId)
    {
        if (!parentMap.ContainsKey(nodeId))
        {
            return null;
        }

        var path = new List<string>();
        string? current = nodeId;

        while (current != null)
        {
            path.Add(current);
            current = parentMap[current];
        }

        return path;
    }

    private static int GetEdgeWeight(List<MeshEdgeDto> edges, string a, string b)
    {
        var edge = edges.FirstOrDefault(e => (e.From == a && e.To == b) || (e.From == b && e.To == a));
        return edge?.Weight ?? 0;
    }

    private async Task LoadHistoryAsync()
    {
        if (_historySensorCombo.SelectedItem is not string mac)
        {
            return;
        }

        var sensor = _sensors.FirstOrDefault(s => s.MacAddress == mac);
        if (sensor == null)
        {
            return;
        }

        long fromTicks = _fromPicker.Value.ToUniversalTime().Ticks;
        long toTicks = _toPicker.Value.ToUniversalTime().Ticks;

        var readings = await _apiClient.GetHistoryRangeAsync(sensor.Id, fromTicks, toTicks);

        _historyGrid.Rows.Clear();
        foreach (var reading in readings)
        {
            _historyGrid.Rows.Add(reading.QueuedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), reading.Value.ToString("F1"));
        }
    }

    private Dictionary<string, PointF> ComputeLayout(Rectangle bounds)
    {
        var positions = new Dictionary<string, PointF>();
        var center = new PointF(bounds.Width / 2f, bounds.Height / 2f);

        var gateway = _nodes.FirstOrDefault(n => n.IsGateway);
        if (gateway != null)
        {
            positions[gateway.Id] = center;
        }

        var others = _nodes.Where(n => !n.IsGateway).ToList();
        float radius = Math.Min(bounds.Width, bounds.Height) / 2f - 60;
        if (radius < 40)
        {
            radius = 40;
        }

        for (int i = 0; i < others.Count; i++)
        {
            double angle = 2 * Math.PI * i / Math.Max(others.Count, 1);
            float x = center.X + radius * (float)Math.Cos(angle);
            float y = center.Y + radius * (float)Math.Sin(angle);
            positions[others[i].Id] = new PointF(x, y);
        }

        return positions;
    }

    private void DiagramPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        if (_nodes.Count == 0)
        {
            g.DrawString("No sensors registered yet", Font, Brushes.Gray, 20, 20);
            return;
        }

        var positions = ComputeLayout(_diagramPanel.ClientRectangle);

        using (var meshPen = new Pen(Color.LightGray, 1))
        {
            foreach (var edge in _edges)
            {
                if (positions.TryGetValue(edge.From, out var from) && positions.TryGetValue(edge.To, out var to))
                {
                    g.DrawLine(meshPen, from, to);
                }
            }
        }

        using (var mstPen = new Pen(Color.SeaGreen, 3))
        {
            foreach (var edge in _mstEdges)
            {
                if (positions.TryGetValue(edge.From, out var from) && positions.TryGetValue(edge.To, out var to))
                {
                    g.DrawLine(mstPen, from, to);
                }
            }
        }

        if (_highlightedPath != null)
        {
            using var pathPen = new Pen(Color.OrangeRed, 4);
            for (int i = 0; i < _highlightedPath.Count - 1; i++)
            {
                if (positions.TryGetValue(_highlightedPath[i], out var from) && positions.TryGetValue(_highlightedPath[i + 1], out var to))
                {
                    g.DrawLine(pathPen, from, to);
                }
            }
        }

        foreach (var node in _nodes)
        {
            if (!positions.TryGetValue(node.Id, out var pos))
            {
                continue;
            }

            var color = node.IsGateway ? Color.Black : node.Online ? Color.SteelBlue : Color.Firebrick;
            float size = node.IsGateway ? 22 : 16;

            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, pos.X - size / 2, pos.Y - size / 2, size, size);

            var label = node.IsGateway ? "Gateway" : node.Id;
            g.DrawString(label, Font, Brushes.Black, pos.X + size / 2 + 2, pos.Y - 7);
        }
    }
}
