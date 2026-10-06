using System.Linq;
using Godot;
using Godot.Collections;

namespace ShipperSimulator;

/// <summary>
/// Side panel listing available jobs, plus earnings history and stats.
/// Toggle with Tab / J.
/// </summary>
public partial class JobBoard : Control
{
    private const string JobEntryScenePath = "res://ui/job_entry.tscn";
    private const int HistoryLines = 5;

    private PackedScene _jobEntryScene = null!;
    private bool _hasActiveJob;

    private Control _panel = null!;
    private Label _subtitleLabel = null!;
    private Label _noticeLabel = null!;
    private VBoxContainer _jobList = null!;
    private Label _emptyLabel = null!;
    private Button _refreshButton = null!;
    private Button _closeButton = null!;
    private Label _statsLabel = null!;
    private Label _historyLabel = null!;

    public override void _Ready()
    {
        _jobEntryScene = GD.Load<PackedScene>(JobEntryScenePath);
        _panel = GetNode<Control>("%Panel");
        _subtitleLabel = GetNode<Label>("%SubtitleLabel");
        _noticeLabel = GetNode<Label>("%NoticeLabel");
        _jobList = GetNode<VBoxContainer>("%JobList");
        _emptyLabel = GetNode<Label>("%EmptyLabel");
        _refreshButton = GetNode<Button>("%RefreshButton");
        _closeButton = GetNode<Button>("%CloseButton");
        _statsLabel = GetNode<Label>("%StatsLabel");
        _historyLabel = GetNode<Label>("%HistoryLabel");

        var bus = EventBus.Instance;
        bus.JobsUpdated += OnJobsUpdated;
        bus.JobBoardOpenRequested += Open;
        bus.JobStateChanged += OnJobStateChanged;
        bus.BalanceChanged += OnBalanceChanged;
        bus.StatsChanged += RefreshHistory;
        bus.ReputationChanged += RefreshHistory;
        bus.JobBoardNoticeChanged += OnNoticeChanged;
        _refreshButton.Pressed += OnRefreshPressed;
        _closeButton.Pressed += Close;

        _panel.Hide();
        UpdateEmptyState(0);
    }

    public override void _ExitTree()
    {
        // C# events of [Signal]s declared in C# are plain delegates: Godot does not
        // disconnect them when this node is freed, so unsubscribe explicitly.
        var bus = EventBus.Instance;
        bus.JobsUpdated -= OnJobsUpdated;
        bus.JobBoardOpenRequested -= Open;
        bus.JobStateChanged -= OnJobStateChanged;
        bus.BalanceChanged -= OnBalanceChanged;
        bus.StatsChanged -= RefreshHistory;
        bus.ReputationChanged -= RefreshHistory;
        bus.JobBoardNoticeChanged -= OnNoticeChanged;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("toggle_job_board"))
            return;
        GetViewport().SetInputAsHandled();
        if (_panel.Visible)
            Close();
        else
            Open();
    }

    public void Open()
    {
        _panel.Show();
        RefreshHistory();
        EventBus.Instance.EmitSignal(EventBus.SignalName.JobBoardOpened);
    }

    public void Close() => _panel.Hide();

    private void OnJobsUpdated(Array<JobData> jobs)
    {
        foreach (var child in _jobList.GetChildren())
            child.QueueFree();
        foreach (var job in jobs)
        {
            var entry = _jobEntryScene.Instantiate<JobEntry>();
            _jobList.AddChild(entry);
            entry.Setup(job);
            entry.AcceptPressed += OnAcceptPressed;
        }
        var district = GameManager.Instance.GetCurrentDistrict();
        _subtitleLabel.Text = $"Jobs near {district?.DisplayName ?? "you"}";
        UpdateEmptyState(jobs.Count);
    }

    private void OnAcceptPressed(JobData job)
    {
        EventBus.Instance.EmitSignal(EventBus.SignalName.JobAcceptRequested, job);
        Close();
    }

    private void OnJobStateChanged(int state, JobData job)
    {
        _hasActiveJob = (JobManager.State)state != JobManager.State.Idle;
        UpdateEmptyState(_jobList.GetChildCount());
    }

    private void UpdateEmptyState(int jobCount)
    {
        _refreshButton.Disabled = _hasActiveJob;
        if (_hasActiveJob)
        {
            _emptyLabel.Text = "You already have an active job.\nFinish or cancel it to take a new one.";
            _emptyLabel.Show();
        }
        else if (jobCount == 0)
        {
            _emptyLabel.Text = "No jobs available. Try refreshing.";
            _emptyLabel.Show();
        }
        else
        {
            _emptyLabel.Hide();
        }
    }

    private void OnBalanceChanged(int balance, int delta) => RefreshHistory();

    private void OnNoticeChanged(string text)
    {
        _noticeLabel.Text = text;
        _noticeLabel.Visible = text.Length > 0;
    }

    private void OnRefreshPressed() => EventBus.Instance.EmitSignal(EventBus.SignalName.JobRefreshRequested);

    private void RefreshHistory()
    {
        var stats = GameManager.Instance.Stats;
        var rep = GameManager.Instance.Reputation;
        _statsLabel.Text =
            $"Rating: {GameManager.FormatRating(rep.Rating)} / 5   Pay x{rep.RewardMultiplier.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}\n" +
            $"On time: {rep.TotalOnTime}   Late: {rep.TotalLate}   Failed: {rep.TotalFailed}   Cancelled: {rep.TotalCancelled}\n" +
            $"Deliveries: {stats.TotalDeliveries}   Earned: {GameManager.FormatMoney(stats.TotalEarned)}   " +
            $"Tips: {GameManager.FormatMoney(stats.TotalTips)}\nBest: {GameManager.FormatMoney(stats.BestReward)}   " +
            $"Play time: {GameManager.FormatTime(stats.PlayTime)}";

        var lines = GameManager.Instance.Wallet.History
            .Take(HistoryLines)
            .Select(e => $"+{GameManager.FormatMoney(e.Amount)}  {e.Description}")
            .ToList();
        _historyLabel.Text = lines.Count > 0 ? string.Join("\n", lines) : "No earnings yet.";
    }
}
