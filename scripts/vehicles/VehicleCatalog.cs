using System.Linq;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Every vehicle model in the game (resources/vehicles/vehicle_catalog.tres).
/// A model is a <see cref="VehicleStats"/> .tres: handling, fuel and its shop data.
/// Adding a vehicle = a new VehicleStats .tres added to <see cref="Models"/>.
/// </summary>
[GlobalClass]
public partial class VehicleCatalog : Resource
{
    public const string DefaultPath = "res://resources/vehicles/vehicle_catalog.tres";

    [Export] public Godot.Collections.Array<VehicleStats> Models { get; set; } = new();
    /// <summary>The vehicle new players (and New Game) start with.</summary>
    [Export] public string StarterModelId { get; set; } = "basic_scooter";

    public VehicleStats? Find(string modelId) => Models.FirstOrDefault(m => m.ModelId == modelId);

    public OwnedVehicle CreateStarter()
    {
        var model = Find(StarterModelId);
        return new OwnedVehicle { ModelId = StarterModelId, Price = model?.BasePrice ?? 0 };
    }
}
