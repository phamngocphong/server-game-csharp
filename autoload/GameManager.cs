using System;
using System.Globalization;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Singleton holding global game state (wallet, statistics, world references)
/// and shared helpers. Gameplay flow lives in dedicated nodes (JobManager,
/// Player...) which talk to each other through <see cref="EventBus"/>.
/// </summary>
public partial class GameManager : Node
{
	/// <summary>World scale used for display: how many pixels make one kilometre.</summary>
	public const float PixelsPerKm = 2000f;
	/// <summary>Converts velocity (px/s) into a believable km/h readout.</summary>
	public const float SpeedToKmh = 0.12f;
	public const string CurrencySymbol = "$";
	private const double AutosaveInterval = 60.0;

	public static GameManager Instance { get; private set; } = null!;

	public Wallet Wallet { get; } = new();
	public PlayerStats Stats { get; } = new();
	public Player? Player { get; private set; }
	public CityMap? CityMap { get; private set; }

	private double _autosaveTimer;

	public override void _EnterTree() => Instance = this;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Wallet.BalanceChanged += OnWalletBalanceChanged;
		Stats.Changed += OnStatsChanged;
	}

	public override void _Process(double delta)
	{
		if (Player == null)
			return;
		Stats.PlayTime += delta;
		_autosaveTimer += delta;
		if (_autosaveTimer >= AutosaveInterval)
		{
			_autosaveTimer = 0.0;
			SaveManager.Instance.SaveGame(silent: true);
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("quick_save") && Player != null)
			SaveManager.Instance.SaveGame();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest && Player != null)
			SaveManager.Instance.SaveGame(silent: true);
	}

	public void RegisterWorld(CityMap map, Player player)
	{
		CityMap = map;
		Player = player;
	}

	/// <summary>Called when the gameplay scene leaves the tree, so autosave and stats stop touching freed nodes.</summary>
	public void UnregisterWorld()
	{
		CityMap = null;
		Player = null;
		_autosaveTimer = 0.0;
	}

	public Vector2 GetPlayerPosition() => Player?.GlobalPosition ?? Vector2.Zero;

	public DistrictData? GetCurrentDistrict()
	{
		if (CityMap == null || Player == null)
			return null;
		return CityMap.GetDistrictAtPosition(Player.GlobalPosition);
	}

	/// <summary>
	/// Global reward multiplier hook. Future systems (reputation, weather,
	/// rush hour, vehicle perks...) plug in here.
	/// </summary>
	public float GetRewardMultiplier(JobTemplate template) => 1f;

	/// <summary>Pays out a finished job (minus the late penalty) and records statistics.</summary>
	public DeliveryResult CompleteDelivery(JobData job, double elapsedTime, double deliveryTime, bool late)
	{
		var penalty = late ? Mathf.RoundToInt(job.Reward * job.LatePenalty) : 0;
		var reward = Mathf.Max(1, job.Reward - penalty);
		var lateTag = late ? " (late)" : "";
		Wallet.Add(reward, $"{job.Title}{lateTag}: {job.PickupName} -> {job.DeliveryName}");
		Stats.RecordDelivery(reward, job.Distance);

		var result = new DeliveryResult
		{
			Job = job,
			Reward = reward,
			WasLate = late,
			LatePenaltyAmount = job.Reward - reward,
			DeliveryTime = deliveryTime,
			ElapsedTime = elapsedTime,
			NewBalance = Wallet.Balance,
			TotalDeliveries = Stats.TotalDeliveries,
		};

		SaveManager.Instance.SaveGame(silent: true);
		return result;
	}

	// --- Formatting helpers ---------------------------------------------------

	public static string FormatMoney(int amount)
	{
		var digits = Math.Abs(amount).ToString("N0", CultureInfo.InvariantCulture);
		return (amount < 0 ? "-" : "") + CurrencySymbol + digits;
	}

	public static string FormatDistance(float pixels)
	{
		var km = pixels / PixelsPerKm;
		if (km < 1f)
			return $"{Mathf.RoundToInt(km * 1000f)} m";
		return km.ToString("0.0", CultureInfo.InvariantCulture) + " km";
	}

	public static string FormatTime(double seconds)
	{
		var total = (int)seconds;
		return $"{total / 60}:{total % 60:00}";
	}

	// --- Save data ------------------------------------------------------------

	public GameSaveData ToSaveData() => new()
	{
		Wallet = Wallet.ToSaveData(),
		Stats = Stats.ToSaveData(),
		RegionId = CityMap?.Region.RegionId ?? "",
		LayoutSeed = CityMap?.ActiveSeed ?? 0,
		Player = Player?.GetSaveData(),
	};

	public void ApplySaveData(GameSaveData data)
	{
		Wallet.LoadSaveData(data.Wallet);
		Stats.LoadSaveData(data.Stats);
		// A position is only meaningful on the same city layout; otherwise keep the spawn point.
		var sameMap = CityMap != null && data.RegionId == CityMap.Region.RegionId && data.LayoutSeed == CityMap.ActiveSeed;
		if (Player != null && data.Player != null && sameMap)
			Player.ApplySaveData(data.Player);
	}

	private void OnWalletBalanceChanged(int balance, int delta) =>
		EventBus.Instance.EmitSignal(EventBus.SignalName.BalanceChanged, balance, delta);

	private void OnStatsChanged() =>
		EventBus.Instance.EmitSignal(EventBus.SignalName.StatsChanged);
}
