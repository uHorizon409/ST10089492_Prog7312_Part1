using SmartX.Shared;

namespace SmartX.Client;

// shows one sensor's history as an actual line graph, pulled straight from
// the sorted dictionary log store on the api side so the points already
// arrive in timestamp order, nothing gets sorted client side
public class TimelineForm : Form
{
    private readonly ApiClient _apiClient;
    private readonly string _sensorId;
    private readonly Panel _chartPanel = null!;
    private Label _summaryLabel = null!;
    private List<QueuedTelemetry> _points = new();

    public TimelineForm(ApiClient apiClient, string sensorId, string sensorLabel)
    {
        _apiClient = apiClient;
        _sensorId = sensorId;

        Text = $"Timeline - {sensorLabel}";
        Width = 700;
        Height = 420;
        MinimumSize = new Size(460, 300);
        StartPosition = FormStartPosition.CenterParent;

        var refreshButton = new Button { Text = "Refresh", Location = new Point(10, 10), Width = 100 };
        refreshButton.Click += async (s, e) => await LoadAsync();

        _summaryLabel = new Label { Location = new Point(120, 15), Width = 540, AutoSize = false };

        _chartPanel = new Panel
        {
            Location = new Point(10, 45),
            Width = 660,
            Height = 320,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _chartPanel.Paint += ChartPanel_Paint;

        Controls.Add(refreshButton);
        Controls.Add(_summaryLabel);
        Controls.Add(_chartPanel);

        Load += async (s, e) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _points = await _apiClient.GetTimelineAsync(_sensorId);
        _summaryLabel.Text = _points.Count == 0
            ? "No readings yet for this sensor"
            : $"{_points.Count} reading(s), oldest {_points[0].QueuedAt:HH:mm:ss}, newest {_points[^1].QueuedAt:HH:mm:ss}";
        _chartPanel.Invalidate();
    }

    private void ChartPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        var bounds = _chartPanel.ClientRectangle;
        const int margin = 30;

        if (_points.Count < 2)
        {
            g.DrawString("Not enough data yet to draw a line", Font, Brushes.Gray, margin, margin);
            return;
        }

        double minValue = _points.Min(p => p.Value);
        double maxValue = _points.Max(p => p.Value);
        if (Math.Abs(maxValue - minValue) < 0.0001)
        {
            // flat line, still draw it, just give it some room above and below
            maxValue += 1;
            minValue -= 1;
        }

        int plotWidth = bounds.Width - margin * 2;
        int plotHeight = bounds.Height - margin * 2;

        float XFor(int index) => margin + (float)index / (_points.Count - 1) * plotWidth;
        float YFor(double value) => margin + plotHeight - (float)((value - minValue) / (maxValue - minValue) * plotHeight);

        using var axisPen = new Pen(Color.Black, 1);
        g.DrawLine(axisPen, margin, margin, margin, margin + plotHeight);
        g.DrawLine(axisPen, margin, margin + plotHeight, margin + plotWidth, margin + plotHeight);

        g.DrawString(maxValue.ToString("F1"), Font, Brushes.Black, 2, margin - 6);
        g.DrawString(minValue.ToString("F1"), Font, Brushes.Black, 2, margin + plotHeight - 6);

        using var linePen = new Pen(Color.SteelBlue, 2);
        for (int i = 0; i < _points.Count - 1; i++)
        {
            g.DrawLine(linePen, XFor(i), YFor(_points[i].Value), XFor(i + 1), YFor(_points[i + 1].Value));
        }

        using var pointBrush = new SolidBrush(Color.DarkRed);
        for (int i = 0; i < _points.Count; i++)
        {
            float x = XFor(i);
            float y = YFor(_points[i].Value);
            g.FillEllipse(pointBrush, x - 2.5f, y - 2.5f, 5, 5);
        }
    }
}
