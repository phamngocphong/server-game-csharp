using Godot;

namespace ShipperSimulator;

/// <summary>
/// Always-on HUD: wallet, current district, active job, interaction prompt,
/// speedometer and toast notifications.
/// </summary>
public partial class Hud : Control
{
    private Label _balanceLabel = null!;
    private Label _deliveriesLabel = null!;
    private Label _districtLabel = null!;
    private Control _activeJobPanel = null!;
    private Label _jobStatusLabel = null!;
    private Label _jobTargetLabel = null!;
    private Label _jobInfoLabel = null!;
    private Label _jobDistanceLabel = null!;
    private Button _cancelButton = null!;
    private Control _promptPanel = null!;
    private Label _promptLabel = null!;
    private Label _speedLabel = null!;
    private Control _toastPanel = null!;
    private Label _toastLabel = null!;

    private Vector2 _target;
    private bool _hasTarget;
    private Tween? _toastTween;

    public override void _Ready()
    {
        _balanceLabel = GetNode<Label>("%BalanceLabel");
        _deliveriesLabel = GetNode<Label>("%DeliveriesLabel");
        _districtLabel = GetNode<Label>("%DistrictLabel");
        _activeJobPanel = GetNode<Control>("%ActiveJobPanel");
        _jobStatusLabel = GetNode<Label>("%JobStatusLabel");
        _jobTargetLabel = GetNode<Label>("%JobTargetLabel");
        _jobInfoLabel = GetNode<Label>("%JobInfoLabel");
        _jobDistanceLabel = GetNode<Label>("%JobDistanceLabel");
        _cancelButton = GetNode<Button>("%CancelButton");
        _promptPanel = GetNode<Control>("%PromptPanel");
        _promptLabel = GetNode<Label>("%PromptLabel");
        _speedLabel = GetNode<Label>("%SpeedLabel");
        _toastPanel = GetNode<Control>("%ToastPanel");
        _toastLabel = GetNode<Label>("%ToastLabel");

        var bus = EventBus.Instance;
        bus.BalanceChanged += OnBalanceChanged;
        bus.StatsChanged += RefreshStats;
        bus.JobStateChanged += OnJobStateChanged;
        bus.NavigationTargetChanged += OnTargetChanged;
        bus.NavigationTargetCleared += OnTargetCleared;
        bus.InteractionPromptChanged += OnPromptChanged;
        bus.NotificationRequested += ShowToast;
        _cancelButton.Pressed += OnCancelPressed;

        _activeJobPanel.Hide();
        _promptPanel.Hide();
        _toastPanel.Hide();
        OnBalanceChanged(GameManager.Instance.Wallet.Balance, 0);
        RefreshStats();
    }

    public override void _Process(double delta)
    {
        var player = GameManager.Instance.Player;
        if (player == null)
            return;
        _speedLabel.Text = $"{Mathf.RoundToInt(player.GetSpeedKmh())} km/h";
        _districtLabel.Text = GameManager.Instance.GetCurrentDistrict()?.DisplayName ?? "";
        if (_hasTarget && _activeJobPanel.Visible)
        {
            var distance = JobGenerator.RouteDistance(player.GlobalPosition, _target);
            _jobDistanceLabel.Text = $"Distance: {GameManager.FormatDistance(distance)}";
        }
    }

    public void ShowToast(string text)
    {
        _toastLabel.Text = text;
        _toastPanel.Show();
        _toastPanel.Modulate = Colors.White;
        _toastTween?.Kill();
        _toastTween = CreateTween();
        _toastTween.TweenInterval(1.8);
        _toastTween.TweenProperty(_toastPanel, "modulate:a", 0.0, 0.4);
        _toastTween.TweenCallback(Callable.From(_toastPanel.Hide));
    }

    private void OnBalanceChanged(int balance, int delta)
    {
        _balanceLabel.Text = GameManager.FormatMoney(balance);
        if (delta > 0)
        {
            _balanceLabel.Modulate = new Color(0.4f, 1f, 0.5f);
            CreateTween().TweenProperty(_balanceLabel, "modulate", Colors.White, 0.8);
        }
    }

    private void RefreshStats() =>
        _deliveriesLabel.Text = $"Deliveries: {GameManager.Instance.Stats.TotalDeliveries}";

    private void OnJobStateChanged(int state, JobData job)
    {
        switch ((JobManager.State)state)
        {
            case JobManager.State.ToPickup:
                _jobStatusLabel.Text = "GO TO PICKUP";
                _jobTargetLabel.Text = $"{job.PickupName} ({job.PickupDistrict})";
                break;
            case JobManager.State.ToDelivery:
                _jobStatusLabel.Text = "DELIVER TO";
                _jobTargetLabel.Text = $"{job.DeliveryName} ({job.DeliveryDistrict})";
                break;
            default:
                _activeJobPanel.Hide();
                return;
        }
        _activeJobPanel.Show();
        _jobInfoLabel.Text = $"{job.PackageName}  -  Reward {GameManager.FormatMoney(job.Reward)}";
        _jobStatusLabel.AddThemeColorOverride("font_color", job.PackageColor);
    }

    private void OnTargetChanged(Vector2 target, string label, Color color)
    {
        _target = target;
        _hasTarget = true;
    }

    private void OnTargetCleared() => _hasTarget = false;

    private void OnPromptChanged(string text)
    {
        _promptLabel.Text = text;
        _promptPanel.Visible = !string.IsNullOrEmpty(text);
    }

    private void OnCancelPressed() => EventBus.Instance.EmitSignal(EventBus.SignalName.JobCancelRequested);
}
