using Godot;

namespace ShipperSimulator;

/// <summary>
/// Static obstacle (building or tree) drawn procedurally.
/// Can later be swapped for sprite-based scenes without touching CityMap logic.
/// </summary>
public partial class Building : StaticBody2D
{
    public const uint WorldLayer = 1u << 1;

    public Vector2 Size { get; private set; } = new(64, 64);
    public Color Color { get; private set; } = Colors.Gray;
    public bool IsRound { get; private set; }

    public void Setup(Vector2 size, Color color, bool isRound = false)
    {
        Size = size;
        Color = color;
        IsRound = isRound;
        CollisionLayer = WorldLayer;
        CollisionMask = 0;

        var shapeNode = new CollisionShape2D();
        shapeNode.Shape = isRound
            ? new CircleShape2D { Radius = size.X * 0.5f }
            : new RectangleShape2D { Size = size };
        AddChild(shapeNode);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (IsRound)
        {
            var r = Size.X * 0.5f;
            DrawCircle(new Vector2(4, 5), r, new Color(0, 0, 0, 0.25f));
            DrawCircle(Vector2.Zero, r, Color);
            DrawCircle(new Vector2(-r * 0.25f, -r * 0.25f), r * 0.55f, Color.Lightened(0.15f));
            return;
        }

        var rect = new Rect2(-Size * 0.5f, Size);
        DrawRect(new Rect2(rect.Position + new Vector2(7, 9), rect.Size), new Color(0, 0, 0, 0.3f));
        DrawRect(rect, Color.Darkened(0.15f));
        DrawRect(rect.Grow(-6f), Color);
        DrawRect(rect, Color.Darkened(0.45f), false, 2f);
        // Rooftop unit for a bit of detail.
        var unit = new Vector2(Mathf.Min(22f, Size.X * 0.25f), Mathf.Min(16f, Size.Y * 0.25f));
        DrawRect(new Rect2(rect.End - unit - new Vector2(12, 12), unit), Color.Lightened(0.2f));
    }
}
