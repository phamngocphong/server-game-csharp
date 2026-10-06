using System.Linq;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Every phone in the game (resources/phones/phone_catalog.tres). All of them are always
/// for sale in the shop's Phones tab; the starter is what new players (and New Game) get.
/// </summary>
[GlobalClass]
public partial class PhoneCatalog : Resource
{
    public const string DefaultPath = "res://resources/phones/phone_catalog.tres";

    [Export] public Godot.Collections.Array<PhoneData> Phones { get; set; } = new();
    [Export] public string StarterPhoneId { get; set; } = "basic_phone";

    public PhoneData? Find(string phoneId) => Phones.FirstOrDefault(p => p.PhoneId == phoneId);

    /// <summary>The starter phone (falls back to the first one).</summary>
    public PhoneData Starter() => Find(StarterPhoneId) ?? Phones[0];
}
