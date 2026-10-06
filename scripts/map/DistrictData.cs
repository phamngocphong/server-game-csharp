using Godot;

namespace ShipperSimulator;

/// <summary>
/// A district of a city region: covers a rectangle of city blocks and controls
/// its look, building density and pay rate.
/// </summary>
[GlobalClass]
public partial class DistrictData : Resource
{
    [Export] public string DistrictId { get; set; } = "district";
    [Export] public string DisplayName { get; set; } = "District";
    /// <summary>Area in block coordinates (x, y, width, height).</summary>
    [Export] public Rect2I GridRect { get; set; } = new(0, 0, 1, 1);

    [ExportGroup("Look")]
    [Export] public Color SidewalkColor { get; set; } = new(0.55f, 0.55f, 0.55f);
    [Export] public Color[] BuildingColors { get; set; } = { new(0.5f, 0.5f, 0.6f) };

    [ExportGroup("Layout")]
    [Export(PropertyHint.Range, "0,1,0.01")] public float ParkChance { get; set; } = 0.1f;
    /// <summary>Chance that a block is a lake/river (impassable, rolled before parks). 1 = all water.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float WaterChance { get; set; }
    [Export(PropertyHint.Range, "1,6")] public int MinLots { get; set; } = 2;
    [Export(PropertyHint.Range, "1,6")] public int MaxLots { get; set; } = 3;

    [ExportGroup("Economy")]
    [Export] public float RewardMultiplier { get; set; } = 1f;
}
