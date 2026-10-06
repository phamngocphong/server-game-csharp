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
	/// <summary>The player crashed into traffic and cannot drive for <paramref name="stunSeconds"/>.</summary>
	[Signal] public delegate void PlayerCrashedEventHandler(string vehicleName, int collisionScore, float stunSeconds);
	/// <summary>The player was fined <paramref name="amount"/>; <paramref name="multiplier"/> > 1 for repeat offences.</summary>
	[Signal] public delegate void TrafficFinedEventHandler(int amount, string reason, int multiplier);
	/// <summary>A new part of the day started (morning, midday...); also sent once at scene start.</summary>
	[Signal] public delegate void PeriodChangedEventHandler(string periodName);
	/// <summary>The weather changed (also sent once when the gameplay scene starts).</summary>
	[Signal] public delegate void WeatherChangedEventHandler(string weatherName);
	/// <summary>Empty text hides the prompt.</summary>
	[Signal] public delegate void InteractionPromptChangedEventHandler(string text);

	// --- Economy / stats ------------------------------------------------------
	[Signal] public delegate void BalanceChangedEventHandler(int balance, int delta);
	[Signal] public delegate void StatsChangedEventHandler();
	/// <summary>The driver rating or the failure counters changed.</summary>
	[Signal] public delegate void ReputationChangedEventHandler();
	/// <summary>Text for the Job Board about job types the rating locks or makes rarer (empty = none).</summary>
	[Signal] public delegate void JobBoardNoticeChangedEventHandler(string text);

	// --- UI -------------------------------------------------------------------
	[Signal] public delegate void JobBoardOpenRequestedEventHandler();
	[Signal] public delegate void JobBoardOpenedEventHandler();
	/// <summary>The pause menu's "Vehicle Shop" button was pressed.</summary>
	[Signal] public delegate void ShopRequestedEventHandler();
	/// <summary>The player's phone changed (bought in the shop, or loaded from a save).</summary>
	[Signal] public delegate void PhoneChangedEventHandler();
	/// <summary>A job refresh was used, or the daily refreshes were reset.</summary>
	[Signal] public delegate void RefreshesChangedEventHandler();
	/// <summary>The player's vehicle changed (bought in the shop, or loaded from a save).</summary>
	[Signal] public delegate void VehicleChangedEventHandler();
	/// <summary>The on-screen pause button was pressed (Esc / P go straight to the pause menu).</summary>
	[Signal] public delegate void PauseMenuRequestedEventHandler();
	[Signal] public delegate void DeliveryPopupClosedEventHandler();
	[Signal] public delegate void PlayerControlsLockedEventHandler(bool locked);
	[Signal] public delegate void NotificationRequestedEventHandler(string text);

	// --- Persistence ----------------------------------------------------------
	[Signal] public delegate void GameSavedEventHandler();
	[Signal] public delegate void GameLoadedEventHandler();
	/// <summary>GameSettings changed (SaveManager.SaveSettings).</summary>
	[Signal] public delegate void SettingsChangedEventHandler();

	public override void _EnterTree() => Instance = this;
}
