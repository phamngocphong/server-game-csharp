using Godot;

namespace ShipperSimulator;

/// <summary>
/// Describes one city region (grid layout, districts, street names).
/// Built at runtime from the JSON files in data/cities/ by CityLoader.
/// </summary>
[GlobalClass]
public partial class CityRegionData : Resource
{
	[Export] public string RegionId { get; set; } = "city";
	[Export] public string DisplayName { get; set; } = "City";
	/// <summary>
	/// Seed for the procedural layout. A fixed seed always generates the same
	/// layout (saved player positions stay valid); 0 = new random layout every time.
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
	[Export] public Color WaterColor { get; set; } = new(0.2f, 0.42f, 0.6f);

	[ExportGroup("Traffic")]
	/// <summary>Number of AI vehicles; -1 = automatic (TrafficManager.AutoDensity per block).</summary>
	[Export] public int TrafficCount { get; set; } = -1;
	/// <summary>Spawn weight per TrafficVehicleData.VehicleId; missing ids use the vehicle's own SpawnWeight.</summary>
	[Export] public Godot.Collections.Dictionary<string, float> TrafficWeights { get; set; } = new();
	/// <summary>Share (0-1) of inner intersections that get traffic lights.</summary>
	[Export(PropertyHint.Range, "0,1,0.05")] public float TrafficLightChance { get; set; } = 0.45f;

	[ExportGroup("Fuel")]
	/// <summary>Money per liter at the gas stations.</summary>
	[Export] public float FuelPrice { get; set; } = 2.5f;
	/// <summary>Number of gas stations; -1 = automatic (about one per 12 blocks, at least 3).</summary>
	[Export] public int FuelStationCount { get; set; } = -1;

	[ExportGroup("Rest")]
	/// <summary>Price of a meal and rest at a rest stop.</summary>
	[Export] public int RestPrice { get; set; } = 8;
	/// <summary>Number of rest stops; -1 = automatic (about one per 16 blocks, at least 2).</summary>
	[Export] public int RestStopCount { get; set; } = -1;

	[ExportGroup("Weather")]
	/// <summary>Chance per WeatherData.WeatherId; missing ids use the weather's DefaultWeight.</summary>
	[Export] public Godot.Collections.Dictionary<string, float> WeatherWeights { get; set; } = new();

	[ExportGroup("Content")]
	/// <summary>Place name used for addresses next to water blocks.</summary>
	[Export] public string WaterPlaceName { get; set; } = "Waterfront";
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
