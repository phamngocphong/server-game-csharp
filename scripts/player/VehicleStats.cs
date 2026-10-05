using Godot;

namespace ShipperSimulator;

/// <summary>
/// Handling data for a vehicle. Upgrades / new bikes = new or modified
/// VehicleStats resources (resources/vehicles/). Fuel capacity etc. go here later.
/// </summary>
[GlobalClass]
public partial class VehicleStats : Resource
{
    [Export] public string VehicleName { get; set; } = "Scooter";

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
}
