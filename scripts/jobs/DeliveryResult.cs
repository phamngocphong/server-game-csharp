using Godot;

namespace ShipperSimulator;

/// <summary>
/// Summary of a completed delivery, shown in the result popup.
/// RefCounted so it can travel through Godot signals.
/// Future: fuel used, damage...
/// </summary>
public partial class DeliveryResult : RefCounted
{
	public JobData Job { get; set; } = null!;
	/// <summary>Money actually paid (after the late penalty).</summary>
	public int Reward { get; set; }
	public bool WasLate { get; set; }
	/// <summary>Amount cut from <see cref="JobData.Reward"/> for arriving late.</summary>
	public int LatePenaltyAmount { get; set; }
	/// <summary>Seconds spent between pickup and drop-off.</summary>
	public double DeliveryTime { get; set; }
	/// <summary>Extra money from the customer for a fast delivery (not included in <see cref="Reward"/>).</summary>
	public int Tip { get; set; }
	/// <summary>Stars (1-5) the customer gave for this job.</summary>
	public int CustomerStars { get; set; }
	public float RatingBefore { get; set; }
	public float RatingAfter { get; set; }
	public double ElapsedTime { get; set; }
	public int NewBalance { get; set; }
	public int TotalDeliveries { get; set; }
}
