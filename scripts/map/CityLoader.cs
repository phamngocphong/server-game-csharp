using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Loads every city JSON file (*.json) in a folder and converts it into a
/// <see cref="CityRegionData"/> that <see cref="CityMap"/> can build.
/// Adding a city = dropping a new JSON file into the folder; no code or scene changes.
/// Invalid files are skipped with an error in the Output panel.
/// </summary>
public static class CityLoader
{
    public const string DefaultFolder = "res://data/cities";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>All valid cities in <paramref name="folder"/>, sorted by file name.</summary>
    public static List<CityRegionData> LoadAll(string folder = DefaultFolder)
    {
        var cities = new List<CityRegionData>();
        if (!DirAccess.DirExistsAbsolute(folder))
        {
            GD.PushError($"CityLoader: folder {folder} does not exist.");
            return cities;
        }

        var files = DirAccess.GetFilesAt(folder)
            .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal);
        foreach (var file in files)
        {
            var path = $"{folder}/{file}";
            var city = LoadFile(path);
            if (city == null)
                continue;
            if (cities.Any(c => c.RegionId == city.RegionId))
            {
                GD.PushError($"CityLoader: {path} reuses code \"{city.RegionId}\", skipped.");
                continue;
            }
            cities.Add(city);
        }
        return cities;
    }

    /// <summary>Loads one city file; returns null (and logs why) when it is invalid.</summary>
    public static CityRegionData? LoadFile(string path)
    {
        CityConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<CityConfig>(FileAccess.GetFileAsString(path), JsonOptions);
        }
        catch (JsonException e)
        {
            GD.PushError($"CityLoader: {path} is not valid JSON. {e.Message}");
            return null;
        }
        if (config == null)
            return null;

        var errors = Validate(config);
        if (errors.Count > 0)
        {
            GD.PushError($"CityLoader: {path} skipped:\n  - " + string.Join("\n  - ", errors));
            return null;
        }
        foreach (var warning in Warnings(config))
            GD.PushWarning($"CityLoader: {path}: {warning}");
        return ToRegion(config, path);
    }

    // --- Conversion -----------------------------------------------------------

    private static CityRegionData ToRegion(CityConfig c, string path)
    {
        var region = new CityRegionData
        {
            RegionId = c.Code,
            DisplayName = c.Name,
            LayoutSeed = c.Seed,
            GridSize = new Vector2I(c.Grid.Columns, c.Grid.Rows),
            BlockSize = c.Grid.BlockSize,
            RoadWidth = c.Grid.RoadWidth,
            RoadColor = ParseColor(c.Colors.Road, path),
            LaneColor = ParseColor(c.Colors.Lane, path),
            ParkColor = ParseColor(c.Colors.Park, path),
            WaterColor = ParseColor(c.Colors.Water, path),
            WaterPlaceName = c.WaterPlaceName,
            HorizontalStreetNames = c.Streets.Horizontal.ToArray(),
            VerticalStreetNames = c.Streets.Vertical.ToArray(),
            TrafficCount = c.Traffic.Count,
            TrafficLightChance = c.Traffic.LightChance,
            FuelPrice = c.Fuel.PricePerLiter,
            FuelStationCount = c.Fuel.Stations,
            RestPrice = c.Rest.Price,
            RestStopCount = c.Rest.Stops,
        };
        foreach (var (id, weight) in c.Weather.Weights)
            region.WeatherWeights[id] = weight;
        foreach (var (id, weight) in c.Traffic.Weights)
            region.TrafficWeights[id] = weight;
        foreach (var d in c.Districts)
        {
            region.Districts.Add(new DistrictData
            {
                DistrictId = d.Code,
                DisplayName = d.Name,
                GridRect = new Rect2I(d.Area[0], d.Area[1], d.Area[2], d.Area[3]),
                SidewalkColor = ParseColor(d.SidewalkColor, path),
                BuildingColors = d.BuildingColors.Select(color => ParseColor(color, path)).ToArray(),
                ParkChance = d.ParkChance,
                WaterChance = d.WaterChance,
                MinLots = d.MinLots,
                MaxLots = d.MaxLots,
                RewardMultiplier = d.RewardMultiplier,
            });
        }
        return region;
    }

    private static Color ParseColor(string html, string path)
    {
        if (Color.HtmlIsValid(html))
            return Color.FromHtml(html);
        GD.PushWarning($"CityLoader: {path}: invalid color \"{html}\", using gray.");
        return Colors.Gray;
    }

    // --- Validation -----------------------------------------------------------

    /// <summary>Problems that make the city unplayable.</summary>
    private static List<string> Validate(CityConfig c)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(c.Code))
            errors.Add("\"code\" is required.");
        if (string.IsNullOrWhiteSpace(c.Name))
            errors.Add("\"name\" is required.");
        if (c.Grid.Columns < 2 || c.Grid.Rows < 2)
            errors.Add("grid must be at least 2 x 2 blocks.");
        if (c.Grid.BlockSize < 100f || c.Grid.RoadWidth < 60f)
            errors.Add("grid.block_size must be >= 100 and grid.road_width >= 60.");
        if (c.Districts.Count == 0)
            errors.Add("at least one district is required.");

        var gridRect = new Rect2I(0, 0, c.Grid.Columns, c.Grid.Rows);
        foreach (var d in c.Districts)
        {
            var label = string.IsNullOrEmpty(d.Code) ? "(district without code)" : d.Code;
            if (string.IsNullOrWhiteSpace(d.Code) || string.IsNullOrWhiteSpace(d.Name))
                errors.Add($"{label}: \"code\" and \"name\" are required.");
            if (d.Area.Length != 4 || d.Area[2] < 1 || d.Area[3] < 1)
                errors.Add($"{label}: \"area\" must be [x, y, width, height] with width/height >= 1.");
            else if (!gridRect.Encloses(new Rect2I(d.Area[0], d.Area[1], d.Area[2], d.Area[3])))
                errors.Add($"{label}: area {string.Join(",", d.Area)} is outside the {c.Grid.Columns}x{c.Grid.Rows} grid.");
            if (d.MinLots < 1 || d.MaxLots < d.MinLots)
                errors.Add($"{label}: need 1 <= min_lots <= max_lots.");
            if (d.BuildingColors.Count == 0)
                errors.Add($"{label}: building_colors cannot be empty.");
        }
        if (c.Districts.Select(d => d.Code).Distinct().Count() != c.Districts.Count)
            errors.Add("district codes must be unique.");
        if (c.Traffic.Count > 400)
            errors.Add("traffic.count must be 400 or less (-1 = automatic).");
        if (c.Traffic.Weights.Values.Any(w => w < 0f))
            errors.Add("traffic.weights cannot be negative.");
        if (c.Traffic.LightChance is < 0f or > 1f)
            errors.Add("traffic.light_chance must be between 0 and 1.");
        if (c.Fuel.PricePerLiter <= 0f)
            errors.Add("fuel.price_per_liter must be greater than 0.");
        if (c.Fuel.Stations > 50)
            errors.Add("fuel.stations must be 50 or less (-1 = automatic).");
        if (c.Weather.Weights.Values.Any(w => w < 0f))
            errors.Add("weather.weights cannot be negative.");
        if (c.Rest.Price < 0)
            errors.Add("rest.price cannot be negative.");
        if (c.Rest.Stops > 50)
            errors.Add("rest.stops must be 50 or less (-1 = automatic).");
        return errors;
    }

    /// <summary>Things that still work but are probably mistakes.</summary>
    private static IEnumerable<string> Warnings(CityConfig c)
    {
        var uncovered = 0;
        var overlapping = 0;
        for (var y = 0; y < c.Grid.Rows; y++)
        {
            for (var x = 0; x < c.Grid.Columns; x++)
            {
                var hits = c.Districts.Count(d => d.Area.Length == 4
                    && new Rect2I(d.Area[0], d.Area[1], d.Area[2], d.Area[3]).HasPoint(new Vector2I(x, y)));
                if (hits == 0)
                    uncovered++;
                else if (hits > 1)
                    overlapping++;
            }
        }
        if (uncovered > 0)
            yield return $"{uncovered} block(s) are not in any district and fall back to the first district.";
        if (overlapping > 0)
            yield return $"{overlapping} block(s) are in several districts; the first one listed wins.";
        if (c.Streets.Horizontal.Count > 0 && c.Streets.Horizontal.Count != c.Grid.Rows + 1)
            yield return $"streets.horizontal has {c.Streets.Horizontal.Count} names, expected {c.Grid.Rows + 1} (names repeat).";
        if (c.Streets.Vertical.Count > 0 && c.Streets.Vertical.Count != c.Grid.Columns + 1)
            yield return $"streets.vertical has {c.Streets.Vertical.Count} names, expected {c.Grid.Columns + 1} (names repeat).";
    }
}
