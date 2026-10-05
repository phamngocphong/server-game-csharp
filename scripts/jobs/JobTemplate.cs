using Godot;

namespace ShipperSimulator;

/// <summary>
/// Data-driven job definition (one .tres per job type in resources/jobs/).
/// <see cref="JobGenerator"/> turns templates into concrete <see cref="JobData"/>.
/// </summary>
[GlobalClass]
public partial class JobTemplate : Resource
{
    [Export] public string TemplateId { get; set; } = "parcel";
    [Export] public string PackageName { get; set; } = "Parcel";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    [Export] public Color Color { get; set; } = new(0.95f, 0.65f, 0.2f);

    [ExportGroup("Reward")]
    [Export] public float BaseReward { get; set; } = 20f;
    [Export] public float RewardPerKm { get; set; } = 12f;
    /// <summary>+/- random spread applied to the final reward (0.15 = 15%).</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float RewardVariance { get; set; } = 0.15f;

    [ExportGroup("Route")]
    /// <summary>Route distance range between pickup and delivery, in pixels.</summary>
    [Export] public float MinDistance { get; set; } = 800f;
    [Export] public float MaxDistance { get; set; } = 3000f;

    [ExportGroup("Generation")]
    /// <summary>Relative chance this template is picked.</summary>
    [Export] public float Weight { get; set; } = 1f;
}
