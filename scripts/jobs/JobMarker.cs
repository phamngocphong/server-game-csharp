using Godot;

namespace ShipperSimulator;

/// <summary>World-space zone for a pickup or delivery point.</summary>
public partial class JobMarker : Area2D
{
    [Signal] public delegate void PlayerEnteredEventHandler();
    [Signal] public delegate void PlayerExitedEventHandler();

    public enum Kind { Pickup, Delivery }

    private static readonly Color PickupColor = new(1f, 0.65f, 0.15f);
    private static readonly Color DeliveryColor = new(0.3f, 0.9f, 0.45f);

    [Export] public float Radius { get; set; } = 56f;

    public Kind MarkerKind { get; private set; } = Kind.Pickup;
    public string Title { get; private set; } = "";
    /// <summary>Draws a person instead of a package at the pickup.</summary>
    public bool IsPassenger { get; private set; }

    private float _time;
    private Label _label = null!;

    public void Setup(Kind kind, string title, bool isPassenger = false)
    {
        MarkerKind = kind;
        Title = title;
        IsPassenger = isPassenger;
    }

    public override void _Ready()
    {
        _label = GetNode<Label>("Label");
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        _label.Text = Title;
        _label.AddThemeColorOverride("font_color", GetColor());
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public Color GetColor() => MarkerKind == Kind.Pickup ? PickupColor : DeliveryColor;

    public override void _Draw()
    {
        var c = GetColor();
        var pulse = 0.5f + 0.5f * Mathf.Sin(_time * 4f);
        DrawCircle(Vector2.Zero, Radius, new Color(c, 0.15f + 0.1f * pulse));
        DrawArc(Vector2.Zero, Radius * (0.85f + 0.15f * pulse), 0f, Mathf.Tau, 48, c, 4f, true);

        if (MarkerKind == Kind.Pickup && IsPassenger)
        {
            // Waiting person icon: head + shoulders.
            DrawCircle(new Vector2(0, -9), 7f, c);
            DrawColoredPolygon(new[] { new Vector2(-13, 14), new Vector2(-9, 1), new Vector2(9, 1), new Vector2(13, 14) }, c);
        }
        else if (MarkerKind == Kind.Pickup)
        {
            // Package box icon.
            DrawRect(new Rect2(-14, -14, 28, 28), c);
            DrawLine(new Vector2(-14, 0), new Vector2(14, 0), c.Darkened(0.5f), 3f);
            DrawLine(new Vector2(0, -14), new Vector2(0, 14), c.Darkened(0.5f), 3f);
        }
        else
        {
            // House icon.
            DrawColoredPolygon(new[] { new Vector2(-16, -2), new Vector2(0, -18), new Vector2(16, -2) }, c);
            DrawRect(new Rect2(-11, -2, 22, 16), c);
            DrawRect(new Rect2(-4, 4, 8, 10), c.Darkened(0.5f));
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body.IsInGroup("player"))
            EmitSignal(SignalName.PlayerEntered);
    }

    private void OnBodyExited(Node2D body)
    {
        if (body.IsInGroup("player"))
            EmitSignal(SignalName.PlayerExited);
    }
}
