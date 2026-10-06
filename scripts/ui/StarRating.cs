using Godot;

namespace ShipperSimulator;

/// <summary>
/// Draws five stars filled up to <see cref="Value"/> (0-5, fractions allowed).
/// The stars are drawn as shapes, not text, so they work with any font.
/// </summary>
[GlobalClass]
public partial class StarRating : Control
{
    private static readonly Color FilledColor = new(1f, 0.8f, 0.2f);
    private static readonly Color EmptyColor = new(1f, 1f, 1f, 0.18f);

    private float _value = 5f;
    private float _starSize = 18f;

    [Export(PropertyHint.Range, "0,5,0.1")]
    public float Value
    {
        get => _value;
        set
        {
            _value = Mathf.Clamp(value, 0f, 5f);
            QueueRedraw();
        }
    }

    [Export]
    public float StarSize
    {
        get => _starSize;
        set
        {
            _starSize = value;
            UpdateMinimumSize();
            QueueRedraw();
        }
    }

    private float Gap => _starSize * 0.15f;

    public override Vector2 _GetMinimumSize() => new(_starSize * 5f + Gap * 4f, _starSize);

    public override void _Draw()
    {
        for (var i = 0; i < 5; i++)
        {
            var origin = new Vector2(i * (_starSize + Gap), (Size.Y - _starSize) * 0.5f);
            var star = BuildStar(origin + Vector2.One * _starSize * 0.5f, _starSize * 0.5f);
            DrawColoredPolygon(star, EmptyColor);

            var fill = Mathf.Clamp(_value - i, 0f, 1f);
            if (fill <= 0f)
                continue;
            if (fill >= 1f)
            {
                DrawColoredPolygon(star, FilledColor);
                continue;
            }
            // Partial star: keep only the part of the star left of the fill line.
            var clip = new[]
            {
                origin, origin + new Vector2(_starSize * fill, 0f),
                origin + new Vector2(_starSize * fill, _starSize), origin + new Vector2(0f, _starSize),
            };
            foreach (var part in Geometry2D.IntersectPolygons(star, clip))
                DrawColoredPolygon(part, FilledColor);
        }
    }

    private static Vector2[] BuildStar(Vector2 center, float radius)
    {
        var points = new Vector2[10];
        for (var i = 0; i < 10; i++)
        {
            var r = i % 2 == 0 ? radius : radius * 0.45f;
            var angle = -Mathf.Pi / 2f + i * Mathf.Pi / 5f;
            points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
        }
        return points;
    }
}
