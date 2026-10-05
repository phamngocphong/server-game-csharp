using Godot;

namespace ShipperSimulator;

/// <summary>
/// A concrete, generated delivery job. Only plain data types so it can be
/// serialized later (e.g. saving an in-progress job).
/// </summary>
[GlobalClass]
public partial class JobData : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string TemplateId { get; set; } = "";
    [Export] public string PackageName { get; set; } = "";
    [Export] public Color PackageColor { get; set; } = Colors.White;

    [Export] public string PickupName { get; set; } = "";
    [Export] public string PickupDistrict { get; set; } = "";
    [Export] public Vector2 PickupPosition { get; set; }

    [Export] public string DeliveryName { get; set; } = "";
    [Export] public string DeliveryDistrict { get; set; } = "";
    [Export] public Vector2 DeliveryPosition { get; set; }

    [Export] public int Reward { get; set; }
    /// <summary>Estimated route distance pickup -> delivery, in pixels.</summary>
    [Export] public float Distance { get; set; }
    /// <summary>Estimated route distance player -> pickup when the job was generated.</summary>
    [Export] public float DistanceToPickup { get; set; }
}
