using SmartX.Shared;

namespace SmartX.Client;

// the "real-time command stream and history" pillar, part 2. live incoming
// data plus manual override commands that actually change what part 1's
// dashboard reports back for actuators
public class CommandStreamForm : Form
{
    private readonly ApiClient _apiClient;
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 2000 };

    private DataGridView _grid = null!;
    private ComboBox _commandTypeCombo = null!;
    private TextBox _targetMacTextBox = null!;
    private Label _statusLabel = null!;

    public CommandStreamForm(ApiClient apiClient)
    {
        _apiClient = apiClient;
        InitializeComponent();

        _refreshTimer.Tick += async (s, e) => await RefreshGridAsync();
        FormClosed += (s, e) => _refreshTimer.Stop();
        Load += async (s, e) =>
        {
            await RefreshGridAsync();
            _refreshTimer.Start();
        };
    }

    private void InitializeComponent()
    {
        Text = "Real-Time Command Stream and History";
        Width = 950;
        Height = 650;
        MinimumSize = new Size(820, 560);
        StartPosition = FormStartPosition.CenterParent;

        var commandLabel = new Label { Text = "Command", Location = new Point(10, 15), AutoSize = true };
        _commandTypeCombo = new ComboBox
        {
            Location = new Point(10, 35),
            Width = 160,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _commandTypeCombo.Items.AddRange(Enum.GetNames(typeof(CommandType)));
        _commandTypeCombo.SelectedIndex = 0;

        var macLabel = new Label { Text = "Target MAC (blank for Shut Down All Valves)", Location = new Point(180, 15), AutoSize = true };
        _targetMacTextBox = new TextBox { Location = new Point(180, 35), Width = 220 };

        var issueButton = new Button { Text = "Issue Command", Location = new Point(410, 34), Width = 130 };
        issueButton.Click += async (s, e) => await IssueCommandAsync();

        var undoButton = new Button { Text = "Undo Last", Location = new Point(550, 34), Width = 100 };
        undoButton.Click += async (s, e) => await UndoLastCommandAsync();

        _statusLabel = new Label
        {
            Location = new Point(660, 38),
            Width = 270,
            AutoSize = false,
            ForeColor = Color.DarkRed
        };

        _grid = new DataGridView
        {
            Location = new Point(10, 70),
            Width = 910,
            Height = 300,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        _grid.Columns.Add("Mac", "MAC Address");
        _grid.Columns.Add("Location", "Location");
        _grid.Columns.Add("Category", "Category");
        _grid.Columns.Add("Reading", "Last Reading");
        _grid.Columns.Add("Override", "Override");
        _grid.Columns.Add("Status", "Status");

        Controls.Add(commandLabel);
        Controls.Add(_commandTypeCombo);
        Controls.Add(macLabel);
        Controls.Add(_targetMacTextBox);
        Controls.Add(issueButton);
        Controls.Add(undoButton);
        Controls.Add(_statusLabel);
        Controls.Add(_grid);
    }

    private async Task IssueCommandAsync()
    {
        var type = Enum.Parse<CommandType>((string)_commandTypeCombo.SelectedItem!);
        var mac = string.IsNullOrWhiteSpace(_targetMacTextBox.Text) ? null : _targetMacTextBox.Text.Trim();

        if (type != CommandType.ShutDownAllValves && mac == null)
        {
            _statusLabel.ForeColor = Color.DarkRed;
            _statusLabel.Text = "target mac is required for this command";
            return;
        }

        var result = await _apiClient.IssueCommandAsync(type, mac);
        if (result.Command == null)
        {
            _statusLabel.ForeColor = Color.DarkRed;
            _statusLabel.Text = result.Error ?? "command failed";
            return;
        }

        _statusLabel.ForeColor = Color.DarkGreen;
        _statusLabel.Text = $"{type} applied to {result.Command.TargetDeviceIds.Count} device(s)";

        await _apiClient.RecordActionAsync(type.ToString());
        await RefreshGridAsync();
    }

    private async Task UndoLastCommandAsync()
    {
        var result = await _apiClient.UndoLastCommandAsync();
        if (result.Command == null)
        {
            _statusLabel.ForeColor = Color.DarkRed;
            _statusLabel.Text = result.Error ?? "nothing to undo";
            return;
        }

        _statusLabel.ForeColor = Color.DarkGreen;
        _statusLabel.Text = $"undone: {result.Command.Type} on {result.Command.TargetDeviceIds.Count} device(s)";
        await RefreshGridAsync();
    }

    private async Task RefreshGridAsync()
    {
        var sensors = await _apiClient.GetSensorsAsync();
        _grid.Rows.Clear();

        foreach (var sensor in sensors)
        {
            bool anomalous = AnomalyChecker.IsReadingAnomalous(sensor.Category, sensor.LastReading);
            bool disconnected = AnomalyChecker.IsDisconnected(sensor.LastSeen, TimeSpan.FromSeconds(30));
            string status = disconnected ? "DISCONNECTED" : anomalous ? "ANOMALY" : "OK";

            int rowIndex = _grid.Rows.Add(
                sensor.MacAddress,
                sensor.Location,
                sensor.Category,
                sensor.LastReading.ToString("F1"),
                sensor.Category == SensorCategory.Actuator ? (sensor.OverrideActive ? "ON" : "OFF") : "-",
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
            }
        }
    }
}
