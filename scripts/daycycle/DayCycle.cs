using System;
using System.Linq;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Real-time day/night cycle of the gameplay scene: follows the computer's local clock
/// (or <see cref="ClockOverrideHour"/>), picks the active <see cref="TimePeriodData"/> and
/// computes the daylight color that <see cref="WeatherSystem"/> multiplies into the world
/// tint, plus <see cref="Darkness"/> for headlights and glows (<see cref="NightLight"/>).
/// Other systems read <see cref="GameManager.DayCycle"/>.
/// </summary>
public partial class DayCycle : Node
{
    /// <summary>Daylight color keyframes by hour; colors in between are interpolated.</summary>
    private static readonly (float Hour, Color Color)[] LightKeys =
    {
        (0f, new Color(0.3f, 0.34f, 0.52f)),
        (4.5f, new Color(0.3f, 0.34f, 0.52f)),
        (6f, new Color(0.9f, 0.74f, 0.66f)), // dawn
        (8f, new Color(1f, 0.97f, 0.93f)),
        (12f, new Color(1f, 1f, 1f)),
        (16.5f, new Color(1f, 0.96f, 0.9f)),
        (18.25f, new Color(0.95f, 0.72f, 0.58f)), // sunset
        (19.5f, new Color(0.55f, 0.5f, 0.66f)),
        (21f, new Color(0.32f, 0.35f, 0.54f)),
        (24f, new Color(0.3f, 0.34f, 0.52f)),
    };

    /// <summary>Luminance of the darkest keyframe; Darkness is 1 there and 0 at full daylight.</summary>
    private const float NightLuminance = 0.35f;
    private const double UpdateInterval = 0.5;

    [Export] public Godot.Collections.Array<TimePeriodData> Periods { get; set; } = new();
    /// <summary>Fixed hour (0-24) instead of the real clock, for testing in the editor; -1 = real clock.</summary>
    [Export(PropertyHint.Range, "-1,24,0.25")] public float ClockOverrideHour { get; set; } = -1f;

    /// <summary>Assigned in _Ready.</summary>
    public TimePeriodData Current { get; private set; } = null!;
    /// <summary>Local time of day in hours (0-24).</summary>
    public float Hour { get; private set; }
    public Color LightColor { get; private set; } = Colors.White;
    /// <summary>0 = full daylight, 1 = night.</summary>
    public float Darkness { get; private set; }

    private double _timer;

    public override void _Ready()
    {
        GameManager.Instance.DayCycle = this;
        UpdateClock(announce: false);
    }

    public override void _ExitTree()
    {
        if (GameManager.Instance.DayCycle == this)
            GameManager.Instance.DayCycle = null;
    }

    public override void _Process(double delta)
    {
        _timer += delta;
        if (_timer < UpdateInterval)
            return;
        _timer = 0.0;
        UpdateClock(announce: true);
    }

    /// <summary>"07:42".</summary>
    public string ClockText()
    {
        var minutes = (int)(Hour * 60f) % (24 * 60);
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }

    private void UpdateClock(bool announce)
    {
        if (ClockOverrideHour >= 0f)
        {
            Hour = ClockOverrideHour % 24f;
        }
        else
        {
            var now = DateTime.Now;
            Hour = now.Hour + now.Minute / 60f + now.Second / 3600f;
        }

        LightColor = SampleLight(Hour);
        var luminance = LightColor.R * 0.3f + LightColor.G * 0.59f + LightColor.B * 0.11f;
        Darkness = Mathf.Clamp((1f - luminance) / (1f - NightLuminance), 0f, 1f);

        var period = FindPeriod(Hour);
        if (period == null || period == Current)
            return;
        Current = period;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PeriodChanged, period.DisplayName);
        if (announce)
            EventBus.Instance.EmitSignal(EventBus.SignalName.NotificationRequested,
                $"{period.DisplayName}: {period.Summary}");
    }

    private TimePeriodData? FindPeriod(float hour)
    {
        if (Periods.Count == 0)
            return Current ?? new TimePeriodData();
        var sorted = Periods.OrderBy(p => p.StartHour).ToList();
        // The last period that already started today; before the first start, yesterday's last one (e.g. night).
        return sorted.LastOrDefault(p => p.StartHour <= hour) ?? sorted[^1];
    }

    private static Color SampleLight(float hour)
    {
        for (var i = 1; i < LightKeys.Length; i++)
        {
            if (hour > LightKeys[i].Hour)
                continue;
            var (h0, c0) = LightKeys[i - 1];
            var (h1, c1) = LightKeys[i];
            return c0.Lerp(c1, (hour - h0) / Mathf.Max(h1 - h0, 0.001f));
        }
        return LightKeys[^1].Color;
    }
}
