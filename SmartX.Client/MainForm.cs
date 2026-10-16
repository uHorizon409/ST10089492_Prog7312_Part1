namespace SmartX.Client;

// startup menu, the three pillars from the brief, only the first one is
// wired up for part 1, the other two get built later in the poe
public class MainForm : Form
{
    private readonly ApiClient _apiClient = new(AppConfig.GetApiBaseUrl());

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
        using var dashboard = new DashboardForm(_apiClient);
        dashboard.ShowDialog(this);
    }

    private void CommandButton_Click(object? sender, EventArgs e)
    {
        using var commandStream = new CommandStreamForm(_apiClient);
        commandStream.ShowDialog(this);
    }
}
