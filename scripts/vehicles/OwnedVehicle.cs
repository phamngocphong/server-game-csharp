using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ShipperSimulator;

/// <summary>One rolled option of a vehicle: its type and strength (0.15 = 15%).</summary>
public readonly record struct PerkRoll(VehiclePerkType Type, float Value);

/// <summary>
/// A concrete vehicle: a model from the <see cref="VehicleCatalog"/> plus the options it
/// was rolled with and what it cost. The player's current vehicle and every shop offer
/// are OwnedVehicles; plain C# so it serializes straight into the save.
/// </summary>
public sealed class OwnedVehicle
{
    /// <summary>Share of the price paid back when this vehicle is traded in.</summary>
    public const float TradeInShare = 0.4f;

    public string ModelId { get; init; } = "";
    /// <summary>What the vehicle is worth (its shop price; the model's base price for the starter).</summary>
    public int Price { get; init; }
    public IReadOnlyList<PerkRoll> Perks { get; init; } = new List<PerkRoll>();

    public int TradeInValue => Mathf.RoundToInt(Price * TradeInShare);

    /// <summary>Total strength of an option (0 when the vehicle does not have it).</summary>
    public float Perk(VehiclePerkType type) => Perks.Where(p => p.Type == type).Sum(p => p.Value);

    public bool HasPerk(VehiclePerkType type) => Perk(type) > 0f;

    /// <summary>The model's handling with the options applied (a copy; the model resource is not changed).</summary>
    public VehicleStats BuildStats(VehicleStats model)
    {
        var stats = (VehicleStats)model.Duplicate();
        var speed = Perk(VehiclePerkType.Speed);
        stats.MaxSpeed *= 1f + speed;
        stats.Acceleration *= 1f + speed * 0.5f;
        var eco = Perk(VehiclePerkType.Eco);
        stats.FuelPerKm *= 1f - eco;
        stats.IdleFuelPerMinute *= 1f - eco;
        stats.FuelCapacity *= 1f + Perk(VehiclePerkType.BigTank);
        return stats;
    }

    /// <summary>"Tuned engine (top speed +15%), Thermal box (...)" or "no options".</summary>
    public string DescribePerks(string separator = "\n") => Perks.Count == 0
        ? "No options"
        : string.Join(separator, Perks.Select(p =>
        {
            var info = VehiclePerks.Get(p.Type);
            return $"{info.Name}: {info.Describe(p.Value)}";
        }));

    public VehicleSaveData ToSaveData() => new()
    {
        ModelId = ModelId,
        Price = Price,
        Perks = Perks.Select(p => new PerkSaveData { Id = VehiclePerks.Get(p.Type).Id, Value = p.Value }).ToList(),
    };

    public static OwnedVehicle FromSaveData(VehicleSaveData data) => new()
    {
        ModelId = data.ModelId,
        Price = data.Price,
        // Unknown ids (an option removed in a later version) are dropped.
        Perks = data.Perks
            .Select(p => (Info: VehiclePerks.FindById(p.Id), p.Value))
            .Where(p => p.Info != null)
            .Select(p => new PerkRoll(p.Info!.Type, Mathf.Clamp(p.Value, 0f, 1f)))
            .ToList(),
    };
}
