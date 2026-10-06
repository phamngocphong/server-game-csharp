using Godot;

namespace ShipperSimulator;

/// <summary>
/// A vehicle model: handling, fuel and shop data (one .tres per model in resources/vehicles/,
/// listed in vehicle_catalog.tres). The options a vehicle is sold with are applied on top
/// by <see cref="OwnedVehicle.BuildStats"/>.
/// </summary>
[GlobalClass]
public partial class VehicleStats : Resource
{
    [Export] public string VehicleName { get; set; } = "Scooter";
    /// <summary>Id stored in saves; must be unique in the catalog.</summary>
    [Export] public string ModelId { get; set; } = "scooter";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    [Export] public Color BodyColor { get; set; } = new(0.85f, 0.2f, 0.2f);

    [ExportGroup("Speed")]
    /// <summary>px/s</summary>
    [Export] public float MaxSpeed { get; set; } = 560f;
    [Export] public float ReverseMaxSpeed { get; set; } = 150f;
    [Export] public float Acceleration { get; set; } = 420f;
    [Export] public float BrakeDeceleration { get; set; } = 950f;
    /// <summary>Natural slow-down when no throttle.</summary>
    [Export] public float CoastDeceleration { get; set; } = 260f;

    [ExportGroup("Handling")]
    /// <summary>Max turn rate in rad/s.</summary>
    [Export] public float TurnSpeed { get; set; } = 3f;
    /// <summary>How quickly velocity aligns with the heading (higher = less drift).</summary>
    [Export] public float Grip { get; set; } = 9f;
    /// <summary>Grip while holding the handbrake (drifting).</summary>
    [Export] public float HandbrakeGrip { get; set; } = 2.2f;

    [ExportGroup("Fuel")]
    /// <summary>Tank size in liters.</summary>
    [Export] public float FuelCapacity { get; set; } = 4f;
    /// <summary>Liters per km driven (before the weather multiplier).</summary>
    [Export] public float FuelPerKm { get; set; } = 0.08f;
    /// <summary>Liters per minute with the engine running, even when standing still.</summary>
    [Export] public float IdleFuelPerMinute { get; set; } = 0.02f;
    /// <summary>Top speed when the tank is empty and the rider pushes the bike (px/s).</summary>
    [Export] public float PushSpeed { get; set; } = 70f;

    [ExportGroup("Shop")]
    /// <summary>Price without options; also the value of the starter vehicle.</summary>
    [Export] public int BasePrice { get; set; } = 300;
    /// <summary>Relative chance to appear in the shop; 0 = never sold (e.g. the starter).</summary>
    [Export] public float ShopWeight { get; set; } = 1f;
    /// <summary>How many options a shop vehicle of this model is rolled with (inclusive range).</summary>
    [Export(PropertyHint.Range, "0,5")] public int MinPerks { get; set; }
    [Export(PropertyHint.Range, "0,5")] public int MaxPerks { get; set; } = 2;
    /// <summary>Chance per option id (speed, eco, thermal, guard, tank); missing ids = 1, 0 = never.</summary>
    [Export] public Godot.Collections.Dictionary<string, float> PerkWeights { get; set; } = new();
}
