using System.Collections.Generic;

namespace ShipperSimulator;

// Plain DTOs for the city JSON files in data/cities/ (snake_case keys).
// CityLoader turns them into CityRegionData / DistrictData. Every field
// except code, name and districts has a default, so a minimal city is short.
// Colors are HTML strings: "#rrggbb" or "#rrggbbaa".

public sealed class CityConfig
{
    /// <summary>Unique id, stored in the save file. Keep it stable once players have saves.</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Layout seed; 0 = new random layout every time the city is loaded.</summary>
    public int Seed { get; set; } = 1337;
    public CityGridConfig Grid { get; set; } = new();
    public CityColorsConfig Colors { get; set; } = new();
    public string WaterPlaceName { get; set; } = "Waterfront";
    public List<DistrictConfig> Districts { get; set; } = new();
    public CityStreetsConfig Streets { get; set; } = new();
}

public sealed class CityGridConfig
{
    public int Columns { get; set; } = 10;
    public int Rows { get; set; } = 8;
    public float BlockSize { get; set; } = 420f;
    public float RoadWidth { get; set; } = 140f;
}

public sealed class CityColorsConfig
{
    public string Road { get; set; } = "#383b42";
    public string Lane { get; set; } = "#d9cc73b3";
    public string Park { get; set; } = "#4d8c4d";
    public string Water { get; set; } = "#336b99";
}

public sealed class CityStreetsConfig
{
    /// <summary>Top to bottom; a full set has grid.rows + 1 names (shorter lists repeat).</summary>
    public List<string> Horizontal { get; set; } = new();
    /// <summary>Left to right; a full set has grid.columns + 1 names (shorter lists repeat).</summary>
    public List<string> Vertical { get; set; } = new();
}

public sealed class DistrictConfig
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Blocks covered by the district: [x, y, width, height] in grid cells.</summary>
    public int[] Area { get; set; } = { 0, 0, 1, 1 };
    public string SidewalkColor { get; set; } = "#8c8c8c";
    public List<string> BuildingColors { get; set; } = new() { "#808099" };
    public float ParkChance { get; set; } = 0.1f;
    public float WaterChance { get; set; }
    public int MinLots { get; set; } = 2;
    public int MaxLots { get; set; } = 3;
    public float RewardMultiplier { get; set; } = 1f;
}
