using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ShipperSimulator;

/// <summary>A vehicle for sale in the current rotation.</summary>
public sealed record VehicleOffer(string SlotKey, int OfferId, VehicleStats Model, OwnedVehicle Vehicle);

/// <summary>
/// The vehicle shop's stock. It changes at 00:00, 08:00 and 16:00 local time; within a
/// rotation the offers are generated from the rotation key ("2026-10-06#2"), so reopening
/// the shop (or restarting the game) shows the same vehicles.
/// </summary>
public static class VehicleShop
{
    public const int OffersPerRotation = 4;
    public const int RotationHours = 8;

    /// <summary>Key of the rotation <paramref name="localTime"/> falls in, e.g. "2026-10-06#1" for 08:00-15:59.</summary>
    public static string RotationKey(DateTime localTime) =>
        $"{localTime:yyyy-MM-dd}#{localTime.Hour / RotationHours}";

    public static DateTime NextRotation(DateTime localTime) =>
        localTime.Date.AddHours((localTime.Hour / RotationHours + 1) * RotationHours);

    public static List<VehicleOffer> GenerateOffers(VehicleCatalog catalog, string rotationKey)
    {
        var random = new Random(StableHash(rotationKey));
        var forSale = catalog.Models.Where(m => m.ShopWeight > 0f).ToList();
        var offers = new List<VehicleOffer>();
        if (forSale.Count == 0)
            return offers;

        for (var i = 0; i < OffersPerRotation; i++)
        {
            var model = PickWeighted(random, forSale, m => m.ShopWeight);
            var perks = RollPerks(random, model);
            var vehicle = new OwnedVehicle { ModelId = model.ModelId, Price = PriceOf(model, perks), Perks = perks };
            offers.Add(new VehicleOffer(rotationKey, i, model, vehicle));
        }
        return offers;
    }

    /// <summary>Model price plus each option's strength in percent times its price per percent, rounded to $10.</summary>
    public static int PriceOf(VehicleStats model, IEnumerable<PerkRoll> perks)
    {
        var price = model.BasePrice + perks.Sum(p => p.Value * 100f * VehiclePerks.Get(p.Type).PricePerPercent);
        return Mathf.RoundToInt(price / 10f) * 10;
    }

    private static List<PerkRoll> RollPerks(Random random, VehicleStats model)
    {
        var count = random.Next(model.MinPerks, Math.Max(model.MinPerks, model.MaxPerks) + 1);
        var pool = VehiclePerks.All
            .Select(p => (Info: p, Weight: model.PerkWeights.TryGetValue(p.Id, out var w) ? w : 1f))
            .Where(p => p.Weight > 0f)
            .ToList();
        var perks = new List<PerkRoll>();
        while (perks.Count < count && pool.Count > 0)
        {
            var pick = PickWeighted(random, pool, p => p.Weight);
            pool.Remove(pick);
            // Uniform strength in 5% steps between Min and Max.
            var steps = Mathf.RoundToInt((pick.Info.Max - pick.Info.Min) / 0.05f);
            var value = pick.Info.Min + random.Next(0, steps + 1) * 0.05f;
            perks.Add(new PerkRoll(pick.Info.Type, MathF.Round(value, 2)));
        }
        return perks.OrderBy(p => p.Type).ToList();
    }

    private static T PickWeighted<T>(Random random, IReadOnlyList<T> items, Func<T, float> weight)
    {
        var roll = random.NextDouble() * items.Sum(weight);
        foreach (var item in items)
        {
            roll -= weight(item);
            if (roll <= 0)
                return item;
        }
        return items[^1];
    }

    /// <summary>FNV-1a: string.GetHashCode is randomized per process, this is stable across runs.</summary>
    private static int StableHash(string text)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in text)
                hash = (hash ^ c) * 16777619u;
            return (int)hash;
        }
    }
}
