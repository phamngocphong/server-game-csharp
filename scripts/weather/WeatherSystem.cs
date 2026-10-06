using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Runs the weather of the gameplay scene: picks a <see cref="WeatherData"/> by the city's
/// weights, keeps it for a random duration, then changes to a different one. Tints the world
/// (CanvasModulate) and draws rain (<see cref="RainOverlay"/>), fading between weathers.
/// Other systems read <see cref="GameManager.Weather"/>.<see cref="Current"/>.
/// </summary>
public partial class WeatherSystem : Node
{
    private const float FadeSpeed = 0.4f;

    [Export] public CityMap Map { get; set; } = null!;
    [Export] public Godot.Collections.Array<WeatherData> WeatherTypes { get; set; } = new();
    /// <summary>CanvasLayer index of the rain; keep it below the UI layer.</summary>
    [Export] public int RainLayer { get; set; } = 5;

    /// <summary>
    /// Assigned in _Ready (never in a field initializer: creating a Resource while Godot
    /// is still constructing this node breaks the C# object handles).
    /// </summary>
    public WeatherData Current { get; private set; } = null!;
    /// <summary>Seconds until the weather changes.</summary>
    public float TimeLeft { get; private set; }

    private readonly RandomNumberGenerator _rng = new();
    private CanvasModulate _tint = null!;
    private RainOverlay _rain = null!;

    public override void _Ready()
    {
        _rng.Randomize();
        _tint = new CanvasModulate { Name = "WeatherTint" };
        AddChild(_tint);
        var layer = new CanvasLayer { Name = "RainLayer", Layer = RainLayer };
        AddChild(layer);
        _rain = new RainOverlay { Name = "Rain" };
        layer.AddChild(_rain);

        if (WeatherTypes.Count == 0)
        {
            Current = new WeatherData(); // neutral weather
            GameManager.Instance.Weather = this;
            return;
        }
        GameManager.Instance.Weather = this;
        SetWeather(Pick(null), announce: false);
        _tint.Color = Current.Tint;
        _rain.Intensity = Current.RainIntensity;
    }

    public override void _ExitTree()
    {
        if (GameManager.Instance.Weather == this)
            GameManager.Instance.Weather = null;
    }

    public override void _Process(double delta)
    {
        if (WeatherTypes.Count == 0)
            return;
        var dt = (float)delta;
        TimeLeft -= dt;
        if (TimeLeft <= 0f)
            SetWeather(Pick(Current), announce: true);

        var fade = Mathf.Clamp(FadeSpeed * dt, 0f, 1f);
        _tint.Color = _tint.Color.Lerp(Current.Tint, fade);
        _rain.Intensity = Mathf.MoveToward(_rain.Intensity, Current.RainIntensity, fade);
    }

    /// <summary>Forces a weather by id (debugging, events, tests).</summary>
    public bool ForceWeather(string weatherId)
    {
        var data = WeatherTypes.FirstOrDefault(w => w.WeatherId == weatherId);
        if (data == null)
            return false;
        SetWeather(data, announce: true);
        return true;
    }

    private void SetWeather(WeatherData data, bool announce)
    {
        Current = data;
        TimeLeft = _rng.RandfRange(data.MinDuration, Mathf.Max(data.MinDuration, data.MaxDuration));
        EventBus.Instance.EmitSignal(EventBus.SignalName.WeatherChanged, data.DisplayName);
        if (announce)
            EventBus.Instance.EmitSignal(EventBus.SignalName.NotificationRequested,
                $"Weather: {data.DisplayName} - {data.EffectSummary()}");
    }

    /// <summary>Weighted pick by the city's weights, avoiding <paramref name="previous"/> when possible.</summary>
    private WeatherData Pick(WeatherData? previous)
    {
        var overrides = Map?.Region?.WeatherWeights;
        var candidates = new List<(WeatherData Data, float Weight)>();
        foreach (var w in WeatherTypes)
        {
            var weight = overrides != null && overrides.TryGetValue(w.WeatherId, out var o) ? o : w.DefaultWeight;
            if (weight > 0f && w != previous)
                candidates.Add((w, weight));
        }
        if (candidates.Count == 0)
            return previous ?? WeatherTypes[0];

        var roll = _rng.Randf() * candidates.Sum(c => c.Weight);
        foreach (var (data, weight) in candidates)
        {
            roll -= weight;
            if (roll <= 0f)
                return data;
        }
        return candidates[^1].Data;
    }
}
