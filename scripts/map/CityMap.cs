using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Builds a city from a <see cref="CityRegionData"/> resource:
/// - roads, sidewalks, parks and water are drawn in _Draw()
/// - buildings and trees are <see cref="Building"/> (StaticBody2D) obstacles; water blocks are plain StaticBody2D
/// - every block side gets a curbside <see cref="DeliveryLocation"/> for the job system
/// - a few of those curbside spots become <see cref="GasStation"/>s and <see cref="RestStop"/>s (not used for jobs)
/// </summary>
public partial class CityMap : Node2D
{
    [Signal] public delegate void MapBuiltEventHandler();

    private const float SidewalkWidth = 22f;
    private const float LotGap = 12f;
    /// <summary>How far into the road (from the block edge) addresses are placed.</summary>
    private const float CurbOffset = 34f;
    private const float BoundaryThickness = 200f;

    private static readonly string[] PlaceNames =
    {
        "Mini Mart", "Pho Shop", "Bakery", "Coffee House", "Pharmacy", "Bookstore",
        "Apartment", "Office Tower", "Tailor", "Flower Shop", "Electronics Store",
        "Noodle Bar", "Clinic", "Hotel", "Print Shop", "Tea House", "Hardware Store",
        "Bike Repair", "Market Stall", "Studio",
    };

    private enum BlockKind { Lots, Park, Water }

    private readonly record struct Block(Rect2 Rect, DistrictData District, BlockKind Kind);

    [Export] public CityRegionData Region { get; set; } = null!;

    /// <summary>Seed used by the last Build(): SeedOverride, else Region.LayoutSeed, or a random one when that is 0.</summary>
    public int ActiveSeed { get; private set; }
    /// <summary>Set before Build() to rebuild a saved layout (wins over Region.LayoutSeed); 0 = not set.</summary>
    public int SeedOverride { get; set; }

    public IReadOnlyList<DeliveryLocation> Locations => _locations;
    public IReadOnlyList<GasStation> GasStations => _gasStations;
    public IReadOnlyList<RestStop> RestStops => _restStops;
    /// <summary>The player's home (one per city).</summary>
    public HomeStop? Home { get; private set; }

    private readonly List<DeliveryLocation> _locations = new();
    private readonly List<GasStation> _gasStations = new();
    private readonly List<RestStop> _restStops = new();
    private readonly List<Block> _blocks = new();
    private readonly RandomNumberGenerator _rng = new();
    private readonly DistrictData _fallbackDistrict = new();
    private Node2D? _obstaclesRoot;

    public override void _Ready() => Build();

    public void Build()
    {
        if (Region == null)
        {
            GD.PushError("CityMap needs a CityRegionData resource.");
            return;
        }
        Clear();
        ActiveSeed = SeedOverride != 0 ? SeedOverride
            : Region.LayoutSeed != 0 ? Region.LayoutSeed
            : (int)(GD.Randi() % int.MaxValue) + 1;
        _rng.Seed = (ulong)ActiveSeed;

        _obstaclesRoot = new Node2D { Name = "Obstacles" };
        AddChild(_obstaclesRoot);

        for (var by = 0; by < Region.GridSize.Y; by++)
        {
            for (var bx = 0; bx < Region.GridSize.X; bx++)
                BuildBlock(new Vector2I(bx, by));
        }
        BuildBoundaries();
        BuildGasStations();
        BuildRestStops();
        BuildHome();
        QueueRedraw();
        EmitSignal(SignalName.MapBuilt);
    }

    // --- Queries --------------------------------------------------------------

    public float Step => Region.BlockSize + Region.RoadWidth;

    public Rect2 GetMapRect()
    {
        var size = (Vector2)Region.GridSize * Step + Vector2.One * Region.RoadWidth;
        return new Rect2(Vector2.Zero, size);
    }

    public Rect2 GetBlockRect(Vector2I cell)
    {
        var origin = Vector2.One * Region.RoadWidth + (Vector2)cell * Step;
        return new Rect2(origin, Vector2.One * Region.BlockSize);
    }

    /// <summary>Center of the intersection between vertical road <paramref name="ix"/> and horizontal road <paramref name="iy"/>.</summary>
    public Vector2 GetIntersection(int ix, int iy) =>
        new Vector2(ix, iy) * Step + Vector2.One * Region.RoadWidth * 0.5f;

    public Vector2 GetSpawnPosition() => GetIntersection(Region.GridSize.X / 2, Region.GridSize.Y / 2);

