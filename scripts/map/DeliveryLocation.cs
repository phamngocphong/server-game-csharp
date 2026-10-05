using Godot;

namespace ShipperSimulator;

/// <summary>A curbside address on the map that can be used as pickup or delivery point.</summary>
public sealed class DeliveryLocation
{
    public int Id { get; init; } = -1;
    public string DisplayName { get; init; } = "";
    public string StreetName { get; init; } = "";
    public Vector2 Position { get; init; }
    public DistrictData? District { get; init; }

    public string DistrictName => District?.DisplayName ?? "";
}
