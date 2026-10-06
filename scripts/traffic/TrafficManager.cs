using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Spawns the AI road users when the gameplay scene starts. How many and which
/// kinds come from the city's "traffic" JSON section (<see cref="CityRegionData.TrafficCount"/>,
/// <see cref="CityRegionData.TrafficWeights"/>); the kinds themselves are
/// <see cref="TrafficVehicleData"/> resources in resources/traffic/.
/// </summary>
public partial class TrafficManager : Node2D
{
    /// <summary>Vehicles per city block when the city JSON does not set "traffic.count".</summary>
    public const float AutoDensity = 0.6f;
    /// <summary>No vehicle spawns closer than this to the player's spawn point.</summary>
    private const float SpawnClearance = 450f;

    [Export] public CityMap Map { get; set; } = null!;
    /// <summary>Optional: vehicles stop at its red lights.</summary>
    [Export] public TrafficLights? Lights { get; set; }
    [Export] public Godot.Collections.Array<TrafficVehicleData> VehicleTypes { get; set; } = new();

    public IReadOnlyList<TrafficVehicle> Vehicles => _vehicles;

    private readonly List<TrafficVehicle> _vehicles = new();
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _rng.Randomize();
        if (Map?.Region == null || VehicleTypes.Count == 0)
            return;
        Spawn(VehicleCount());
    }

    private int VehicleCount()
    {
        var region = Map.Region;
        return region.TrafficCount >= 0
            ? region.TrafficCount
            : Mathf.RoundToInt(region.GridSize.X * region.GridSize.Y * AutoDensity);
    }

    private void Spawn(int count)
    {
        var weights = VehicleTypes.Select(WeightOf).ToList();
        var total = weights.Sum();
        if (total <= 0f)
            return;

        var avoid = Map.GetSpawnPosition();
        var grid = Map.Region.GridSize;
        Vector2I[] dirs = { Vector2I.Right, Vector2I.Left, Vector2I.Down, Vector2I.Up };
        var attempts = 0;
        while (_vehicles.Count < count && attempts++ < count * 20)
        {
            var data = PickType(weights, total);
            var from = new Vector2I(_rng.RandiRange(0, grid.X), _rng.RandiRange(0, grid.Y));
            var dir = dirs[_rng.RandiRange(0, dirs.Length - 1)];
            var to = from + dir;
            if (to.X < 0 || to.Y < 0 || to.X > grid.X || to.Y > grid.Y)
                continue;

            var vehicle = new TrafficVehicle { Name = $"{data.VehicleId}_{_vehicles.Count}" };
            // Spawn well before the next stop line, so no vehicle starts inside a red light.
            vehicle.Setup(data, Map, Lights, from, dir, _rng.RandfRange(0.1f, 0.6f), _rng);
            if (vehicle.Position.DistanceTo(avoid) < SpawnClearance || Overlaps(vehicle))
            {
                vehicle.Free();
                continue;
            }
            AddChild(vehicle);
            _vehicles.Add(vehicle);
        }
    }

    private bool Overlaps(TrafficVehicle candidate)
    {
        foreach (var other in _vehicles)
        {
            var minGap = (candidate.Data.Length + other.Data.Length) * 0.5f + 30f;
            if (candidate.Position.DistanceTo(other.Position) < minGap)
                return true;
        }
        return false;
    }

    private float WeightOf(TrafficVehicleData data)
    {
        var overrides = Map.Region.TrafficWeights;
        return Mathf.Max(0f, overrides.TryGetValue(data.VehicleId, out var w) ? w : data.SpawnWeight);
    }

    private TrafficVehicleData PickType(List<float> weights, float total)
    {
        var roll = _rng.Randf() * total;
        for (var i = 0; i < VehicleTypes.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0f && weights[i] > 0f)
                return VehicleTypes[i];
        }
        return VehicleTypes[weights.FindLastIndex(w => w > 0f)];
    }
}
