namespace SmartX.Client;

// startup menu, the three pillars from the brief, only the first one is
// wired up for part 1, the other two get built later in the poe
public class MainForm : Form
{
    private readonly ApiClient _apiClient = new(AppConfig.GetApiBaseUrl());

    // kept non-modal and tracked here so both pillars can be open and
    // updating live at the same time instead of forcing close then reopen
    // to switch between them, each one still pulls its own state from the
    // api fresh so nothing is lost either way, this just makes switching
    // between them not block each other
    private DashboardForm? _dashboardForm;
    private CommandStreamForm? _commandStreamForm;

    public MainForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Smart-X Gateway";
        Width = 500;
        Height = 400;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(420, 320);

        var title = new Label
        {
            Text = "Smart-X IoT Mesh Gateway",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 20)
        };

        var ingestionButton = new Button
        {
            Text = "Sensor Data Ingestion and Telemetry",
            Location = new Point(20, 80),
            Width = 400,
            Height = 50,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        ingestionButton.Click += IngestionButton_Click;

        var commandButton = new Button
        {
            Text = "Real-Time Command Stream and History",
            Location = new Point(20, 150),
            Width = 400,
            Height = 50,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        commandButton.Click += CommandButton_Click;

        var meshButton = new Button
        {
            Text = "Network Topology and Mesh Routing (Coming Soon)",
            Location = new Point(20, 220),
            Width = 400,
            Height = 50,
            Enabled = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        Controls.Add(title);
        Controls.Add(ingestionButton);
        Controls.Add(commandButton);
        Controls.Add(meshButton);
    }

    private void IngestionButton_Click(object? sender, EventArgs e)
    {
        if (_dashboardForm == null || _dashboardForm.IsDisposed)
        {
            _dashboardForm = new DashboardForm(_apiClient);
            _dashboardForm.FormClosed += (s, e) => _dashboardForm = null;

            // offset from the main menu so it doesnt land exactly on top of
            // the command stream window if both are opened
            _dashboardForm.StartPosition = FormStartPosition.Manual;
            _dashboardForm.Location = new Point(Location.X + 40, Location.Y + 40);

            _dashboardForm.Show(this);
        }
        else
        {
            _dashboardForm.Activate();
        }
    }

    private void CommandButton_Click(object? sender, EventArgs e)
    {
        if (_commandStreamForm == null || _commandStreamForm.IsDisposed)
        {
            _commandStreamForm = new CommandStreamForm(_apiClient);
            _commandStreamForm.FormClosed += (s, e) => _commandStreamForm = null;

            // offset the other way so both windows are visible side by side
            // instead of stacked on top of each other
            _commandStreamForm.StartPosition = FormStartPosition.Manual;
            _commandStreamForm.Location = new Point(Location.X + 110, Location.Y + 90);

            _commandStreamForm.Show(this);
        }
        else
        {
            _commandStreamForm.Activate();
        }
    }
}
