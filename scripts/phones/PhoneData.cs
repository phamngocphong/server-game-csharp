using Godot;

namespace ShipperSimulator;

/// <summary>
/// A phone model (one .tres per phone in resources/phones/, listed in phone_catalog.tres).
/// The phone decides how many jobs the Job Board shows, and better phones add job refreshes per day.
/// </summary>
[GlobalClass]
public partial class PhoneData : Resource
{
    /// <summary>Id stored in saves; must be unique in the catalog.</summary>
    [Export] public string PhoneId { get; set; } = "basic_phone";
    [Export] public string DisplayName { get; set; } = "Basic Phone";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    [Export] public Color CaseColor { get; set; } = new(0.3f, 0.3f, 0.35f);
    [Export] public int Price { get; set; }

    /// <summary>Jobs on the board (times the part of the day's JobCountMultiplier, at least 1).</summary>
    [Export(PropertyHint.Range, "1,12")] public int JobSlots { get; set; } = 3;
    /// <summary>Job refreshes per day on top of GameManager.FreeRefreshesPerDay.</summary>
    [Export(PropertyHint.Range, "0,10")] public int ExtraRefreshes { get; set; }
}
