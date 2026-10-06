using Godot;

namespace ShipperSimulator;

/// <summary>Procedural top-down motorbike + rider (faces +X). Replace with sprites later.</summary>
public partial class BikeVisual : Node2D
{
    private static readonly Color BodyColor = new(0.85f, 0.2f, 0.2f);
    private static readonly Color TireColor = new(0.08f, 0.08f, 0.08f);
    private static readonly Color JacketColor = new(0.95f, 0.55f, 0.1f);
    private static readonly Color HelmetColor = new(0.95f, 0.95f, 0.95f);
    private static readonly Color BoxEmptyColor = new(0.45f, 0.3f, 0.15f);
    private static readonly Color BoxFullColor = new(0.95f, 0.75f, 0.3f);
    private static readonly Color PassengerShirtColor = new(0.3f, 0.6f, 0.95f);
    private static readonly Color PassengerHelmetColor = new(0.2f, 0.75f, 0.35f);

    private bool _hasPackage;
    private bool _hasPassenger;
    private float _lean;

    public bool HasPackage
    {
        get => _hasPackage;
        set
        {
            _hasPackage = value;
            QueueRedraw();
        }
    }

    public bool HasPassenger
    {
        get => _hasPassenger;
        set
        {
            _hasPassenger = value;
            QueueRedraw();
        }
    }

    /// <summary>-1..1, leaning into turns.</summary>
    public float Lean
    {
        get => _lean;
        set
        {
            _lean = value;
            Rotation = _lean * 0.18f;
        }
    }

    public override void _Draw()
    {
        // Shadow
        DrawRect(new Rect2(-22, -6, 48, 16), new Color(0, 0, 0, 0.25f));
        // Wheels
        DrawRect(new Rect2(12, -3, 13, 6), TireColor);
        DrawRect(new Rect2(-24, -3, 13, 6), TireColor);
        // Body
        DrawRect(new Rect2(-14, -6, 30, 12), BodyColor);
        DrawRect(new Rect2(14, -4, 4, 8), new Color(1f, 0.95f, 0.6f)); // headlight
        // Handlebar
        DrawLine(new Vector2(9, -11), new Vector2(9, 11), new Color(0.2f, 0.2f, 0.2f), 3f);
        // Delivery box
        var boxColor = HasPackage ? BoxFullColor : BoxEmptyColor;
        DrawRect(new Rect2(-26, -10, 15, 20), boxColor);
        DrawRect(new Rect2(-26, -10, 15, 20), boxColor.Darkened(0.5f), false, 2f);
        // Passenger sits behind the rider, on the front of the box.
        if (HasPassenger)
        {
            DrawCircle(new Vector2(-14, 0), 7.5f, PassengerShirtColor);
            DrawCircle(new Vector2(-10, 0), 5f, PassengerHelmetColor);
        }
        // Rider
        DrawCircle(new Vector2(-3, 0), 8f, JacketColor);
        DrawCircle(new Vector2(2, 0), 5.5f, HelmetColor);
    }
}
