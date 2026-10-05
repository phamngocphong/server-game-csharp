using System;

namespace ShipperSimulator;

/// <summary>Lifetime statistics of the player.</summary>
public sealed class PlayerStats
{
    public event Action? Changed;

    public int TotalDeliveries { get; private set; }
    public int TotalEarned { get; private set; }
    public int BestReward { get; private set; }
    /// <summary>Sum of delivery route distances, in pixels.</summary>
    public float TotalDistance { get; private set; }
    /// <summary>Seconds played.</summary>
    public double PlayTime { get; set; }

    public void RecordDelivery(int reward, float distance)
    {
        TotalDeliveries++;
        TotalEarned += reward;
        BestReward = Math.Max(BestReward, reward);
        TotalDistance += distance;
        Changed?.Invoke();
    }

    public StatsSaveData ToSaveData() => new()
    {
        TotalDeliveries = TotalDeliveries,
        TotalEarned = TotalEarned,
        BestReward = BestReward,
        TotalDistance = TotalDistance,
        PlayTime = PlayTime,
    };

    public void LoadSaveData(StatsSaveData data)
    {
        TotalDeliveries = data.TotalDeliveries;
        TotalEarned = data.TotalEarned;
        BestReward = data.BestReward;
        TotalDistance = data.TotalDistance;
        PlayTime = data.PlayTime;
        Changed?.Invoke();
    }
}
