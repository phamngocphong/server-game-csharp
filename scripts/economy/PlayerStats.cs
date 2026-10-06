using System;

namespace ShipperSimulator;

/// <summary>Lifetime statistics of the player.</summary>
public sealed class PlayerStats
{
    public event Action? Changed;

    public int TotalDeliveries { get; private set; }
    public int TotalEarned { get; private set; }
    public int BestReward { get; private set; }
    /// <summary>Tips received for fast deliveries (already included in TotalEarned).</summary>
    public int TotalTips { get; private set; }
    /// <summary>Red lights run and the money paid for them.</summary>
    public int TotalViolations { get; private set; }
    public int TotalFines { get; private set; }
    /// <summary>Sum of delivery route distances, in pixels.</summary>
    public float TotalDistance { get; private set; }
    /// <summary>Seconds played.</summary>
    public double PlayTime { get; set; }

    public void RecordDelivery(int reward, float distance, int tip = 0)
    {
        TotalDeliveries++;
        TotalEarned += reward + tip;
        TotalTips += tip;
        BestReward = Math.Max(BestReward, reward + tip);
        TotalDistance += distance;
        Changed?.Invoke();
    }

    public void RecordFine(int amount)
    {
        TotalViolations++;
        TotalFines += amount;
        Changed?.Invoke();
    }

    public StatsSaveData ToSaveData() => new()
    {
        TotalDeliveries = TotalDeliveries,
        TotalEarned = TotalEarned,
        BestReward = BestReward,
        TotalTips = TotalTips,
        TotalViolations = TotalViolations,
        TotalFines = TotalFines,
        TotalDistance = TotalDistance,
        PlayTime = PlayTime,
    };

    public void LoadSaveData(StatsSaveData data)
    {
        TotalDeliveries = data.TotalDeliveries;
        TotalEarned = data.TotalEarned;
        BestReward = data.BestReward;
        TotalTips = data.TotalTips;
        TotalViolations = data.TotalViolations;
        TotalFines = data.TotalFines;
        TotalDistance = data.TotalDistance;
        PlayTime = data.PlayTime;
        Changed?.Invoke();
    }
}
