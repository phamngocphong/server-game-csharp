using Godot;

namespace ShipperSimulator;

/// <summary>
/// Describes one city region (grid layout, districts, street names).
/// Future "multiple city regions" = more .tres files + a region switcher.
/// </summary>
[GlobalClass]
public partial class CityRegionData : Resource
{
    [Export] public string RegionId { get; set; } = "central_city";
    [Export] public string DisplayName { get; set; } = "Central City";
    /// <summary>
    /// Fixed seed so the same region always generates the same layout
    /// (saved player positions stay valid).
    /// </summary>
    [Export] public int LayoutSeed { get; set; } = 1337;

    [ExportGroup("Grid")]
    /// <summary>Number of city blocks (columns, rows).</summary>
    [Export] public Vector2I GridSize { get; set; } = new(10, 8);
    [Export] public float BlockSize { get; set; } = 420f;
    [Export] public float RoadWidth { get; set; } = 140f;

    [ExportGroup("Look")]
    [Export] public Color RoadColor { get; set; } = new(0.22f, 0.23f, 0.26f);
    [Export] public Color LaneColor { get; set; } = new(0.85f, 0.8f, 0.45f, 0.7f);
    [Export] public Color ParkColor { get; set; } = new(0.3f, 0.55f, 0.3f);

    [ExportGroup("Content")]
    [Export] public Godot.Collections.Array<DistrictData> Districts { get; set; } = new();
    /// <summary>Names of horizontal roads, top to bottom (GridSize.Y + 1 roads).</summary>
    [Export] public string[] HorizontalStreetNames { get; set; } = System.Array.Empty<string>();
    /// <summary>Names of vertical roads, left to right (GridSize.X + 1 roads).</summary>
    [Export] public string[] VerticalStreetNames { get; set; } = System.Array.Empty<string>();

    public DistrictData? GetDistrictAt(Vector2I cell)
    {
        foreach (var district in Districts)
        {
            if (district.GridRect.HasPoint(cell))
                return district;
        }
        return Districts.Count > 0 ? Districts[0] : null;
    }

    public string GetHorizontalStreet(int index) =>
        HorizontalStreetNames.Length == 0 ? $"Street {index + 1}" : HorizontalStreetNames[index % HorizontalStreetNames.Length];

    public string GetVerticalStreet(int index) =>
        VerticalStreetNames.Length == 0 ? $"Avenue {index + 1}" : VerticalStreetNames[index % VerticalStreetNames.Length];
}
