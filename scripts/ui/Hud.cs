using Godot;

namespace ShipperSimulator;

/// <summary>
/// Always-on HUD: wallet, driver rating, current city, district and weather, fuel gauge, active job (with its countdown
/// for timed jobs), interaction prompt, speedometer and toast notifications.
/// </summary>
public partial class Hud : Control
{
    private Label _balanceLabel = null!;
    private Label _deliveriesLabel = null!;
    private StarRating _ratingStars = null!;
    private Label _ratingLabel = null!;
    private Label _cityLabel = null!;
    private Label _districtLabel = null!;
    private Control _activeJobPanel = null!;
    private Label _jobStatusLabel = null!;
    private Label _jobTimerLabel = null!;
    private Label _jobTargetLabel = null!;
    private Label _jobInfoLabel = null!;
    private Label _jobDistanceLabel = null!;
    private Button _cancelButton = null!;
    private Control _promptPanel = null!;
    private Label _promptLabel = null!;
    private Label _speedLabel = null!;
    private ProgressBar _fuelBar = null!;
    private Label _fuelLabel = null!;
    private Label _stationLabel = null!;
    private Label _weatherLabel = null!;
    private Label _clockLabel = null!;
    private ProgressBar _fatigueBar = null!;
    private Label _fatigueLabel = null!;
    private Label _restLabel = null!;
    private Control _toastPanel = null!;
    private Label _toastLabel = null!;
    private ColorRect _flashRect = null!;

    private Vector2 _target;
    private bool _hasTarget;
    private Tween? _toastTween;

    public override void _Ready()
    {
        _balanceLabel = GetNode<Label>("%BalanceLabel");
        _deliveriesLabel = GetNode<Label>("%DeliveriesLabel");
        _ratingStars = GetNode<StarRating>("%RatingStars");
        _ratingLabel = GetNode<Label>("%RatingLabel");
        _cityLabel = GetNode<Label>("%CityLabel");
        _districtLabel = GetNode<Label>("%DistrictLabel");
        _activeJobPanel = GetNode<Control>("%ActiveJobPanel");
        _jobStatusLabel = GetNode<Label>("%JobStatusLabel");
        _jobTimerLabel = GetNode<Label>("%JobTimerLabel");
        _jobTargetLabel = GetNode<Label>("%JobTargetLabel");
        _jobInfoLabel = GetNode<Label>("%JobInfoLabel");
        _jobDistanceLabel = GetNode<Label>("%JobDistanceLabel");
        _cancelButton = GetNode<Button>("%CancelButton");
        _promptPanel = GetNode<Control>("%PromptPanel");
        _promptLabel = GetNode<Label>("%PromptLabel");
        _speedLabel = GetNode<Label>("%SpeedLabel");
        _fuelBar = GetNode<ProgressBar>("%FuelBar");
        _fuelLabel = GetNode<Label>("%FuelLabel");
        _stationLabel = GetNode<Label>("%StationLabel");
        _weatherLabel = GetNode<Label>("%WeatherLabel");
        _clockLabel = GetNode<Label>("%ClockLabel");
        _fatigueBar = GetNode<ProgressBar>("%FatigueBar");
        _fatigueLabel = GetNode<Label>("%FatigueLabel");
        _restLabel = GetNode<Label>("%RestLabel");
        _toastPanel = GetNode<Control>("%ToastPanel");
        _toastLabel = GetNode<Label>("%ToastLabel");
        _flashRect = GetNode<ColorRect>("%FlashRect");

        var bus = EventBus.Instance;
        bus.BalanceChanged += OnBalanceChanged;
        bus.StatsChanged += RefreshStats;
        bus.ReputationChanged += RefreshRating;
        bus.JobStateChanged += OnJobStateChanged;
        bus.JobTimerUpdated += OnJobTimerUpdated;
        bus.NavigationTargetChanged += OnTargetChanged;
        bus.NavigationTargetCleared += OnTargetCleared;
        bus.InteractionPromptChanged += OnPromptChanged;
        bus.NotificationRequested += ShowToast;
        bus.PlayerCrashed += OnPlayerCrashed;
        bus.TrafficFined += OnTrafficFined;
        bus.WeatherChanged += OnWeatherChanged;
        _cancelButton.Pressed += OnCancelPressed;
        GetNode<Button>("%PauseButton").Pressed += OnPausePressed;

        _activeJobPanel.Hide();
        _promptPanel.Hide();
        _toastPanel.Hide();
        _flashRect.Hide();
        OnBalanceChanged(GameManager.Instance.Wallet.Balance, 0);
        RefreshStats();
        RefreshRating();
        OnWeatherChanged(""); // the Weather node is ready before the HUD and already picked one
    }

