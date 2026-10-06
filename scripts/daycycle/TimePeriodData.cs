using System.Collections.Generic;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// A part of the day (one .tres per period in resources/day_periods/), active from
/// <see cref="StartHour"/> until the next period starts. Its multipliers change jobs,
/// pay, fuel, fatigue and traffic while it lasts.
/// </summary>
[GlobalClass]
public partial class TimePeriodData : Resource
{
    [Export] public string PeriodId { get; set; } = "afternoon";
    [Export] public string DisplayName { get; set; } = "Afternoon";
    /// <summary>Local hour (0-24, fractions allowed) this period starts at.</summary>
    [Export(PropertyHint.Range, "0,24,0.25")] public float StartHour { get; set; } = 14f;
    /// <summary>One line shown on the Job Board and in the toast when the period starts.</summary>
    [Export] public string Summary { get; set; } = "";

    [ExportGroup("Jobs")]
    /// <summary>Jobs on the board (JobManager.JobsPerBoard x this, at least 1).</summary>
    [Export(PropertyHint.Range, "0.2,2,0.05")] public float JobCountMultiplier { get; set; } = 1f;
    [Export(PropertyHint.Range, "0.5,3,0.05")] public float RewardMultiplier { get; set; } = 1f;
    /// <summary>Extra chance per JobTemplate.TemplateId (e.g. "food": 3 for breakfast); missing = 1.</summary>
    [Export] public Godot.Collections.Dictionary<string, float> TemplateWeights { get; set; } = new();

    [ExportGroup("Driving")]
    [Export(PropertyHint.Range, "0.5,3,0.05")] public float FuelMultiplier { get; set; } = 1f;
    [Export(PropertyHint.Range, "0.5,3,0.05")] public float FatigueMultiplier { get; set; } = 1f;
    /// <summary>AI traffic amount compared with the city's normal count.</summary>
    [Export(PropertyHint.Range, "0,3,0.05")] public float TrafficMultiplier { get; set; } = 1f;

    public float TemplateWeight(string templateId) =>
        TemplateWeights.TryGetValue(templateId, out var weight) ? weight : 1f;

    /// <summary>"pay +30%, fatigue +40%, fewer jobs" or "normal".</summary>
    public string EffectSummary()
    {
        var parts = new List<string>();
        void Add(string name, float multiplier)
        {
            var percent = Mathf.RoundToInt((multiplier - 1f) * 100f);
            if (percent != 0)
                parts.Add($"{name} {(percent > 0 ? "+" : "")}{percent}%");
        }
        Add("pay", RewardMultiplier);
        Add("fuel use", FuelMultiplier);
        Add("fatigue", FatigueMultiplier);
        Add("traffic", TrafficMultiplier);
        if (JobCountMultiplier < 0.95f)
            parts.Add("fewer jobs");
        else if (JobCountMultiplier > 1.05f)
            parts.Add("more jobs");
        return parts.Count > 0 ? string.Join(", ", parts) : "normal";
    }
}
