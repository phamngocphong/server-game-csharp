using Godot;

namespace ShipperSimulator;

/// <summary>
/// Summary of a completed delivery, shown in the result popup.
/// RefCounted so it can travel through Godot signals.
/// Future: tips, late penalties, reputation change, fuel used...
/// </summary>
public partial class DeliveryResult : RefCounted
{
	public JobData Job { get; set; } = null!;
	public int Reward { get; set; }
	public double ElapsedTime { get; set; }
	public int NewBalance { get; set; }
	public int TotalDeliveries { get; set; }
}
