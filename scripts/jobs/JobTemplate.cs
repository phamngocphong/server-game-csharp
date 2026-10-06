using Godot;

namespace ShipperSimulator;

/// <summary>
/// Data-driven job definition (one .tres per job type in resources/jobs/).
/// <see cref="JobGenerator"/> turns templates into concrete <see cref="JobData"/>.
/// </summary>
[GlobalClass]
public partial class JobTemplate : Resource
{
    public enum CargoKind { Package, Passenger }

    [Export] public string TemplateId { get; set; } = "parcel";
    [Export] public string PackageName { get; set; } = "Parcel";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    [Export] public Color Color { get; set; } = new(0.95f, 0.65f, 0.2f);
    /// <summary>What the bike carries: changes texts, the marker icon and the bike visual.</summary>
    [Export] public CargoKind Cargo { get; set; } = CargoKind.Package;
    /// <summary>Optional customer names; one is picked per job and shown with the job title.</summary>
    [Export] public string[] CustomerNames { get; set; } = System.Array.Empty<string>();

    [ExportGroup("Reward")]
    [Export] public float BaseReward { get; set; } = 20f;
    [Export] public float RewardPerKm { get; set; } = 12f;
    /// <summary>+/- random spread applied to the final reward (0.15 = 15%).</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float RewardVariance { get; set; } = 0.15f;

    [ExportGroup("Route")]
    /// <summary>Route distance range between pickup and delivery, in pixels.</summary>
    [Export] public float MinDistance { get; set; } = 800f;
    [Export] public float MaxDistance { get; set; } = 3000f;

    [ExportGroup("Time limits")]
    /// <summary>
    /// Timed jobs get a deadline for the pickup and another for the drop-off:
    /// limit = BaseTime + route km * TimePerKm (seconds, rounded up to 5 s).
    /// </summary>
    [Export] public bool IsTimed { get; set; }
    [Export] public float PickupBaseTime { get; set; } = 15f;
    [Export] public float PickupTimePerKm { get; set; } = 9f;
    [Export] public float DeliveryBaseTime { get; set; } = 15f;
    [Export] public float DeliveryTimePerKm { get; set; } = 8f;
    /// <summary>
    /// Missing the pickup deadline always fails the job. Missing the drop-off deadline
    /// cuts this share of the reward; 1 = the job fails instead.
    /// </summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float LatePenalty { get; set; } = 0.5f;

    [ExportGroup("Generation")]
    /// <summary>Relative chance this template is picked.</summary>
    [Export] public float Weight { get; set; } = 1f;
}
