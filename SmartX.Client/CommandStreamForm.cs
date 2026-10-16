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
    private TextBox _searchTextBox = null!;
    private ComboBox _categoryFilterCombo = null!;
    private CheckBox _activeAlertsOnlyCheckBox = null!;
    private ListBox _suggestionsList = null!;
    private ListBox _historyList = null!;
    private List<SensorRegistration> _lastSensors = new();

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

        var searchLabel = new Label { Text = "Search", Location = new Point(10, 70), AutoSize = true };
        _searchTextBox = new TextBox { Location = new Point(10, 90), Width = 220 };

        var categoryLabel = new Label { Text = "Category", Location = new Point(240, 70), AutoSize = true };
        _categoryFilterCombo = new ComboBox
        {
            Location = new Point(240, 90),
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _categoryFilterCombo.Items.Add("All");
        _categoryFilterCombo.Items.AddRange(Enum.GetNames(typeof(SensorCategory)));
        _categoryFilterCombo.SelectedIndex = 0;

        _activeAlertsOnlyCheckBox = new CheckBox { Text = "Active alerts only", Location = new Point(400, 92), AutoSize = true };

        var searchButton = new Button { Text = "Search", Location = new Point(560, 88), Width = 90 };
        searchButton.Click += async (s, e) => await SearchAsync();

        var timelineButton = new Button { Text = "View Timeline", Location = new Point(660, 88), Width = 120 };
        timelineButton.Click += (s, e) => OpenTimelineForSelectedRow();

        _grid = new DataGridView
        {
            Location = new Point(10, 125),
            Width = 910,
            Height = 220,
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
        _grid.Columns.Add("Override", "Override");
        _grid.Columns.Add("Status", "Status");
        _grid.Columns["Id"]!.Visible = false;

        var suggestionsLabel = new Label
        {
            Text = "Suggested Actions / Automated Insights",
            Location = new Point(10, 355),
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _suggestionsList = new ListBox
        {
            Location = new Point(10, 375),
            Width = 910,
            Height = 90,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        var historyLabel = new Label
        {
            Text = "Command History (most recent first)",
            Location = new Point(10, 470),
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _historyList = new ListBox
        {
            Location = new Point(10, 490),
            Width = 910,
            Height = 90,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        Controls.Add(commandLabel);
        Controls.Add(_commandTypeCombo);
        Controls.Add(macLabel);
        Controls.Add(_targetMacTextBox);
        Controls.Add(issueButton);
        Controls.Add(undoButton);
        Controls.Add(_statusLabel);
        Controls.Add(searchLabel);
        Controls.Add(_searchTextBox);
        Controls.Add(categoryLabel);
        Controls.Add(_categoryFilterCombo);
        Controls.Add(_activeAlertsOnlyCheckBox);
        Controls.Add(searchButton);
        Controls.Add(timelineButton);
        Controls.Add(_grid);
        Controls.Add(suggestionsLabel);
        Controls.Add(_suggestionsList);
        Controls.Add(historyLabel);
        Controls.Add(_historyList);
    }

    private async Task SearchAsync()
    {
        if (!string.IsNullOrWhiteSpace(_searchTextBox.Text))
        {
            await _apiClient.RecordActionAsync(_searchTextBox.Text.Trim());
        }

        ApplyFilterToGrid();
        await RefreshSuggestionsAsync();
    }

    private void OpenTimelineForSelectedRow()
    {
        if (_grid.SelectedRows.Count == 0)
        {
            _statusLabel.ForeColor = Color.DarkRed;
            _statusLabel.Text = "select a row first to view its timeline";
            return;
        }

        var row = _grid.SelectedRows[0];
        var sensorId = (string)row.Cells["Id"].Value!;
        var mac = (string)row.Cells["Mac"].Value!;

        using var timeline = new TimelineForm(_apiClient, sensorId, mac);
        timeline.ShowDialog(this);
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
        _lastSensors = await _apiClient.GetSensorsAsync();
        ApplyFilterToGrid();
        await RefreshSuggestionsAsync();
        await RefreshHistoryAsync();
    }

    // filters whatever was last fetched instead of hitting the api again,
    // search and category/alert filters all run against the same snapshot
    private void ApplyFilterToGrid()
    {
        var search = _searchTextBox.Text.Trim();
        var categoryFilter = (string)_categoryFilterCombo.SelectedItem!;
        bool alertsOnly = _activeAlertsOnlyCheckBox.Checked;

        _grid.Rows.Clear();

        foreach (var sensor in _lastSensors)
        {
            bool anomalous = AnomalyChecker.IsReadingAnomalous(sensor.Category, sensor.LastReading);
            bool disconnected = AnomalyChecker.IsDisconnected(sensor.LastSeen, TimeSpan.FromSeconds(30));
            string status = disconnected ? "DISCONNECTED" : anomalous ? "ANOMALY" : "OK";

            if (!string.IsNullOrWhiteSpace(search) &&
                !sensor.MacAddress.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !sensor.Location.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (categoryFilter != "All" && sensor.Category.ToString() != categoryFilter)
            {
                continue;
            }

            if (alertsOnly && !anomalous && !disconnected)
            {
                continue;
            }

            int rowIndex = _grid.Rows.Add(
                sensor.Id,
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

    private async Task RefreshSuggestionsAsync()
    {
        var result = await _apiClient.GetSuggestionsAsync();
        _suggestionsList.Items.Clear();

        foreach (var suggestion in result.PatternSuggestions)
        {
            _suggestionsList.Items.Add($"Suggested: {suggestion.Text} ({suggestion.Reason})");
        }

        foreach (var insight in result.AutomatedInsights)
        {
            _suggestionsList.Items.Add($"Insight: {insight}");
        }

        if (_suggestionsList.Items.Count == 0)
        {
            _suggestionsList.Items.Add("No suggestions yet, search or issue a few commands first");
        }
    }

    private async Task RefreshHistoryAsync()
    {
        var history = await _apiClient.GetCommandHistoryAsync();
        _historyList.Items.Clear();

        foreach (var command in history)
        {
            _historyList.Items.Add(command.ToString());
        }

        if (_historyList.Items.Count == 0)
        {
            _historyList.Items.Add("No commands issued yet");
        }
    }
}
