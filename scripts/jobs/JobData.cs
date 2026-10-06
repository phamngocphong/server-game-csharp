using Godot;

namespace ShipperSimulator;

/// <summary>
/// A concrete, generated delivery job. Only plain data types so it can be
/// serialized later (e.g. saving an in-progress job).
/// </summary>
[GlobalClass]
public partial class JobData : Resource
{
    public enum JobTier
    {
        Normal,
        /// <summary>Rare: pickup close by, short trip, double pay.</summary>
        Premium,
        /// <summary>Rare: very long trip, half pay, untimed, but the customer adds extra 5-star reviews.</summary>
        Special,
    }

    [Export] public string Id { get; set; } = "";
    [Export] public string TemplateId { get; set; } = "";
    [Export] public string PackageName { get; set; } = "";
    [Export] public Color PackageColor { get; set; } = Colors.White;
    [Export] public JobTemplate.CargoKind Cargo { get; set; }
    [Export] public string CustomerName { get; set; } = "";
    [Export] public JobTier Tier { get; set; }
    /// <summary>Extra 5-star reviews the customer adds on delivery (special jobs).</summary>
    [Export] public int RatingBonus { get; set; }

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

    /// <summary>Seconds to reach the pickup; 0 = no limit. Recalculated when the job is accepted.</summary>
    [Export] public float PickupTimeLimit { get; set; }
    /// <summary>Seconds from pickup to drop-off; 0 = no limit.</summary>
    [Export] public float DeliveryTimeLimit { get; set; }
    /// <summary>Share of the reward lost on a late drop-off (1 = the job fails).</summary>
    [Export] public float LatePenalty { get; set; }
    /// <summary>Deliver within this many seconds after pickup to earn a tip; 0 = no tip.</summary>
    [Export] public float TipTimeLimit { get; set; }
    /// <summary>Tip as a share of <see cref="Reward"/>.</summary>
    [Export] public float TipShare { get; set; }

    public bool IsTimed => DeliveryTimeLimit > 0f;
    public bool HasTip => TipTimeLimit > 0f && TipShare > 0f;
    public int TipAmount => HasTip ? Mathf.Max(1, Mathf.RoundToInt(Reward * TipShare)) : 0;
    public bool IsPassenger => Cargo == JobTemplate.CargoKind.Passenger;
    /// <summary>Package name plus customer, e.g. "Passenger Ride - Ms. Lan".</summary>
    public string Title
    {
        get
        {
            var name = CustomerName.Length > 0 ? $"{PackageName} - {CustomerName}" : PackageName;
            return Tier switch
            {
                JobTier.Premium => $"Premium {name}",
                JobTier.Special => $"Special: {name}",
                _ => name,
            };
        }
    }
}
