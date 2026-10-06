using Godot;

namespace ShipperSimulator;

/// <summary>
/// Summary of a completed delivery, shown in the result popup.
/// RefCounted so it can travel through Godot signals.
/// Future: tips, reputation change, fuel used...
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
	public double ElapsedTime { get; set; }
	public int NewBalance { get; set; }
	public int TotalDeliveries { get; set; }
}
