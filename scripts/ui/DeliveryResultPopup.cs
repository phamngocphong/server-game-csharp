using Godot;

namespace ShipperSimulator;

/// <summary>Modal summary shown after a successful delivery.</summary>
public partial class DeliveryResultPopup : Control
{
    private GridContainer _detailsGrid = null!;
    private Label _rewardLabel = null!;
    private Label _balanceLabel = null!;
    private Button _continueButton = null!;

    public override void _Ready()
    {
        _detailsGrid = GetNode<GridContainer>("%DetailsGrid");
        _rewardLabel = GetNode<Label>("%RewardLabel");
        _balanceLabel = GetNode<Label>("%BalanceLabel");
        _continueButton = GetNode<Button>("%ContinueButton");

        Hide();
        EventBus.Instance.JobDelivered += ShowResult;
        _continueButton.Pressed += Close;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("interact"))
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
    }

    public void ShowResult(DeliveryResult result)
    {
        foreach (var child in _detailsGrid.GetChildren())
            child.QueueFree();

        var job = result.Job;
        AddRow("Package", job.PackageName);
        AddRow("From", $"{job.PickupName} ({job.PickupDistrict})");
        AddRow("To", $"{job.DeliveryName} ({job.DeliveryDistrict})");
        AddRow("Distance", GameManager.FormatDistance(job.Distance));
        AddRow("Time", GameManager.FormatTime(result.ElapsedTime));
        AddRow("Total deliveries", result.TotalDeliveries.ToString());

        _rewardLabel.Text = $"+{GameManager.FormatMoney(result.Reward)}";
        _balanceLabel.Text = $"Balance: {GameManager.FormatMoney(result.NewBalance)}";

        Show();
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerControlsLocked, true);
        _continueButton.GrabFocus();
    }

    private void Close()
    {
        if (!Visible)
            return;
        Hide();
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerControlsLocked, false);
        EventBus.Instance.EmitSignal(EventBus.SignalName.DeliveryPopupClosed);
    }

    private void AddRow(string key, string value)
    {
        _detailsGrid.AddChild(new Label { Text = key, Modulate = new Color(1, 1, 1, 0.65f) });
        _detailsGrid.AddChild(new Label
        {
            Text = value,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
    }
}
