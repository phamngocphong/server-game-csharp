using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
	/// <summary>Gameplay scene kept loaded for the whole session; see <see cref="_gameScene"/>.</summary>
	public const string GameScenePath = "res://scenes/main.tscn";

	public static GameManager Instance { get; private set; } = null!;

	public enum StartMode
	{
		/// <summary>Load the save (its city, layout seed and position) if there is one.</summary>
		Continue,
		/// <summary>Fresh progress in a random city; the old save is already deleted.</summary>
		NewGame,
	}

	/// <summary>How the next gameplay scene starts; set by the main menu, read by Main.</summary>
	public StartMode NextStart { get; set; } = StartMode.Continue;

	public Wallet Wallet { get; } = new();
	public PlayerStats Stats { get; } = new();
	public Reputation Reputation { get; } = new();
	/// <summary>All vehicle models (loaded in _Ready).</summary>
	public VehicleCatalog Catalog { get; private set; } = null!;
	/// <summary>The player's vehicle and its options; survives scene changes, saved in the game save.</summary>
	public OwnedVehicle Vehicle { get; private set; } = new();

	/// <summary>Job refreshes everyone gets per day; the phone can add more (PhoneData.ExtraRefreshes).</summary>
	public const int FreeRefreshesPerDay = 3;

	/// <summary>All phones (loaded in _Ready).</summary>
	public PhoneCatalog Phones { get; private set; } = null!;
	/// <summary>The player's phone: how many jobs the board shows.</summary>
	public PhoneData Phone { get; private set; } = null!;
	/// <summary>Date ("yyyy-MM-dd") to use instead of today's, for testing the daily reset; empty = real date.</summary>
	public string TodayOverride { get; set; } = "";

	private string _refreshDay = "";
	private int _refreshesUsed;
	private string _shopRotation = "";
	private readonly HashSet<int> _soldOffers = new();
	public Player? Player { get; private set; }
	public CityMap? CityMap { get; private set; }
	/// <summary>Weather of the gameplay scene; set by WeatherSystem while it is in the tree.</summary>
	public WeatherSystem? Weather { get; set; }
	/// <summary>Time of day of the gameplay scene; set by DayCycle while it is in the tree.</summary>
	public DayCycle? DayCycle { get; set; }

	/// <summary>Active part of the day, or null outside the gameplay scene.</summary>
	public TimePeriodData? Period => DayCycle?.Current;

	private double _autosaveTimer;

	/// <summary>
	/// Keeps the gameplay scene, and through it every C#-scripted .tres it uses (JobTemplate,
	/// TrafficVehicleData, WeatherData, VehicleStats), natively referenced for the app's lifetime.
	/// Without this, freeing the scene leaves those resources held only by their C# wrapper:
	/// Godot makes the GC handle weak but keeps the object in ResourceCache. The next load of
	/// main.tscn re-references the cached object on the main thread (weak -> strong handle swap)
	/// while the GC finalizer thread may be disposing it, which crashes with
	/// "gchandle.is_released()" or "Handle is not initialized" (an engine race).
	/// </summary>
	private PackedScene? _gameScene;

	public override void _EnterTree() => Instance = this;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_gameScene = GD.Load<PackedScene>(GameScenePath);
		Wallet.BalanceChanged += OnWalletBalanceChanged;
		Stats.Changed += OnStatsChanged;
		Reputation.Changed += OnReputationChanged;
		Catalog = GD.Load<VehicleCatalog>(VehicleCatalog.DefaultPath);
		Vehicle = Catalog.CreateStarter();
		Phones = GD.Load<PhoneCatalog>(PhoneCatalog.DefaultPath);
		Phone = Phones.Starter();
	}

	public override void _Process(double delta)
	{
		// This node always processes (for quick save); play time and autosave stop while paused.
		if (Player == null || GetTree().Paused)
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
		ApplyVehicleToPlayer(fillTank: true);
	}

	/// <summary>Called when the gameplay scene leaves the tree, so autosave and stats stop touching freed nodes.</summary>
	public void UnregisterWorld()
	{
		CityMap = null;
		Player = null;
		_autosaveTimer = 0.0;
	}

	/// <summary>
	/// "New game": deletes the save file and resets wallet, statistics and rating in memory.
	/// The next gameplay scene starts in a random city.
	/// </summary>
	public void StartNewGame()
	{
		SaveManager.Instance.DeleteSave();
		Wallet.LoadSaveData(new WalletSaveData());
		Stats.LoadSaveData(new StatsSaveData());
		Reputation.LoadSaveData(new ReputationSaveData());
		Vehicle = Catalog.CreateStarter();
		Phone = Phones.Starter();
		_shopRotation = "";
		_soldOffers.Clear();
		_refreshDay = "";
		_refreshesUsed = 0;
		NextStart = StartMode.NewGame;
	}

	public Vector2 GetPlayerPosition() => Player?.GlobalPosition ?? Vector2.Zero;

	public DistrictData? GetCurrentDistrict()
	{
		if (CityMap == null || Player == null)
			return null;
		return CityMap.GetDistrictAtPosition(Player.GlobalPosition);
	}

	/// <summary>
	/// Global reward multiplier hook. The driver rating applies here; future systems
	/// (weather, rush hour, vehicle perks...) plug in here too.
	/// </summary>
	public float GetRewardMultiplier(JobTemplate template) =>
		Reputation.RewardMultiplier
		* (Weather?.Current.RewardMultiplier ?? 1f)
		* (Period?.RewardMultiplier ?? 1f);

	/// <summary>
	/// Eat and rest at a rest stop: pays the city's rest price and clears fatigue in a few seconds.
	/// </summary>
	public void Rest(string stopName)
	{
		if (Player == null || CityMap == null)
			return;
		if (Player.IsResting)
			return;
		if (Player.Fatigue < 5f)
		{
			EventBus.Instance.EmitSignal(EventBus.SignalName.NotificationRequested, "You are not tired yet");
			return;
		}
		var price = CityMap.Region.RestPrice;
		if (price > Wallet.Balance)
		{
			EventBus.Instance.EmitSignal(EventBus.SignalName.NotificationRequested,
				"Not enough money - stop the bike to recover slowly");
			return;
		}
		Wallet.Add(-price, $"Meal & rest: {stopName}");
		Stats.RecordRest(price);
		Player.StartRest();
		EventBus.Instance.EmitSignal(EventBus.SignalName.NotificationRequested,
			$"Eating and resting... ({FormatMoney(price)})");
	}

	/// <summary>
	/// Fills the player's tank at a gas station. With too little money it buys what the
	/// balance covers; with none it refuses.
	/// </summary>
	public void BuyFuel(string stationName)
	{
		if (Player == null || CityMap == null)
			return;
		var price = CityMap.Region.FuelPrice;
		var missing = Player.FuelCapacity - Player.Fuel;
		if (missing < 0.05f)
		{
			EventBus.Instance.EmitSignal(EventBus.SignalName.NotificationRequested, "The tank is already full");
			return;
		}

		var liters = missing;
		var cost = Mathf.CeilToInt(liters * price);
		if (cost > Wallet.Balance)
		{
			if (Wallet.Balance <= 0)
			{
				EventBus.Instance.EmitSignal(EventBus.SignalName.NotificationRequested, "Not enough money for fuel");
				return;
			}
			cost = Wallet.Balance;
			liters = cost / price;
		}

		var litersText = liters.ToString("0.0", CultureInfo.InvariantCulture);
		Wallet.Add(-cost, $"Fuel {litersText} L: {stationName}");
		Stats.RecordFuel(cost);
		Player.AddFuel(liters);
		var full = Player.FuelRatio >= 0.99f ? "Full tank! " : "";
		EventBus.Instance.EmitSignal(EventBus.SignalName.NotificationRequested,
			$"{full}Refueled {litersText} L for {FormatMoney(cost)}");
	}

	// --- Vehicle and shop ---------------------------------------------------------

	public string VehicleName(OwnedVehicle? vehicle = null)
	{
		vehicle ??= Vehicle;
		return Catalog.Find(vehicle.ModelId)?.VehicleName ?? vehicle.ModelId;
	}

	/// <summary>Gives the player the current vehicle's stats, color and thermal box.</summary>
	public void ApplyVehicleToPlayer(bool fillTank = false)
	{
		var model = Catalog.Find(Vehicle.ModelId);
		if (model == null)
		{
			GD.PushWarning($"GameManager: unknown vehicle model \"{Vehicle.ModelId}\", using the starter.");
			Vehicle = Catalog.CreateStarter();
			model = Catalog.Find(Vehicle.ModelId);
		}
		if (Player == null || model == null)
			return;
		Player.SetVehicle(Vehicle.BuildStats(model), model.BodyColor, Vehicle.HasPerk(VehiclePerkType.ThermalBox));
		if (fillTank)
			Player.SetFuel(Player.FuelCapacity);
		EventBus.Instance.EmitSignal(EventBus.SignalName.VehicleChanged);
	}

	// --- Phone and job refreshes -------------------------------------------------

	public string Today => TodayOverride.Length > 0
		? TodayOverride
		: DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

	public int RefreshesPerDay => FreeRefreshesPerDay + Phone.ExtraRefreshes;

	public int RefreshesLeft
	{
		get
		{
			RollRefreshDay();
			return Mathf.Max(0, RefreshesPerDay - _refreshesUsed);
		}
	}

	/// <summary>Spends one of today's job refreshes; false when none are left.</summary>
	public bool TryUseRefresh()
	{
		if (RefreshesLeft <= 0)
			return false;
		_refreshesUsed++;
		EventBus.Instance.EmitSignal(EventBus.SignalName.RefreshesChanged);
		return true;
	}

	/// <summary>A new local day resets the counter.</summary>
	private void RollRefreshDay()
	{
		if (_refreshDay == Today)
			return;
		_refreshDay = Today;
		_refreshesUsed = 0;
		EventBus.Instance.EmitSignal(EventBus.SignalName.RefreshesChanged);
	}

	/// <summary>Buys a better phone (no trade-in). Returns an error message, or null on success.</summary>
	public string? BuyPhone(PhoneData phone)
	{
		if (phone == Phone)
			return "You already have this phone.";
		if (phone.JobSlots <= Phone.JobSlots && phone.ExtraRefreshes <= Phone.ExtraRefreshes)
			return "Your phone is already as good.";
		if (phone.Price > Wallet.Balance)
			return "Not enough money.";
		Wallet.Add(-phone.Price, $"Bought phone: {phone.DisplayName}");
		Phone = phone;
		EventBus.Instance.EmitSignal(EventBus.SignalName.PhoneChanged);
		EventBus.Instance.EmitSignal(EventBus.SignalName.RefreshesChanged);
		SaveManager.Instance.SaveGame(silent: true);
		return null;
	}

	public bool IsOfferSold(VehicleOffer offer) => offer.SlotKey == _shopRotation && _soldOffers.Contains(offer.OfferId);

	/// <summary>
	/// Buys a shop vehicle, trading in the current one (<see cref="OwnedVehicle.TradeInValue"/>).
	/// Returns an error message, or null on success.
	/// </summary>
	public string? BuyVehicle(VehicleOffer offer)
	{
		if (IsOfferSold(offer))
			return "This vehicle has already been sold.";
		var net = offer.Vehicle.Price - Vehicle.TradeInValue;
		if (net > Wallet.Balance)
			return "Not enough money.";

		var oldName = VehicleName();
		Wallet.Add(-net, $"Bought {offer.Model.VehicleName} (traded in {oldName})");
		Vehicle = offer.Vehicle;
		if (_shopRotation != offer.SlotKey)
		{
			_shopRotation = offer.SlotKey;
			_soldOffers.Clear();
		}
		_soldOffers.Add(offer.OfferId);
		ApplyVehicleToPlayer(fillTank: true);
		SaveManager.Instance.SaveGame(silent: true);
		return null;
	}

	/// <summary>Late penalty share for a job after the thermal box (timed package jobs only; jobs that fail when late stay failing).</summary>
	public float LatePenaltyFor(JobData job) => job.IsPassenger || job.LatePenalty >= 1f
		? job.LatePenalty
		: job.LatePenalty * (1f - Vehicle.Perk(VehiclePerkType.ThermalBox));

	/// <summary>Tip for delivering a job fast, raised by the thermal box (package jobs only).</summary>
	public int TipFor(JobData job) => !job.HasTip ? 0 : job.IsPassenger
		? job.TipAmount
		: Mathf.Max(1, Mathf.RoundToInt(job.TipAmount * (1f + Vehicle.Perk(VehiclePerkType.ThermalBox))));

	/// <summary>
	/// Pays out a finished job (minus the late penalty, plus a tip for a fast delivery),
	/// records the customer's rating and statistics.
	/// </summary>
	public DeliveryResult CompleteDelivery(JobData job, double elapsedTime, double deliveryTime, bool late)
	{
		var penalty = late ? Mathf.RoundToInt(job.Reward * LatePenaltyFor(job)) : 0;
		var reward = Mathf.Max(1, job.Reward - penalty);
		var tip = !late && job.HasTip && deliveryTime <= job.TipTimeLimit ? TipFor(job) : 0;
		var lateTag = late ? " (late)" : "";
		Wallet.Add(reward, $"{job.Title}{lateTag}: {job.PickupName} -> {job.DeliveryName}");
		Wallet.Add(tip, $"Tip: {job.Title}");
		Stats.RecordDelivery(reward, job.Distance, tip);

		var ratingBefore = Reputation.Rating;
		var stars = Reputation.RecordDelivered(late);
		if (job.RatingBonus > 0)
			Reputation.RecordBonus(job.RatingBonus);

		var result = new DeliveryResult
		{
			Job = job,
			Reward = reward,
			WasLate = late,
			LatePenaltyAmount = job.Reward - reward,
			Tip = tip,
			CustomerStars = stars,
			RatingBonus = job.RatingBonus,
			RatingBefore = ratingBefore,
			RatingAfter = Reputation.Rating,
			DeliveryTime = deliveryTime,
			ElapsedTime = elapsedTime,
			NewBalance = Wallet.Balance,
			TotalDeliveries = Stats.TotalDeliveries,
		};

		SaveManager.Instance.SaveGame(silent: true);
		return result;
	}

	/// <summary>
	/// Records a job that ran out of time (<paramref name="cancelledByPlayer"/> = false)
	/// or that the player cancelled, and returns a short "rating 4.8 -> 4.4" note for the toast.
	/// </summary>
	public string RecordJobFailure(JobData job, bool cancelledByPlayer)
	{
		var before = Reputation.Rating;
		if (cancelledByPlayer)
			Reputation.RecordCancelled();
		else
			Reputation.RecordFailed();
		SaveManager.Instance.SaveGame(silent: true);
		return $"rating {FormatRating(before)} -> {FormatRating(Reputation.Rating)}";
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

	/// <summary>Takes a traffic fine from the wallet (the balance may go negative) and records it.</summary>
	public void ApplyTrafficFine(int amount, string reason)
	{
		Wallet.Add(-amount, $"Fine: {reason}");
		Stats.RecordFine(amount);
	}

	/// <summary>Price with cents, e.g. "$2.40".</summary>
	public static string FormatPrice(float amount) =>
		CurrencySymbol + amount.ToString("0.00", CultureInfo.InvariantCulture);

	/// <summary>Rating with one decimal, e.g. "4.7".</summary>
	public static string FormatRating(float rating) => rating.ToString("0.0", CultureInfo.InvariantCulture);

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
		Reputation = Reputation.ToSaveData(),
		Fuel = Player?.Fuel,
		Fatigue = Player?.Fatigue ?? 0f,
		Vehicle = Vehicle.ToSaveData(),
		Shop = new ShopSaveData { Rotation = _shopRotation, SoldOffers = _soldOffers.ToList() },
		PhoneId = Phone.PhoneId,
		RefreshDay = _refreshDay,
		RefreshesUsed = _refreshesUsed,
		RegionId = CityMap?.Region.RegionId ?? "",
		LayoutSeed = CityMap?.ActiveSeed ?? 0,
		Player = Player?.GetSaveData(),
	};

	public void ApplySaveData(GameSaveData data)
	{
		Wallet.LoadSaveData(data.Wallet);
		Stats.LoadSaveData(data.Stats);
		Reputation.LoadSaveData(data.Reputation);
		Vehicle = data.Vehicle != null ? OwnedVehicle.FromSaveData(data.Vehicle) : Catalog.CreateStarter();
		Phone = Phones.Find(data.PhoneId) ?? Phones.Starter();
		_refreshDay = data.RefreshDay;
		_refreshesUsed = data.RefreshesUsed;
		EventBus.Instance.EmitSignal(EventBus.SignalName.PhoneChanged);
		EventBus.Instance.EmitSignal(EventBus.SignalName.RefreshesChanged);
		_shopRotation = data.Shop.Rotation;
		_soldOffers.Clear();
		_soldOffers.UnionWith(data.Shop.SoldOffers);
		ApplyVehicleToPlayer(); // before the fuel, so the tank size is right
		if (Player != null && data.Fuel.HasValue)
			Player.SetFuel(data.Fuel.Value);
		Player?.SetFatigue(data.Fatigue);
		// A position is only meaningful on the same city layout; otherwise keep the spawn point.
		var sameMap = CityMap != null && data.RegionId == CityMap.Region.RegionId && data.LayoutSeed == CityMap.ActiveSeed;
		if (Player != null && data.Player != null && sameMap)
			Player.ApplySaveData(data.Player);
	}

	private void OnWalletBalanceChanged(int balance, int delta) =>
		EventBus.Instance.EmitSignal(EventBus.SignalName.BalanceChanged, balance, delta);

	private void OnStatsChanged() =>
		EventBus.Instance.EmitSignal(EventBus.SignalName.StatsChanged);

	private void OnReputationChanged() =>
		EventBus.Instance.EmitSignal(EventBus.SignalName.ReputationChanged);
}
