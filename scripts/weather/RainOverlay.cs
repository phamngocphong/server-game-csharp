using Godot;

namespace ShipperSimulator;

/// <summary>
/// Screen-space rain: slanted streaks drawn on a full-screen Control, plus lightning
/// flashes when <see cref="Intensity"/> is near 1. Cheap enough to redraw every frame.
/// </summary>
public partial class RainOverlay : Control
{
    private const int MaxDrops = 420;
    private const float FallSpeed = 1500f;
    private static readonly Vector2 Wind = new(-0.22f, 1f);

    /// <summary>0 = no rain, 1 = storm.</summary>
    public float Intensity { get; set; }

    private readonly Vector2[] _drops = new Vector2[MaxDrops];
    private readonly float[] _lengths = new float[MaxDrops];
    private readonly RandomNumberGenerator _rng = new();
    private float _flash;
    private float _nextFlash = 6f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        _rng.Randomize();
        for (var i = 0; i < MaxDrops; i++)
            Respawn(i, anywhere: true);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (Intensity <= 0.01f && _flash <= 0f)
        {
            if (Visible)
                Hide();
            return;
        }
        Show();

        var size = Size;
        for (var i = 0; i < ActiveDrops(); i++)
        {
            _drops[i] += Wind.Normalized() * FallSpeed * dt;
            if (_drops[i].Y > size.Y + 40f || _drops[i].X < -40f)
                Respawn(i, anywhere: false);
        }

        _flash = Mathf.Max(0f, _flash - dt * 3f);
        if (Intensity > 0.9f)
        {
            _nextFlash -= dt;
            if (_nextFlash <= 0f)
            {
                _flash = 1f;
                _nextFlash = _rng.RandfRange(5f, 12f);
            }
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var direction = Wind.Normalized();
        var color = new Color(0.75f, 0.82f, 0.95f, 0.25f + 0.25f * Intensity);
        for (var i = 0; i < ActiveDrops(); i++)
            DrawLine(_drops[i], _drops[i] + direction * _lengths[i], color, 1.5f);
        if (_flash > 0f)
            DrawRect(new Rect2(Vector2.Zero, Size), new Color(1f, 1f, 1f, 0.45f * _flash));
    }

    private int ActiveDrops() => Mathf.Clamp(Mathf.RoundToInt(MaxDrops * Intensity), 0, MaxDrops);

    private void Respawn(int i, bool anywhere)
    {
        var size = Size == Vector2.Zero ? new Vector2(1280, 720) : Size;
        _drops[i] = anywhere
            ? new Vector2(_rng.RandfRange(0f, size.X * 1.3f), _rng.RandfRange(-size.Y, size.Y))
            : new Vector2(_rng.RandfRange(0f, size.X * 1.3f), _rng.RandfRange(-80f, -10f));
        _lengths[i] = _rng.RandfRange(14f, 30f);
    }
}
