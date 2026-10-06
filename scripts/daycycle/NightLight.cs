using Godot;

namespace ShipperSimulator;

/// <summary>
/// A 2D light that fades in with <see cref="DayCycle.Darkness"/>: off by day, full energy at
/// night. Used for the player's and traffic's headlights and for glows on markers and stops.
/// </summary>
public partial class NightLight : PointLight2D
{
    private const int TextureSize = 128;
    /// <summary>Shared light texture, created on first use (never in a field initializer).</summary>
    private static GradientTexture2D? _radialTexture;

    /// <summary>Energy at full darkness.</summary>
    public float MaxEnergy { get; set; } = 1f;

    /// <summary>Round glow centered on its parent.</summary>
    public static NightLight Glow(float radius, Color color, float energy = 0.9f) => new()
    {
        Texture = RadialTexture(),
        TextureScale = radius * 2f / TextureSize,
        Color = color,
        MaxEnergy = energy,
    };

    /// <summary>Beam in front of a vehicle that faces +X: an elongated glow starting at the nose.</summary>
    public static NightLight Headlight(float noseOffset, float length, float width, float energy = 1f) => new()
    {
        Texture = RadialTexture(),
        TextureScale = length / TextureSize,
        Scale = new Vector2(1f, width / length),
        Position = new Vector2(noseOffset + length * 0.45f, 0f),
        Color = new Color(1f, 0.95f, 0.8f),
        MaxEnergy = energy,
    };

    public override void _Ready()
    {
        Enabled = false;
        Energy = 0f;
    }

    public override void _Process(double delta)
    {
        var darkness = GameManager.Instance.DayCycle?.Darkness ?? 0f;
        Enabled = darkness > 0.05f;
        Energy = MaxEnergy * darkness;
    }

    private static GradientTexture2D RadialTexture()
    {
        if (_radialTexture != null)
            return _radialTexture;
        var gradient = new Gradient();
        gradient.SetColor(0, Colors.White);
        gradient.SetColor(1, new Color(1f, 1f, 1f, 0f));
        _radialTexture = new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Width = TextureSize,
            Height = TextureSize,
        };
        return _radialTexture;
    }
}
