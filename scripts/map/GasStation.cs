using Godot;

namespace ShipperSimulator;

/// <summary>
/// Curbside gas station placed by <see cref="CityMap"/>. Drive in and press E to fill the
/// tank (<see cref="GameManager.BuyFuel"/>). Drawn procedurally: a pad, a pump and a "GAS" sign.
/// </summary>
public partial class GasStation : Area2D
{
    public const float Radius = 64f;
    private const uint InteractablesLayer = 1u << 2;

    private static readonly Color PadColor = new(0.85f, 0.85f, 0.82f, 0.9f);
    private static readonly Color PumpColor = new(0.9f, 0.2f, 0.15f);

    public string StationName { get; private set; } = "Gas Station";
    public bool IsPlayerInside { get; private set; }

    public void Setup(string stationName, Vector2 position)
    {
        StationName = stationName;
        Position = position;
    }

    public override void _Ready()
    {
        CollisionLayer = InteractablesLayer;
        CollisionMask = 1u; // player
        ZIndex = 2;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsPlayerInside || !@event.IsActionPressed("interact"))
            return;
        GetViewport().SetInputAsHandled();
        GameManager.Instance.BuyFuel(StationName);
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, Radius, new Color(1f, 0.85f, 0.2f, 0.12f));
        DrawArc(Vector2.Zero, Radius, 0f, Mathf.Tau, 48, new Color(1f, 0.85f, 0.2f, 0.7f), 3f, true);
        DrawRect(new Rect2(-30, -22, 60, 44), PadColor);
        // Pump: body, display and hose.
        DrawRect(new Rect2(-12, -16, 20, 30), PumpColor);
        DrawRect(new Rect2(-8, -12, 12, 8), new Color(0.15f, 0.2f, 0.25f));
        DrawPolyline(new[] { new Vector2(8, -6), new Vector2(18, -2), new Vector2(18, 12) }, new Color(0.1f, 0.1f, 0.1f), 3f);
        var font = ThemeDB.FallbackFont;
        DrawString(font, new Vector2(-24, -30), "GAS", HorizontalAlignment.Center, 48, 18, new Color(1f, 0.85f, 0.2f));
    }

    private void OnBodyEntered(Node2D body)
    {
        if (!body.IsInGroup("player"))
            return;
        IsPlayerInside = true;
        var price = GameManager.Instance.CityMap?.Region.FuelPrice ?? 0f;
        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionPromptChanged,
            $"[E] Refuel  ({GameManager.FormatPrice(price)} / L)");
    }

    private void OnBodyExited(Node2D body)
    {
        if (!body.IsInGroup("player"))
            return;
        IsPlayerInside = false;
        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionPromptChanged, "");
    }
}
