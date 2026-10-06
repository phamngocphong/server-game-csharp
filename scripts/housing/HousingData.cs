using Godot;

namespace ShipperSimulator;

/// <summary>
/// A place to live (one .tres per home in resources/housing/, listed in housing_catalog.tres).
/// Rented homes cost <see cref="MonthlyCost"/> rent at the start of every month; owned homes
/// cost <see cref="BuyPrice"/> once and then only <see cref="MonthlyCost"/> for utilities.
/// Resting at home is free, and faster the better the home.
/// </summary>
[GlobalClass]
public partial class HousingData : Resource
{
    public enum TenureKind { Rent, Own }

    /// <summary>Id stored in saves; must be unique in the catalog.</summary>
    [Export] public string HousingId { get; set; } = "shared_room";
    [Export] public string DisplayName { get; set; } = "Shared Room";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    [Export] public Color AccentColor { get; set; } = new(0.7f, 0.6f, 0.45f);
    [Export] public TenureKind Tenure { get; set; } = TenureKind.Rent;
    /// <summary>Order in the shop; you can only move to a higher tier.</summary>
    [Export] public int Tier { get; set; }

    /// <summary>Price to buy (owned homes only).</summary>
    [Export] public int BuyPrice { get; set; }
    /// <summary>Rent (rented homes) or utilities (owned homes), charged at the start of each month.</summary>
    [Export] public int MonthlyCost { get; set; } = 120;
    /// <summary>Seconds a rest at home takes to bring fatigue down to 0.</summary>
    [Export] public float RestSeconds { get; set; } = 12f;

    public bool IsOwned => Tenure == TenureKind.Own;

    /// <summary>"rent $300 / month" or "utilities $80 / month".</summary>
    public string MonthlyText => $"{(IsOwned ? "utilities" : "rent")} {GameManager.FormatMoney(MonthlyCost)} / month";
}
