using System.Linq;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Every home in the game (resources/housing/housing_catalog.tres), shown in the shop's
/// Housing tab. The starter is where new players (and New Game) live.
/// </summary>
[GlobalClass]
public partial class HousingCatalog : Resource
{
    public const string DefaultPath = "res://resources/housing/housing_catalog.tres";

    [Export] public Godot.Collections.Array<HousingData> Homes { get; set; } = new();
    [Export] public string StarterHousingId { get; set; } = "shared_room";

    public HousingData? Find(string housingId) => Homes.FirstOrDefault(h => h.HousingId == housingId);

    /// <summary>The starter home (falls back to the lowest tier).</summary>
    public HousingData Starter() => Find(StarterHousingId) ?? Homes.OrderBy(h => h.Tier).First();
}
