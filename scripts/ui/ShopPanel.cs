using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Vehicle shop, opened from the pause menu (the game stays paused). Shows the current
/// vehicle with its trade-in value, the countdown to the next rotation (00:00 / 08:00 / 16:00)
/// and one card per offer. Buying trades in the current vehicle (<see cref="GameManager.BuyVehicle"/>).
/// </summary>
public partial class ShopPanel : Control
{
    /// <summary>Rotation key to show instead of the real clock (testing a rotation in the editor); empty = clock.</summary>
    [Export] public string RotationOverride { get; set; } = "";

    private Label _balanceLabel = null!;
    private Label _rotationLabel = null!;
    private Label _currentLabel = null!;
    private Label _messageLabel = null!;
    private GridContainer _offersGrid = null!;
    private ConfirmationDialog _confirmDialog = null!;
    private string _shownRotation = "";
    private VehicleOffer? _pendingOffer;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _balanceLabel = GetNode<Label>("%BalanceLabel");
        _rotationLabel = GetNode<Label>("%RotationLabel");
        _currentLabel = GetNode<Label>("%CurrentLabel");
        _messageLabel = GetNode<Label>("%MessageLabel");
        _offersGrid = GetNode<GridContainer>("%OffersGrid");
        _confirmDialog = GetNode<ConfirmationDialog>("%ConfirmBuy");
        GetNode<Button>("%CloseButton").Pressed += Close;
        _confirmDialog.Confirmed += OnConfirmed;
        EventBus.Instance.ShopRequested += Open;
        Hide();
    }

    public override void _ExitTree()
    {
        // C# events of [Signal]s declared in C# are plain delegates: Godot does not
        // disconnect them when this node is freed, so unsubscribe explicitly.
        EventBus.Instance.ShopRequested -= Open;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Esc closes the shop (back to the pause menu) instead of unpausing the game under it.
        if (!Visible || !(@event.IsActionPressed("pause") || @event.IsActionPressed("ui_cancel")))
            return;
        GetViewport().SetInputAsHandled();
        Close();
    }

    public override void _Process(double delta)
    {
        if (!Visible)
            return;
        if (CurrentRotation() != _shownRotation)
            Refresh(); // the rotation changed while the shop was open
        UpdateCountdown();
    }

    public void Open()
    {
        _messageLabel.Text = "";
        Refresh();
        Show();
    }

    public void Close() => Hide();

    private string CurrentRotation() =>
        string.IsNullOrEmpty(RotationOverride) ? VehicleShop.RotationKey(DateTime.Now) : RotationOverride;

    private void Refresh()
    {
        var gm = GameManager.Instance;
        _shownRotation = CurrentRotation();
        _balanceLabel.Text = $"Balance {GameManager.FormatMoney(gm.Wallet.Balance)}";

        var current = gm.Vehicle;
        var currentModel = gm.Catalog.Find(current.ModelId);
        _currentLabel.Text =
            $"Your vehicle: {currentModel?.VehicleName ?? current.ModelId}   -   trade-in value {GameManager.FormatMoney(current.TradeInValue)}\n" +
            (currentModel != null ? DescribeStats(current.BuildStats(currentModel)) : "") + "\n" +
            current.DescribePerks("   -   ");

        foreach (var child in _offersGrid.GetChildren())
            child.QueueFree();
        var offers = VehicleShop.GenerateOffers(gm.Catalog, _shownRotation);
        foreach (var offer in offers)
            _offersGrid.AddChild(BuildCard(offer, currentModel != null ? current.BuildStats(currentModel) : null));
        UpdateCountdown();
    }

    private void UpdateCountdown()
    {
        if (!string.IsNullOrEmpty(RotationOverride))
        {
            _rotationLabel.Text = $"Showing rotation {RotationOverride}";
            return;
        }
        var now = DateTime.Now;
        var next = VehicleShop.NextRotation(now);
        var left = next - now;
        _rotationLabel.Text =
            $"New vehicles at {next:HH:mm} (in {(int)left.TotalHours}:{left.Minutes:00}:{left.Seconds:00})  -  the shop restocks at 00:00, 08:00 and 16:00";
    }

    private Control BuildCard(VehicleOffer offer, VehicleStats? currentStats)
    {
        var gm = GameManager.Instance;
        var stats = offer.Vehicle.BuildStats(offer.Model);
        var sold = gm.IsOfferSold(offer);
        var net = offer.Vehicle.Price - gm.Vehicle.TradeInValue;

        var card = new PanelContainer { ThemeTypeVariation = "JobCard", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        card.AddChild(box);

        var header = new HBoxContainer();
        header.AddChild(new ColorRect { Color = offer.Model.BodyColor, CustomMinimumSize = new Vector2(22, 22) });
        var name = new Label { Text = offer.Model.VehicleName, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        name.AddThemeFontSizeOverride("font_size", 18);
        header.AddChild(name);
        var price = new Label { Text = GameManager.FormatMoney(offer.Vehicle.Price) };
        price.AddThemeColorOverride("font_color", new Color(0.55f, 1f, 0.6f));
        price.AddThemeFontSizeOverride("font_size", 18);
        header.AddChild(price);
        box.AddChild(header);

        box.AddChild(SmallLabel(offer.Model.Description, new Color(0.75f, 0.75f, 0.8f)));
        box.AddChild(SmallLabel(DescribeStats(stats, currentStats), Colors.White));
        box.AddChild(SmallLabel(offer.Vehicle.DescribePerks(), new Color(1f, 0.8f, 0.45f)));

        var button = new Button { FocusMode = FocusModeEnum.None };
        if (sold)
        {
            button.Text = "Sold";
            button.Disabled = true;
        }
        else
        {
            button.Text = net >= 0
                ? $"Buy  -  you pay {GameManager.FormatMoney(net)} after trade-in"
                : $"Buy  -  you get {GameManager.FormatMoney(-net)} back after trade-in";
            button.Disabled = net > gm.Wallet.Balance;
            if (button.Disabled)
                button.TooltipText = "Not enough money";
            button.Pressed += () => AskToBuy(offer);
        }
        box.AddChild(button);
        return card;
    }

    private static Label SmallLabel(string text, Color color)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    /// <summary>"Top 72 km/h (+5)  -  Fuel 7.0 L/100 km  -  Tank 4.0 L"; differences against the current vehicle when given.</summary>
    private static string DescribeStats(VehicleStats stats, VehicleStats? compare = null)
    {
        static string F(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        static string Diff(float value, float? other)
        {
            if (other == null || Mathf.Abs(value - other.Value) < 0.05f)
                return "";
            var d = value - other.Value;
            return $" ({(d > 0 ? "+" : "")}{F(d)})";
        }
        var speed = stats.MaxSpeed * GameManager.SpeedToKmh;
        var fuel = stats.FuelPerKm * 100f;
        return $"Top {Mathf.RoundToInt(speed)} km/h{Diff(speed, compare?.MaxSpeed * GameManager.SpeedToKmh)}   " +
            $"Fuel {F(fuel)} L/100km{Diff(fuel, compare?.FuelPerKm * 100f)}   " +
            $"Tank {F(stats.FuelCapacity)} L{Diff(stats.FuelCapacity, compare?.FuelCapacity)}";
    }

    private void AskToBuy(VehicleOffer offer)
    {
        var gm = GameManager.Instance;
        var currentName = gm.Catalog.Find(gm.Vehicle.ModelId)?.VehicleName ?? gm.Vehicle.ModelId;
        var net = offer.Vehicle.Price - gm.Vehicle.TradeInValue;
        _pendingOffer = offer;
        _confirmDialog.DialogText =
            $"Buy the {offer.Model.VehicleName} for {GameManager.FormatMoney(offer.Vehicle.Price)}?\n\n" +
            $"Your {currentName} is traded in for {GameManager.FormatMoney(gm.Vehicle.TradeInValue)}.\n" +
            (net >= 0 ? $"You pay {GameManager.FormatMoney(net)}." : $"You get {GameManager.FormatMoney(-net)} back.");
        _confirmDialog.PopupCentered();
    }

    private void OnConfirmed()
    {
        if (_pendingOffer == null)
            return;
        var error = GameManager.Instance.BuyVehicle(_pendingOffer);
        _messageLabel.Text = error ?? $"You now ride the {_pendingOffer.Model.VehicleName}. Full tank included!";
        _messageLabel.Modulate = error == null ? new Color(0.55f, 1f, 0.6f) : new Color(1f, 0.45f, 0.4f);
        _pendingOffer = null;
        Refresh();
    }
}
