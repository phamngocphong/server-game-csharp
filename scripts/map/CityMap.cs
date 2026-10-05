using System.Collections.Generic;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Builds a city from a <see cref="CityRegionData"/> resource:
/// - roads, sidewalks and parks are drawn in _Draw()
/// - buildings and trees are <see cref="Building"/> (StaticBody2D) obstacles
/// - every block side gets a curbside <see cref="DeliveryLocation"/> for the job system
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

    private readonly record struct Block(Rect2 Rect, DistrictData District, bool IsPark);

    [Export] public CityRegionData Region { get; set; } = null!;

    public IReadOnlyList<DeliveryLocation> Locations => _locations;

    private readonly List<DeliveryLocation> _locations = new();
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
        _rng.Seed = (ulong)Region.LayoutSeed;

        _obstaclesRoot = new Node2D { Name = "Obstacles" };
        AddChild(_obstaclesRoot);

        for (var by = 0; by < Region.GridSize.Y; by++)
        {
            for (var bx = 0; bx < Region.GridSize.X; bx++)
                BuildBlock(new Vector2I(bx, by));
        }
        BuildBoundaries();
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
        _obstaclesRoot?.QueueFree();
        _obstaclesRoot = null;
    }

    private DistrictData DistrictFor(Vector2I cell) => Region.GetDistrictAt(cell) ?? _fallbackDistrict;

    private void BuildBlock(Vector2I cell)
    {
        var rect = GetBlockRect(cell);
        var district = DistrictFor(cell);
        var isPark = _rng.Randf() < district.ParkChance;
        _blocks.Add(new Block(rect, district, isPark));

        var inner = rect.Grow(-SidewalkWidth);
        if (isPark)
            BuildPark(inner);
        else
            BuildLots(inner, district);
        CreateLocations(cell, rect, district, isPark);
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

    private void CreateLocations(Vector2I cell, Rect2 rect, DistrictData district, bool isPark)
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

            var place = isPark ? "City Park" : PlaceNames[_rng.Randi() % (uint)PlaceNames.Length];
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
            var innerColor = block.IsPark ? Region.ParkColor : sidewalk.Darkened(0.15f);
            DrawRect(block.Rect.Grow(-SidewalkWidth), innerColor);
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
