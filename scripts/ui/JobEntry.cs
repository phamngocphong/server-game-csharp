using Godot;

namespace ShipperSimulator;

/// <summary>One card on the Job Board.</summary>
public partial class JobEntry : PanelContainer
{
    [Signal] public delegate void AcceptPressedEventHandler(JobData job);

    public JobData? Job { get; private set; }

    private Label _packageLabel = null!;
    private Label _routeLabel = null!;
    private Label _detailLabel = null!;
    private Label _timeLabel = null!;
    private Label _rewardLabel = null!;
    private Button _acceptButton = null!;

    public override void _Ready()
    {
        _packageLabel = GetNode<Label>("%PackageLabel");
        _routeLabel = GetNode<Label>("%RouteLabel");
        _detailLabel = GetNode<Label>("%DetailLabel");
        _timeLabel = GetNode<Label>("%TimeLabel");
        _rewardLabel = GetNode<Label>("%RewardLabel");
        _acceptButton = GetNode<Button>("%AcceptButton");
        _acceptButton.Pressed += OnAcceptButtonPressed;
        if (Job != null)
            Apply();
    }

    public void Setup(JobData job)
    {
        Job = job;
        if (IsNodeReady())
            Apply();
    }

    private void Apply()
    {
        var job = Job!;
        _packageLabel.Text = job.Title;
        _packageLabel.AddThemeColorOverride("font_color", job.PackageColor);
        _routeLabel.Text = $"From: {job.PickupName} ({job.PickupDistrict})\nTo: {job.DeliveryName} ({job.DeliveryDistrict})";
        _detailLabel.Text = $"Pickup {GameManager.FormatDistance(job.DistanceToPickup)} away  -  Trip {GameManager.FormatDistance(job.Distance)}";
        _rewardLabel.Text = GameManager.FormatMoney(job.Reward);

        _timeLabel.Visible = job.IsTimed;
        if (job.IsTimed)
        {
            var penalty = GameManager.Instance.LatePenaltyFor(job);
            var lateRule = penalty >= 1f
                ? "fails if late"
                : $"late: -{Mathf.RoundToInt(penalty * 100f)}%";
            _timeLabel.Text = $"TIMED  Pickup {GameManager.FormatTime(job.PickupTimeLimit)}  -  " +
                $"{(job.IsPassenger ? "Ride" : "Delivery")} {GameManager.FormatTime(job.DeliveryTimeLimit)}  ({lateRule})";
            if (job.HasTip)
                _timeLabel.Text += $"\nTip +{GameManager.FormatMoney(GameManager.Instance.TipFor(job))} if done within {GameManager.FormatTime(job.TipTimeLimit)}";
        }
    }

    private void OnAcceptButtonPressed()
    {
        if (Job != null)
            EmitSignal(SignalName.AcceptPressed, Job);
    }
}
