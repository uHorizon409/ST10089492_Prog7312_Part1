using SmartX.Shared;

namespace SmartX.Client;

// this is the "sensor data ingestion and telemetry" pillar, shows every
// registered sensor and colour codes the row when something looks wrong,
// that colour coding is the dynamic engagement feature from the research
public class DashboardForm : Form
{
    private readonly ApiClient _apiClient;
    private readonly System.Windows.Forms.Timer _simulationTimer = new() { Interval = 2000 };
    private readonly Random _random = new();
    private readonly TelemetryHistoryStore _localHistory = new();
    private DataGridView _grid = null!;
    private Label _healthScoreLabel = null!;
    private ListBox _packetLog = null!;

    public DashboardForm(ApiClient apiClient)
    {
        _apiClient = apiClient;
        InitializeComponent();

        _simulationTimer.Tick += async (s, e) => await SimulateTelemetryAsync();
        FormClosed += (s, e) => _simulationTimer.Stop();
        Load += async (s, e) => await SeedSensorsAsync();
    }

    private void InitializeComponent()
    {
        Text = "Sensor Data Ingestion and Telemetry";
        Width = 900;
        Height = 550;
        StartPosition = FormStartPosition.CenterParent;

        var registerButton = new Button { Text = "Register New Sensor", Location = new Point(10, 10), Width = 160 };
        registerButton.Click += RegisterButton_Click;

        var refreshButton = new Button { Text = "Refresh", Location = new Point(180, 10), Width = 100 };
        refreshButton.Click += async (s, e) => await RefreshGridAsync();

        var aggregateButton = new Button { Text = "Aggregate Power Load", Location = new Point(290, 10), Width = 160 };
        aggregateButton.Click += async (s, e) => await ShowAggregatePowerLoadAsync();

        _healthScoreLabel = new Label
        {
            Text = "System Health: --",
            Location = new Point(680, 15),
            AutoSize = true,
            Font = new Font("Segoe UI", 11, FontStyle.Bold)
        };

        _grid = new DataGridView
        {
            Location = new Point(10, 50),
            Width = 870,
            Height = 330,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        _grid.Columns.Add("Id", "Sensor Id");
        _grid.Columns.Add("Mac", "MAC Address");
        _grid.Columns.Add("Location", "Location");
        _grid.Columns.Add("Category", "Category");
        _grid.Columns.Add("Reading", "Last Reading");
        _grid.Columns.Add("Status", "Status");
        _grid.Columns["Id"]!.Visible = false;

        var packetLogLabel = new Label
        {
            Text = "Recent Telemetry Packets (TelemetryPacket<T> per sensor type)",
            Location = new Point(10, 390),
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

        _packetLog = new ListBox
        {
            Location = new Point(10, 410),
            Width = 870,
            Height = 110,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        Controls.Add(registerButton);
        Controls.Add(refreshButton);
        Controls.Add(aggregateButton);
        Controls.Add(_healthScoreLabel);
        Controls.Add(_grid);
        Controls.Add(packetLogLabel);
        Controls.Add(_packetLog);
    }

    // adds up every power consumption sensor's last reading using the
    // SensorMeter + operator, same idea as Meter3 = Meter1 + Meter2 from the brief
    private async Task ShowAggregatePowerLoadAsync()
    {
        var sensors = await _apiClient.GetSensorsAsync();
        var powerSensors = sensors.Where(s => s.Category == SensorCategory.PowerConsumption).ToList();

        if (powerSensors.Count == 0)
        {
            MessageBox.Show(this, "No power consumption sensors registered yet.", "Aggregate Power Load");
            return;
        }

        SensorMeter total = new(powerSensors[0].MacAddress, powerSensors[0].LastReading);
        for (int i = 1; i < powerSensors.Count; i++)
        {
            var nextMeter = new SensorMeter(powerSensors[i].MacAddress, powerSensors[i].LastReading);
            total = total + nextMeter;
        }

        MessageBox.Show(
            this,
            $"Combined load across {powerSensors.Count} power sensors: {total.Reading:F1} W",
            "Aggregate Power Load"
        );
    }

    private async void RegisterButton_Click(object? sender, EventArgs e)
    {
        using var form = new SensorRegistrationForm(_apiClient);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await RefreshGridAsync();
        }
    }

    private async Task SeedSensorsAsync()
    {
        // heavily seed the dashboard with mock sensors, the brief wants proof
        // the data structures cope with more than just one or two rows
        var seedData = new (string mac, string location, SensorCategory category)[]
        {
            ("AA:BB:CC:00:00:01", "Facility A > Zone 1 > Sub-Zone A", SensorCategory.Environmental),
            ("AA:BB:CC:00:00:02", "Facility A > Zone 1 > Sub-Zone B", SensorCategory.PowerConsumption),
            ("AA:BB:CC:00:00:03", "Facility A > Zone 2 > Sub-Zone A", SensorCategory.Actuator),
            ("AA:BB:CC:00:00:04", "Facility B > Zone 1 > Sub-Zone A", SensorCategory.Environmental),
            ("AA:BB:CC:00:00:05", "Facility B > Zone 2 > Sub-Zone C", SensorCategory.PowerConsumption),
        };

        var existing = await _apiClient.GetSensorsAsync();
        if (existing.Count == 0)
        {
            foreach (var seed in seedData)
            {
                await _apiClient.RegisterSensorAsync(new ApiClient.RegisterSensorRequest
                {
                    MacAddress = seed.mac,
                    Location = seed.location,
                    Category = seed.category
                });
            }
        }

        await RefreshGridAsync();
        _simulationTimer.Start();
    }

    private async Task SimulateTelemetryAsync()
    {
        var sensors = await _apiClient.GetSensorsAsync();

        foreach (var sensor in sensors)
        {
            double reading = GenerateMockReading(sensor);

            // every now and then send a spike so anomaly highlighting has
            // something real to flag while testing
            if (_random.NextDouble() < 0.15)
            {
                reading *= 5;
            }

            await _apiClient.SendTelemetryAsync(sensor.Id, reading);

            // keep a local jagged history too, same idea as the api side
            // TelemetryHistoryStore, batch first then flatten into a List<T>
            _localHistory.AddBatch(sensor.Id, new[] { reading });

            // build the actual typed packet for this category, float/int/bool
            // all go through the same generic wrapper without getting boxed
            _packetLog.Items.Insert(0, BuildTypedPacket(sensor, reading));
            if (_packetLog.Items.Count > 30)
            {
                _packetLog.Items.RemoveAt(_packetLog.Items.Count - 1);
            }
        }

        await RefreshGridAsync();
    }

    // environmental sensors report a float, power sensors report an int,
    // actuators report a bool, TelemetryPacket<T> handles all three the same way
    private static string BuildTypedPacket(SensorRegistration sensor, double rawValue)
    {
        return sensor.Category switch
        {
            SensorCategory.Environmental => new TelemetryPacket<float>(sensor.MacAddress, (float)rawValue).ToString(),
            SensorCategory.PowerConsumption => new TelemetryPacket<int>(sensor.MacAddress, (int)rawValue).ToString(),
            SensorCategory.Actuator => new TelemetryPacket<bool>(sensor.MacAddress, rawValue != 0).ToString(),
            _ => new TelemetryPacket<double>(sensor.MacAddress, rawValue).ToString()
        };
    }

    private double GenerateMockReading(SensorRegistration sensor)
    {
        return sensor.Category switch
        {
            SensorCategory.Environmental => _random.Next(10, 90),
            SensorCategory.PowerConsumption => _random.Next(200, 3000),
            // actuators report whatever a command last set them to instead of
            // a random value, this is what makes part 2 overrides visible here
            SensorCategory.Actuator => sensor.OverrideActive ? 1 : 0,
            _ => 0
        };
    }

    private async Task RefreshGridAsync()
    {
        var sensors = await _apiClient.GetSensorsAsync();
        _grid.Rows.Clear();

        int healthyCount = 0;

        foreach (var sensor in sensors)
        {
            bool anomalous = AnomalyChecker.IsReadingAnomalous(sensor.Category, sensor.LastReading);
            bool disconnected = AnomalyChecker.IsDisconnected(sensor.LastSeen, TimeSpan.FromSeconds(30));

            string status = disconnected ? "DISCONNECTED" : anomalous ? "ANOMALY" : "OK";

            int rowIndex = _grid.Rows.Add(
                sensor.Id,
                sensor.MacAddress,
                sensor.Location,
                sensor.Category,
                sensor.LastReading.ToString("F1"),
                status
            );

            var row = _grid.Rows[rowIndex];
            if (disconnected)
            {
                row.DefaultCellStyle.BackColor = Color.Gray;
                row.DefaultCellStyle.ForeColor = Color.White;
            }
            else if (anomalous)
            {
                row.DefaultCellStyle.BackColor = Color.MistyRose;
                row.DefaultCellStyle.ForeColor = Color.DarkRed;
            }
            else
            {
                row.DefaultCellStyle.BackColor = Color.Honeydew;
                healthyCount++;
            }
        }

        int score = sensors.Count == 0 ? 100 : (int)(100.0 * healthyCount / sensors.Count);
        _healthScoreLabel.Text = $"System Health: {score}%";
        _healthScoreLabel.ForeColor = score >= 80 ? Color.DarkGreen : score >= 50 ? Color.DarkOrange : Color.DarkRed;
    }
}
