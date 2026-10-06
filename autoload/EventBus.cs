using Godot;
using Godot.Collections;

namespace ShipperSimulator;

/// <summary>
/// Global signal hub. Systems emit and listen here instead of holding direct
/// references to each other, so new features (fuel, weather, traffic...) can
/// hook into the game loop without touching existing code.
/// </summary>
public partial class EventBus : Node
{
	public static EventBus Instance { get; private set; } = null!;

	// --- Job flow -------------------------------------------------------------
	/// <summary>The list of jobs offered on the board changed.</summary>
	[Signal] public delegate void JobsUpdatedEventHandler(Array<JobData> jobs);

	// UI -> JobManager requests.
	[Signal] public delegate void JobAcceptRequestedEventHandler(JobData job);
	[Signal] public delegate void JobRefreshRequestedEventHandler();
	[Signal] public delegate void JobCancelRequestedEventHandler();

	// JobManager -> everyone.
	[Signal] public delegate void JobAcceptedEventHandler(JobData job);
	[Signal] public delegate void JobPickedUpEventHandler(JobData job);
	[Signal] public delegate void JobDeliveredEventHandler(DeliveryResult result);
	[Signal] public delegate void JobCancelledEventHandler(JobData job);
	/// <summary>A timed job ran out of time; <paramref name="reason"/> is shown to the player.</summary>
	[Signal] public delegate void JobFailedEventHandler(JobData job, string reason);
	/// <summary>Every frame of a timed job. <paramref name="remaining"/> goes negative when late.</summary>
	[Signal] public delegate void JobTimerUpdatedEventHandler(float remaining, float limit);
	/// <summary><paramref name="state"/> is a <see cref="JobManager.State"/>; job is null when idle.</summary>
	[Signal] public delegate void JobStateChangedEventHandler(int state, JobData job);

	// --- Navigation / interaction ---------------------------------------------
	[Signal] public delegate void NavigationTargetChangedEventHandler(Vector2 target, string label, Color color);
	[Signal] public delegate void NavigationTargetClearedEventHandler();
	/// <summary>Empty text hides the prompt.</summary>
	[Signal] public delegate void InteractionPromptChangedEventHandler(string text);

	// --- Economy / stats ------------------------------------------------------
	[Signal] public delegate void BalanceChangedEventHandler(int balance, int delta);
	[Signal] public delegate void StatsChangedEventHandler();

	// --- UI -------------------------------------------------------------------
	[Signal] public delegate void JobBoardOpenRequestedEventHandler();
	[Signal] public delegate void JobBoardOpenedEventHandler();
	[Signal] public delegate void DeliveryPopupClosedEventHandler();
	[Signal] public delegate void PlayerControlsLockedEventHandler(bool locked);
	[Signal] public delegate void NotificationRequestedEventHandler(string text);

	// --- Persistence ----------------------------------------------------------
	[Signal] public delegate void GameSavedEventHandler();
	[Signal] public delegate void GameLoadedEventHandler();

	public override void _EnterTree() => Instance = this;
}
