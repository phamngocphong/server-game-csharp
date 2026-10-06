using Godot;

namespace ShipperSimulator;

/// <summary>Modal summary shown after a successful delivery or ride.</summary>
public partial class DeliveryResultPopup : Control
{
    private Label _titleLabel = null!;
    private GridContainer _detailsGrid = null!;
    private Label _rewardLabel = null!;
    private Label _balanceLabel = null!;
    private Button _continueButton = null!;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("%TitleLabel");
        _detailsGrid = GetNode<GridContainer>("%DetailsGrid");
        _rewardLabel = GetNode<Label>("%RewardLabel");
        _balanceLabel = GetNode<Label>("%BalanceLabel");
        _continueButton = GetNode<Button>("%ContinueButton");

        Hide();
        EventBus.Instance.JobDelivered += ShowResult;
        _continueButton.Pressed += Close;
    }

    public override void _ExitTree()
    {
        // C# events of [Signal]s declared in C# are plain delegates: Godot does not
        // disconnect them when this node is freed, so unsubscribe explicitly.
        EventBus.Instance.JobDelivered -= ShowResult;
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
        _titleLabel.Text = job.IsPassenger ? "Ride Complete!" : "Delivery Complete!";
        AddRow(job.IsPassenger ? "Ride" : "Package", job.Title);
        AddRow("From", $"{job.PickupName} ({job.PickupDistrict})");
        AddRow("To", $"{job.DeliveryName} ({job.DeliveryDistrict})");
        AddRow("Distance", GameManager.FormatDistance(job.Distance));
        AddRow("Time", GameManager.FormatTime(result.ElapsedTime));
        if (job.IsTimed)
        {
            AddRow(job.IsPassenger ? "Ride time" : "Delivery time",
                $"{GameManager.FormatTime(result.DeliveryTime)} / {GameManager.FormatTime(job.DeliveryTimeLimit)}");
            AddRow("Status", result.WasLate
                ? $"LATE  (-{GameManager.FormatMoney(result.LatePenaltyAmount)})"
                : "On time", result.WasLate ? new Color(1f, 0.4f, 0.35f) : new Color(0.55f, 1f, 0.6f));
        }
        if (result.Tip > 0)
            AddRow("Tip", $"+{GameManager.FormatMoney(result.Tip)}  fast delivery!", new Color(1f, 0.85f, 0.35f));
        else if (job.HasTip && !result.WasLate)
            AddRow("Tip", $"none (needed {GameManager.FormatTime(job.TipTimeLimit)})", new Color(1f, 1f, 1f, 0.6f));
        AddStarsRow("Customer rating", result.CustomerStars);
        AddRow("Your rating", $"{GameManager.FormatRating(result.RatingBefore)} -> {GameManager.FormatRating(result.RatingAfter)}");
        AddRow("Total deliveries", result.TotalDeliveries.ToString());

        _rewardLabel.Text = result.Tip > 0
            ? $"+{GameManager.FormatMoney(result.Reward + result.Tip)}  (incl. {GameManager.FormatMoney(result.Tip)} tip)"
            : $"+{GameManager.FormatMoney(result.Reward)}";
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

    private void AddStarsRow(string key, int stars)
    {
        _detailsGrid.AddChild(new Label { Text = key, Modulate = new Color(1, 1, 1, 0.65f) });
        _detailsGrid.AddChild(new StarRating { Value = stars, StarSize = 18f, SizeFlagsVertical = SizeFlags.ShrinkCenter });
    }

    private void AddRow(string key, string value, Color? valueColor = null)
    {
        _detailsGrid.AddChild(new Label { Text = key, Modulate = new Color(1, 1, 1, 0.65f) });
        _detailsGrid.AddChild(new Label
        {
            Text = value,
            Modulate = valueColor ?? Colors.White,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
    }
}
