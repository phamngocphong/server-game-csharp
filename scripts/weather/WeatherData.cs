using System.Collections.Generic;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// One kind of weather (one .tres per kind in resources/weather/). The multipliers apply
/// while it lasts: the player's bike and AI traffic read the speed ones, fuel use and job
/// pay read theirs. A city's "weather.weights" decides how often each kind shows up.
/// </summary>
[GlobalClass]
public partial class WeatherData : Resource
{
    /// <summary>Id used by the "weather.weights" section of the city JSON files.</summary>
    [Export] public string WeatherId { get; set; } = "cloudy";
    [Export] public string DisplayName { get; set; } = "Cloudy";

    [ExportGroup("Driving")]
    /// <summary>Top speed of the player and of AI traffic.</summary>
    [Export(PropertyHint.Range, "0.2,1.5,0.05")] public float SpeedMultiplier { get; set; } = 1f;
    [Export(PropertyHint.Range, "0.2,1.5,0.05")] public float AccelerationMultiplier { get; set; } = 1f;
    /// <summary>Lower grip = the bike slides more in turns.</summary>
    [Export(PropertyHint.Range, "0.2,1.5,0.05")] public float GripMultiplier { get; set; } = 1f;

    [ExportGroup("Economy")]
    /// <summary>Fuel used per km and while idling.</summary>
    [Export(PropertyHint.Range, "0.5,3,0.05")] public float FuelMultiplier { get; set; } = 1f;
    /// <summary>Job pay (customers pay more when nobody wants to ride in the rain).</summary>
    [Export(PropertyHint.Range, "0.5,2,0.05")] public float RewardMultiplier { get; set; } = 1f;

    [ExportGroup("Look")]
    /// <summary>Color the world is tinted with (CanvasModulate).</summary>
    [Export] public Color Tint { get; set; } = Colors.White;
    /// <summary>0 = dry, 1 = heavy rain with lightning.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float RainIntensity { get; set; }

    [ExportGroup("Timing")]
    /// <summary>How long one spell of this weather lasts, in seconds.</summary>
    [Export] public float MinDuration { get; set; } = 60f;
    [Export] public float MaxDuration { get; set; } = 150f;
    /// <summary>Default relative chance; a city's "weather.weights" can override it.</summary>
    [Export] public float DefaultWeight { get; set; } = 1f;

    /// <summary>Short list of the effects, e.g. "speed -25%, grip -30%, pay +15%" ("normal" if none).</summary>
    public string EffectSummary()
    {
        var parts = new List<string>();
        void Add(string name, float multiplier)
        {
            var percent = Mathf.RoundToInt((multiplier - 1f) * 100f);
            if (percent != 0)
                parts.Add($"{name} {(percent > 0 ? "+" : "")}{percent}%");
        }
        Add("speed", SpeedMultiplier);
        Add("grip", GripMultiplier);
        Add("fuel use", FuelMultiplier);
        Add("pay", RewardMultiplier);
        return parts.Count > 0 ? string.Join(", ", parts) : "normal driving";
    }
}
