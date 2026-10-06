using Godot;

namespace ShipperSimulator;

/// <summary>
/// One kind of AI road user (one .tres per kind in resources/traffic/).
/// <see cref="CollisionScore"/> decides how long the player is stunned after crashing into it.
/// </summary>
[GlobalClass]
public partial class TrafficVehicleData : Resource
{
    public enum BodyShape { Bicycle, Motorbike, Car, Bus }

    /// <summary>Seconds the player cannot drive per collision point.</summary>
    public const float StunSecondsPerPoint = 0.6f;

    /// <summary>Id used by the "traffic.weights" section of the city JSON files.</summary>
    [Export] public string VehicleId { get; set; } = "car";
    [Export] public string DisplayName { get; set; } = "Car";
    [Export] public BodyShape Shape { get; set; } = BodyShape.Car;

    [ExportGroup("Size")]
    /// <summary>Length along the driving direction, in pixels.</summary>
    [Export] public float Length { get; set; } = 76f;
    [Export] public float Width { get; set; } = 36f;
    /// <summary>Body colors; each spawned vehicle picks one.</summary>
    [Export] public Color[] Colors { get; set; } = { new(0.8f, 0.2f, 0.2f) };

    [ExportGroup("Driving")]
    /// <summary>Cruise speed range in px/s (the player's scooter tops out at 560).</summary>
    [Export] public float MinSpeed { get; set; } = 150f;
    [Export] public float MaxSpeed { get; set; } = 220f;

    [ExportGroup("Collision")]
    /// <summary>
    /// How bad a crash into this vehicle is. The player is stunned for
    /// CollisionScore * <see cref="StunSecondsPerPoint"/> seconds.
    /// </summary>
    [Export(PropertyHint.Range, "1,10")] public int CollisionScore { get; set; } = 4;

    [ExportGroup("Spawning")]
    /// <summary>Default relative spawn chance; a city's "traffic.weights" can override it.</summary>
    [Export] public float SpawnWeight { get; set; } = 1f;

    public float StunSeconds => CollisionScore * StunSecondsPerPoint;
}