    public override void _ExitTree()
    {
        // C# events of [Signal]s declared in C# are plain delegates: Godot does not
        // disconnect them when this node is freed, so unsubscribe explicitly.
        var bus = EventBus.Instance;
        bus.BalanceChanged -= OnBalanceChanged;
        bus.StatsChanged -= RefreshStats;
        bus.ReputationChanged -= RefreshRating;
        bus.JobStateChanged -= OnJobStateChanged;
        bus.JobTimerUpdated -= OnJobTimerUpdated;
        bus.NavigationTargetChanged -= OnTargetChanged;
        bus.NavigationTargetCleared -= OnTargetCleared;
        bus.InteractionPromptChanged -= OnPromptChanged;
        bus.NotificationRequested -= ShowToast;
        bus.PlayerCrashed -= OnPlayerCrashed;
        bus.TrafficFined -= OnTrafficFined;
        bus.WeatherChanged -= OnWeatherChanged;
    }

    public override void _Process(double delta)
    {
        var player = GameManager.Instance.Player;
        if (player == null)
            return;
        if (player.IsResting)
        {
            _speedLabel.Text = $"RESTING  {player.RestTimeLeft.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} s";
            _speedLabel.Modulate = new Color(0.4f, 0.95f, 0.85f);
        }
        else if (player.IsStunned)
        {
            _speedLabel.Text = $"CRASHED  {player.StunTimeLeft.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} s";
            _speedLabel.Modulate = new Color(1f, 0.35f, 0.3f);
        }
        else
        {
            _speedLabel.Text = $"{Mathf.RoundToInt(player.GetSpeedKmh())} km/h";
            _speedLabel.Modulate = Colors.White;
        }
        _cityLabel.Text = GameManager.Instance.CityMap?.Region.DisplayName ?? "";
        UpdateFuel(player);
        UpdateFatigue(player);
        var day = GameManager.Instance.DayCycle;
        _clockLabel.Text = day == null ? "" : $"{day.ClockText()}  {day.Current.DisplayName}";
        _districtLabel.Text = GameManager.Instance.GetCurrentDistrict()?.DisplayName ?? "";
        if (_hasTarget && _activeJobPanel.Visible)
        {
            var distance = JobGenerator.RouteDistance(player.GlobalPosition, _target);
            _jobDistanceLabel.Text = $"Distance: {GameManager.FormatDistance(distance)}";
        }
    }

    public void ShowToast(string text)
    {
        _toastLabel.Text = text;
        _toastPanel.Show();
        _toastPanel.Modulate = Colors.White;
        _toastTween?.Kill();
        _toastTween = CreateTween();
        _toastTween.TweenInterval(1.8);
        _toastTween.TweenProperty(_toastPanel, "modulate:a", 0.0, 0.4);
        _toastTween.TweenCallback(Callable.From(_toastPanel.Hide));
    }