    public Vector2I GetCellAtPosition(Vector2 position)
    {
        var cell = ((position - Vector2.One * Region.RoadWidth * 0.5f) / Step).Floor();
        return new Vector2I(
            Mathf.Clamp((int)cell.X, 0, Region.GridSize.X - 1),
            Mathf.Clamp((int)cell.Y, 0, Region.GridSize.Y - 1));
    }

    public DistrictData GetDistrictAtPosition(Vector2 position) => DistrictFor(GetCellAtPosition(position));

    public GasStation? GetNearestGasStation(Vector2 position) => Nearest(_gasStations, position);

    public RestStop? GetNearestRestStop(Vector2 position) => Nearest(_restStops, position);

    private static T? Nearest<T>(IEnumerable<T> stops, Vector2 position) where T : ServiceStop
    {
        T? best = null;
        var bestDistance = float.MaxValue;
        foreach (var stop in stops)
        {
            var d = JobGenerator.RouteDistance(position, stop.GlobalPosition);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = stop;
            }
        }
        return best;
    }

    public List<DeliveryLocation> GetLocationsInRange(Vector2 origin, float minRadius, float maxRadius)
    {
        var result = new List<DeliveryLocation>();
        foreach (var loc in _locations)
        {
            var d = origin.DistanceTo(loc.Position);
            if (d >= minRadius && d <= maxRadius)
                result.Add(loc);
        }
        return result;
    }

    // --- Generation -----------------------------------------------------------

    private void Clear()
    {
        _locations.Clear();
        _blocks.Clear();
        _gasStations.Clear();
        _restStops.Clear();
        Home = null;
        _obstaclesRoot?.QueueFree();
        _obstaclesRoot = null;
    }

    private DistrictData DistrictFor(Vector2I cell) => Region.GetDistrictAt(cell) ?? _fallbackDistrict;

    private void BuildBlock(Vector2I cell)
    {
        var rect = GetBlockRect(cell);
        var district = DistrictFor(cell);
        var roll = _rng.Randf();
        var kind = roll < district.WaterChance || district.WaterChance >= 1f ? BlockKind.Water
            : roll < district.WaterChance + district.ParkChance ? BlockKind.Park
            : BlockKind.Lots;
        _blocks.Add(new Block(rect, district, kind));

        var inner = rect.Grow(-SidewalkWidth);
        switch (kind)
        {
            case BlockKind.Water:
                BuildWater(inner);
                break;
            case BlockKind.Park:
                BuildPark(inner);
                break;
            default:
                BuildLots(inner, district);
                break;
        }
        CreateLocations(cell, rect, district, kind);
    }

    /// <summary>Water is drawn in _Draw(); here it only gets a collider so it cannot be driven through.</summary>
    private void BuildWater(Rect2 inner)
    {
        var body = new StaticBody2D
        {
            CollisionLayer = Building.WorldLayer,
            CollisionMask = 0,
            Position = inner.GetCenter(),
        };
        body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = inner.Size } });
        _obstaclesRoot!.AddChild(body);
    }

    private void BuildLots(Rect2 inner, DistrictData district)
    {
        var cols = _rng.RandiRange(district.MinLots, Mathf.Max(district.MinLots, district.MaxLots));
        var rows = _rng.Randf() < 0.6f ? 2 : 1;
        var lotSize = new Vector2(inner.Size.X / cols, inner.Size.Y / rows);
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                if (_rng.Randf() < 0.08f)
                    continue; // empty lot / parking space
                var lot = new Rect2(inner.Position + new Vector2(c * lotSize.X, r * lotSize.Y), lotSize)
                    .Grow(-LotGap * 0.5f);
                var shrink = new Vector2(
                    _rng.RandfRange(0f, lot.Size.X * 0.15f),
                    _rng.RandfRange(0f, lot.Size.Y * 0.15f));
                lot = new Rect2(lot.Position + shrink * 0.5f, lot.Size - shrink);
                SpawnObstacle(lot, PickBuildingColor(district));
            }
        }
    }

    private void BuildPark(Rect2 inner)
    {
        var treeCount = _rng.RandiRange(4, 9);
        var area = inner.Grow(-30f);
        for (var i = 0; i < treeCount; i++)
        {
            var radius = _rng.RandfRange(14f, 24f);
            var position = new Vector2(
                _rng.RandfRange(area.Position.X, area.End.X),
                _rng.RandfRange(area.Position.Y, area.End.Y));
            var treeColor = new Color(0.18f, 0.42f, 0.2f).Lightened(_rng.RandfRange(-0.05f, 0.1f));
            SpawnObstacle(new Rect2(position - Vector2.One * radius, Vector2.One * radius * 2f), treeColor, true);
        }
    }

    private Color PickBuildingColor(DistrictData district)
    {
        if (district.BuildingColors.Length == 0)
            return new Color(0.5f, 0.5f, 0.55f);
        var baseColor = district.BuildingColors[_rng.Randi() % (uint)district.BuildingColors.Length];
        return baseColor.Lightened(_rng.RandfRange(0f, 0.1f)).Darkened(_rng.RandfRange(0f, 0.1f));
    }

    private void SpawnObstacle(Rect2 rect, Color color, bool isRound = false)
    {
        var body = new Building();
        body.Setup(rect.Size, color, isRound);
        body.Position = rect.GetCenter();
        _obstaclesRoot!.AddChild(body);
    }

    private void CreateLocations(Vector2I cell, Rect2 rect, DistrictData district, BlockKind kind)
    {
        (Vector2 Normal, string Street)[] sides =
        {
            (Vector2.Up, Region.GetHorizontalStreet(cell.Y)),
            (Vector2.Down, Region.GetHorizontalStreet(cell.Y + 1)),
            (Vector2.Left, Region.GetVerticalStreet(cell.X)),
            (Vector2.Right, Region.GetVerticalStreet(cell.X + 1)),
        };
        var center = rect.GetCenter();
        var half = rect.Size * 0.5f;
        foreach (var (normal, street) in sides)
        {
            var tangent = new Vector2(-normal.Y, normal.X);
            var vertical = normal.X != 0f;
            var sideLength = vertical ? rect.Size.Y : rect.Size.X;
            var edgeDistance = vertical ? half.X : half.Y;
            var position = center + normal * (edgeDistance + CurbOffset)
                + tangent * _rng.RandfRange(-0.3f, 0.3f) * sideLength;

            var place = kind switch
            {
                BlockKind.Park => "City Park",
                BlockKind.Water => Region.WaterPlaceName,
                _ => PlaceNames[_rng.Randi() % (uint)PlaceNames.Length],
            };
            _locations.Add(new DeliveryLocation
            {
                Id = _locations.Count,
                Position = position,
                District = district,
                StreetName = street,
                DisplayName = $"{place}, {_rng.RandiRange(1, 299)} {street}",
            });
        }
    }

    /// <summary>Turns well-spread curbside addresses into gas stations.</summary>
    private void BuildGasStations()
    {
        var count = Region.FuelStationCount >= 0
            ? Region.FuelStationCount
            : Mathf.Max(3, Region.GridSize.X * Region.GridSize.Y / 12);
        foreach (var loc in TakeSpreadLocations(count, (ulong)ActiveSeed * 17UL + 3UL, new List<Vector2>()))
        {
            var station = new GasStation { Name = $"GasStation{_gasStations.Count}" };
            station.Setup($"Gas Station, {loc.StreetName}", loc.Position);
            _obstaclesRoot!.AddChild(station);
            _gasStations.Add(station);
        }
    }

    /// <summary>Turns well-spread curbside addresses (away from gas stations too) into rest stops.</summary>
    private void BuildRestStops()
    {
        var count = Region.RestStopCount >= 0
            ? Region.RestStopCount
            : Mathf.Max(2, Region.GridSize.X * Region.GridSize.Y / 16);
        var avoid = _gasStations.Select(s => s.Position).ToList();
        foreach (var loc in TakeSpreadLocations(count, (ulong)ActiveSeed * 29UL + 11UL, avoid))
        {
            var stop = new RestStop { Name = $"RestStop{_restStops.Count}" };
            stop.Setup($"Rest Stop, {loc.StreetName}", loc.Position);
            _obstaclesRoot!.AddChild(stop);
            _restStops.Add(stop);
        }
    }

    /// <summary>One curbside address, away from gas stations and rest stops, becomes the player's home.</summary>
    private void BuildHome()
    {
        var avoid = _gasStations.Select(s => s.Position).Concat(_restStops.Select(s => s.Position)).ToList();
        foreach (var loc in TakeSpreadLocations(1, (ulong)ActiveSeed * 41UL + 5UL, avoid))
        {
            Home = new HomeStop { Name = "Home" };
            Home.Setup($"Home, {loc.StreetName}", loc.Position);
            _obstaclesRoot!.AddChild(Home);
        }
    }

    /// <summary>
    /// Picks well-spread addresses with farthest-point sampling (each pick goes where picks and
    /// <paramref name="avoid"/> points are scarcest) and removes them from the job locations.
    /// Uses its own RNG from <paramref name="seed"/>, so it does not change the rest of the layout.
    /// </summary>
    private List<DeliveryLocation> TakeSpreadLocations(int count, ulong seed, List<Vector2> avoid)
    {
        var chosen = new List<DeliveryLocation>();
        count = Mathf.Min(count, _locations.Count / 4);
        if (count <= 0)
            return chosen;

        var rng = new RandomNumberGenerator { Seed = seed };
        var taken = new List<Vector2>(avoid);
        if (taken.Count == 0)
        {
            var first = _locations[rng.RandiRange(0, _locations.Count - 1)];
            chosen.Add(first);
            taken.Add(first.Position);
        }
        while (chosen.Count < count)
        {
            DeliveryLocation? best = null;
            var bestScore = -1f;
            foreach (var loc in _locations)
            {
                if (chosen.Contains(loc))
                    continue;
                var nearest = float.MaxValue;
                foreach (var p in taken)
                    nearest = Mathf.Min(nearest, JobGenerator.RouteDistance(loc.Position, p));
                var score = nearest * rng.RandfRange(0.85f, 1f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = loc;
                }
            }
            if (best == null)
                break;
            chosen.Add(best);
            taken.Add(best.Position);
        }
        foreach (var loc in chosen)
            _locations.Remove(loc);
        return chosen;
    }

    private void BuildBoundaries()
    {
        var mapRect = GetMapRect();
        var t = BoundaryThickness;
        Rect2[] walls =
        {
            new(mapRect.Position.X - t, mapRect.Position.Y - t, mapRect.Size.X + t * 2f, t), // top
            new(mapRect.Position.X - t, mapRect.End.Y, mapRect.Size.X + t * 2f, t), // bottom
            new(mapRect.Position.X - t, mapRect.Position.Y, t, mapRect.Size.Y), // left
            new(mapRect.End.X, mapRect.Position.Y, t, mapRect.Size.Y), // right
        };
        foreach (var wall in walls)
        {
            var body = new StaticBody2D
            {
                CollisionLayer = Building.WorldLayer,
                CollisionMask = 0,
                Position = wall.GetCenter(),
            };
            body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = wall.Size } });
            _obstaclesRoot!.AddChild(body);
        }
    }

    // --- Drawing --------------------------------------------------------------

    public override void _Draw()
    {
        if (Region == null)
            return;
        var mapRect = GetMapRect();
        DrawRect(mapRect.Grow(1200f), Region.RoadColor.Darkened(0.55f));
        DrawRect(mapRect, Region.RoadColor);

        foreach (var block in _blocks)
        {
            var sidewalk = block.District.SidewalkColor;
            DrawRect(block.Rect, sidewalk);
            var inner = block.Rect.Grow(-SidewalkWidth);
            var innerColor = block.Kind switch
            {
                BlockKind.Park => Region.ParkColor,
                BlockKind.Water => Region.WaterColor,
                _ => sidewalk.Darkened(0.15f),
            };
            DrawRect(inner, innerColor);
            if (block.Kind == BlockKind.Water)
                DrawRect(inner, Region.WaterColor.Lightened(0.25f), false, 4f); // shoreline
            DrawRect(block.Rect, sidewalk.Darkened(0.35f), false, 3f);
        }

        DrawLaneMarkings(mapRect);
    }

    private void DrawLaneMarkings(Rect2 mapRect)
    {
        const float dash = 36f;
        const float gap = 30f;
        const float margin = 10f;
        var step = Step;
        var road = Region.RoadWidth;

        for (var iy = 0; iy <= Region.GridSize.Y; iy++)
        {
            var y = iy * step + road * 0.5f;
            for (var x = 0f; x < mapRect.Size.X; x += dash + gap)
            {
                var local = x % step;
                if (local > road + margin && local + dash < step - margin)
                    DrawLine(new Vector2(x, y), new Vector2(x + dash, y), Region.LaneColor, 3f);
            }
        }

        for (var ix = 0; ix <= Region.GridSize.X; ix++)
        {
            var x = ix * step + road * 0.5f;
            for (var y = 0f; y < mapRect.Size.Y; y += dash + gap)
            {
                var local = y % step;
                if (local > road + margin && local + dash < step - margin)
                    DrawLine(new Vector2(x, y), new Vector2(x, y + dash), Region.LaneColor, 3f);
            }
        }
    }
}
