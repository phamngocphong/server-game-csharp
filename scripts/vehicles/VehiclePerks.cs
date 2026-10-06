using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ShipperSimulator;

public enum VehiclePerkType { Speed, Eco, ThermalBox, CrashGuard, BigTank }

/// <summary>
/// One option a vehicle can come with. <see cref="Min"/>/<see cref="Max"/> are the possible
/// strengths as fractions (0.15 = 15%); the shop rolls a value in between, in 5% steps.
/// </summary>
public sealed record VehiclePerkInfo(
    VehiclePerkType Type, string Id, string Name, float Min, float Max, int PricePerPercent, string EffectFormat)
{
    /// <summary>Short effect text for a strength, e.g. "top speed +15%".</summary>
    public string Describe(float value) => string.Format(EffectFormat, Mathf.RoundToInt(value * 100f));
}

/// <summary>
/// The table of vehicle options. Ids are what save files and VehicleStats.PerkWeights use.
/// How each option works:
/// - Speed: MaxSpeed +x, Acceleration +x/2 (<see cref="OwnedVehicle.BuildStats"/>)
/// - Eco: fuel per km and idle fuel -x
/// - ThermalBox: timed package jobs (food...) lose x less to the late penalty and get x more tip
///   (<see cref="GameManager.LatePenaltyFor"/>, <see cref="GameManager.TipFor"/>)
/// - CrashGuard: stun after crashing into traffic -x (<see cref="Player"/>)
/// - BigTank: fuel capacity +x
/// </summary>
public static class VehiclePerks
{
    public static readonly IReadOnlyList<VehiclePerkInfo> All = new[]
    {
        new VehiclePerkInfo(VehiclePerkType.Speed, "speed", "Tuned engine", 0.05f, 0.25f, 20, "top speed +{0}%"),
        new VehiclePerkInfo(VehiclePerkType.Eco, "eco", "Eco engine", 0.10f, 0.40f, 6, "fuel use -{0}%"),
        new VehiclePerkInfo(VehiclePerkType.ThermalBox, "thermal", "Thermal box", 0.20f, 0.60f, 5, "late penalty -{0}%, tips +{0}%"),
        new VehiclePerkInfo(VehiclePerkType.CrashGuard, "guard", "Crash guard", 0.20f, 0.60f, 5, "crash stun -{0}%"),
        new VehiclePerkInfo(VehiclePerkType.BigTank, "tank", "Big tank", 0.25f, 0.75f, 3, "fuel tank +{0}%"),
    };

    public static VehiclePerkInfo Get(VehiclePerkType type) => All.First(p => p.Type == type);

    public static VehiclePerkInfo? FindById(string id) => All.FirstOrDefault(p => p.Id == id);
}