    private void OnBalanceChanged(int balance, int delta)
    {
        _balanceLabel.Text = GameManager.FormatMoney(balance);
        if (delta != 0)
        {
            _balanceLabel.Modulate = delta > 0 ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.35f, 0.3f);
            CreateTween().TweenProperty(_balanceLabel, "modulate", Colors.White, 0.8);
        }
    }

    private void RefreshStats() =>
        _deliveriesLabel.Text = $"Deliveries: {GameManager.Instance.Stats.TotalDeliveries}";

    private void UpdateFatigue(Player player)
    {
        var fatigue = player.Fatigue;
        _fatigueBar.Value = fatigue;
        _fatigueBar.Modulate = player.IsExhausted || fatigue >= 70f ? new Color(1f, 0.35f, 0.3f)
            : fatigue >= 50f ? new Color(1f, 0.75f, 0.3f)
            : new Color(0.55f, 0.85f, 1f);
        _fatigueLabel.Text = player.IsExhausted ? $"{Mathf.RoundToInt(fatigue)}% EXHAUSTED" : $"{Mathf.RoundToInt(fatigue)}%";

        var stop = fatigue >= 60f || player.IsExhausted
            ? GameManager.Instance.CityMap?.GetNearestRestStop(player.GlobalPosition)
            : null;
        _restLabel.Visible = stop != null;
        if (stop != null)
        {
            var home = GameManager.Instance.CityMap?.Home;
            _restLabel.Text = $"Rest stop: {GameManager.FormatDistance(JobGenerator.RouteDistance(player.GlobalPosition, stop.GlobalPosition))}" +
                (home != null ? $"   -   Home (free): {GameManager.FormatDistance(JobGenerator.RouteDistance(player.GlobalPosition, home.GlobalPosition))}" : "");
        }
    }

    private void UpdateFuel(Player player)
    {
        var ratio = player.FuelRatio;
        _fuelBar.Value = ratio;
        _fuelBar.Modulate = ratio > 0.25f ? new Color(0.55f, 1f, 0.6f)
            : ratio > 0.1f ? new Color(1f, 0.75f, 0.3f)
            : new Color(1f, 0.35f, 0.3f);
        _fuelLabel.Text = player.IsOutOfFuel
            ? "EMPTY - push!"
            : $"{player.Fuel.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} L";

        var station = ratio <= 0.3f ? GameManager.Instance.CityMap?.GetNearestGasStation(player.GlobalPosition) : null;
        _stationLabel.Visible = station != null;
        if (station != null)
            _stationLabel.Text = $"Nearest gas station: {GameManager.FormatDistance(JobGenerator.RouteDistance(player.GlobalPosition, station.GlobalPosition))}";
    }

    private void OnWeatherChanged(string weatherName)
    {
        var weather = GameManager.Instance.Weather?.Current;
        _weatherLabel.Text = weather == null ? "" : $"{weather.DisplayName}: {weather.EffectSummary()}";
    }

    private void OnPlayerCrashed(string vehicleName, int collisionScore, float stunSeconds) =>
        ShowToast($"Crashed into a {vehicleName}! (impact {collisionScore}) - stunned {stunSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} s");

    private void OnTrafficFined(int amount, string reason, int multiplier)
    {
        var repeat = multiplier > 1 ? $" (repeat offence x{multiplier})" : "";
        ShowToast($"{reason}! Fine -{GameManager.FormatMoney(amount)}{repeat}");
        // Speed-camera flash.
        _flashRect.Show();
        _flashRect.Color = new Color(1f, 0.15f, 0.1f, 0.35f);
        var tween = CreateTween();
        tween.TweenProperty(_flashRect, "color:a", 0.0, 0.5);
        tween.TweenCallback(Callable.From(_flashRect.Hide));
    }

    private void RefreshRating()
    {
        var rating = GameManager.Instance.Reputation.Rating;
        _ratingStars.Value = rating;
        _ratingLabel.Text = GameManager.FormatRating(rating);
    }

    private void OnJobStateChanged(int state, JobData job)
    {
        switch ((JobManager.State)state)
        {
            case JobManager.State.ToPickup:
                _jobStatusLabel.Text = job.IsPassenger ? "PICK UP PASSENGER" : "GO TO PICKUP";
                _jobTargetLabel.Text = $"{job.PickupName} ({job.PickupDistrict})";
                break;
            case JobManager.State.ToDelivery:
                _jobStatusLabel.Text = job.IsPassenger ? "DROP OFF PASSENGER" : "DELIVER TO";
                _jobTargetLabel.Text = $"{job.DeliveryName} ({job.DeliveryDistrict})";
                break;
            default:
                _activeJobPanel.Hide();
                return;
        }
        _activeJobPanel.Show();
        var rewardWord = job.IsPassenger ? "Fare" : "Reward";
        _jobInfoLabel.Text = $"{job.Title}  -  {rewardWord} {GameManager.FormatMoney(job.Reward)}";
        _jobStatusLabel.AddThemeColorOverride("font_color", job.PackageColor);
        _jobTimerLabel.Visible = job.IsTimed;
    }

    private void OnJobTimerUpdated(float remaining, float limit)
    {
        if (remaining > 0f)
        {
            // Round up so the label shows 0:01 until the deadline actually passes.
            _jobTimerLabel.Text = $"Time left {GameManager.FormatTime(Mathf.Ceil(remaining))}";
            var share = limit > 0f ? remaining / limit : 1f;
            _jobTimerLabel.Modulate = share > 0.5f ? new Color(0.6f, 1f, 0.65f)
                : share > 0.25f ? new Color(1f, 0.75f, 0.3f)
                : new Color(1f, 0.35f, 0.3f);
        }
        else
        {
            _jobTimerLabel.Text = $"LATE +{GameManager.FormatTime(Mathf.Ceil(-remaining))}";
            // Blink while late.
            _jobTimerLabel.Modulate = new Color(1f, 0.3f, 0.3f, 0.6f + 0.4f * Mathf.Sin((float)Time.GetTicksMsec() / 120f));
        }
    }

    private void OnTargetChanged(Vector2 target, string label, Color color)
    {
        _target = target;
        _hasTarget = true;
    }

    private void OnTargetCleared() => _hasTarget = false;

    private void OnPromptChanged(string text)
    {
        _promptLabel.Text = text;
        _promptPanel.Visible = !string.IsNullOrEmpty(text);
    }

    private void OnPausePressed() => EventBus.Instance.EmitSignal(EventBus.SignalName.PauseMenuRequested);

    private void OnCancelPressed() => EventBus.Instance.EmitSignal(EventBus.SignalName.JobCancelRequested);
}
