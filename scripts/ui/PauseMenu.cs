using System.Globalization;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// In-game pause menu (Esc / P). Pauses the whole scene tree; this node keeps running
/// (ProcessMode.Always) to handle its buttons: resume, save, save + main menu, save + quit.
/// Warns that a job in progress is not saved.
/// </summary>
public partial class PauseMenu : Control
{
    [Export(PropertyHint.File, "*.tscn")] public string MainMenuScenePath { get; set; } = "res://ui/main_menu.tscn";

    private Label _summaryLabel = null!;
    private Label _statusLabel = null!;
    private Label _warningLabel = null!;
    private Button _resumeButton = null!;
    private bool _hasActiveJob;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _summaryLabel = GetNode<Label>("%SummaryLabel");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _warningLabel = GetNode<Label>("%WarningLabel");
        _resumeButton = GetNode<Button>("%ResumeButton");
        _resumeButton.Pressed += Close;
        GetNode<Button>("%SaveButton").Pressed += OnSavePressed;
        GetNode<Button>("%MainMenuButton").Pressed += OnMainMenuPressed;
        GetNode<Button>("%QuitButton").Pressed += OnQuitPressed;
        EventBus.Instance.JobStateChanged += OnJobStateChanged;
        Hide();
    }

    public override void _ExitTree()
    {
        // C# events of [Signal]s declared in C# are plain delegates: Godot does not
        // disconnect them when this node is freed, so unsubscribe explicitly.
        EventBus.Instance.JobStateChanged -= OnJobStateChanged;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("pause"))
            return;
        GetViewport().SetInputAsHandled();
        if (Visible)
            Close();
        else
            Open();
    }

    public void Open()
    {
        GetTree().Paused = true;
        _statusLabel.Text = "";
        _warningLabel.Visible = _hasActiveJob;
        RefreshSummary();
        Show();
        _resumeButton.GrabFocus();
    }

    public void Close()
    {
        Hide();
        GetTree().Paused = false;
    }

    private void RefreshSummary()
    {
        var gm = GameManager.Instance;
        var city = gm.CityMap?.Region.DisplayName ?? "";
        var weather = gm.Weather?.Current.DisplayName ?? "";
        var fuel = gm.Player != null
            ? $"{gm.Player.Fuel.ToString("0.0", CultureInfo.InvariantCulture)} / {gm.Player.FuelCapacity.ToString("0.0", CultureInfo.InvariantCulture)} L"
            : "-";
        _summaryLabel.Text =
            $"{city}  -  {weather}\n" +
            $"Balance {GameManager.FormatMoney(gm.Wallet.Balance)}   Deliveries {gm.Stats.TotalDeliveries}\n" +
            $"Rating {GameManager.FormatRating(gm.Reputation.Rating)} / 5   Fuel {fuel}   Played {GameManager.FormatTime(gm.Stats.PlayTime)}";
    }

    private void OnSavePressed()
    {
        var ok = SaveManager.Instance.SaveGame(silent: true);
        _statusLabel.Text = ok
            ? $"Game saved at {Time.GetTimeStringFromSystem()[..5]}"
            : "Could not save the game (see the Output panel)";
    }

    private void OnMainMenuPressed()
    {
        SaveManager.Instance.SaveGame(silent: true);
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(MainMenuScenePath);
    }

    private void OnQuitPressed()
    {
        SaveManager.Instance.SaveGame(silent: true);
        GetTree().Quit();
    }

    private void OnJobStateChanged(int state, JobData job) =>
        _hasActiveJob = (JobManager.State)state != JobManager.State.Idle;
}
