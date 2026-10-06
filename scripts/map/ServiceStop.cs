using Godot;

namespace ShipperSimulator;

/// <summary>
/// Base for curbside services placed by <see cref="CityMap"/> (gas stations, rest stops):
/// a round trigger zone with an interaction prompt, E to use it, and a glow at night.
/// Subclasses draw themselves and decide what E does.
/// </summary>
public abstract partial class ServiceStop : Area2D
{
    public const float Radius = 64f;
    private const uint InteractablesLayer = 1u << 2;

    public string StopName { get; private set; } = "";
    public bool IsPlayerInside { get; private set; }

    /// <summary>Color of the zone, the ring and the night glow.</summary>
    protected abstract Color AccentColor { get; }

    /// <summary>Prompt shown while the player is inside, e.g. "[E] Refuel".</summary>
    protected abstract string Prompt();

    /// <summary>What pressing E inside the zone does.</summary>
    protected abstract void Use();

    public void Setup(string stopName, Vector2 position)
    {
        StopName = stopName;
        Position = position;
    }

    public override void _Ready()
    {
        CollisionLayer = InteractablesLayer;
        CollisionMask = 1u; // player
        ZIndex = 2;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });
        AddChild(NightLight.Glow(Radius * 1.6f, AccentColor, 0.8f));
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsPlayerInside || !@event.IsActionPressed("interact"))
            return;
        GetViewport().SetInputAsHandled();
        Use();
    }

    /// <summary>Zone circle and ring; subclasses draw their icon on top.</summary>
    protected void DrawZone()
    {
        DrawCircle(Vector2.Zero, Radius, new Color(AccentColor, 0.12f));
        DrawArc(Vector2.Zero, Radius, 0f, Mathf.Tau, 48, new Color(AccentColor, 0.7f), 3f, true);
    }

    protected void DrawSign(string text)
    {
        DrawString(ThemeDB.FallbackFont, new Vector2(-32, -30), text, HorizontalAlignment.Center, 64, 18, AccentColor);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (!body.IsInGroup("player"))
            return;
        IsPlayerInside = true;
        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionPromptChanged, Prompt());
    }

    private void OnBodyExited(Node2D body)
    {
        if (!body.IsInGroup("player"))
            return;
        IsPlayerInside = false;
        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionPromptChanged, "");
    }
}
