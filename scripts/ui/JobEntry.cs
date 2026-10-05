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
    private Label _rewardLabel = null!;
    private Button _acceptButton = null!;

    public override void _Ready()
    {
        _packageLabel = GetNode<Label>("%PackageLabel");
        _routeLabel = GetNode<Label>("%RouteLabel");
        _detailLabel = GetNode<Label>("%DetailLabel");
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
        _packageLabel.Text = job.PackageName;
        _packageLabel.AddThemeColorOverride("font_color", job.PackageColor);
        _routeLabel.Text = $"From: {job.PickupName} ({job.PickupDistrict})\nTo: {job.DeliveryName} ({job.DeliveryDistrict})";
        _detailLabel.Text = $"Pickup {GameManager.FormatDistance(job.DistanceToPickup)} away  -  Trip {GameManager.FormatDistance(job.Distance)}";
        _rewardLabel.Text = GameManager.FormatMoney(job.Reward);
    }

    private void OnAcceptButtonPressed()
    {
        if (Job != null)
            EmitSignal(SignalName.AcceptPressed, Job);
    }
}
