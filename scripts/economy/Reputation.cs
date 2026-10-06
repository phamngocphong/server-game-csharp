using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Driver rating from 1 to 5 stars, like ride-hailing apps: the average of the customer
/// ratings of the last <see cref="Window"/> jobs. New drivers start with
/// <see cref="StartingRatings"/> five-star ratings so a single bad job does not sink them.
/// A low rating makes high-paying jobs rarer (<see cref="JobTemplate.MinRating"/>)
/// and lowers pay (<see cref="RewardMultiplier"/>).
/// </summary>
public sealed class Reputation
{
    public const int Window = 20;
    public const int StartingRatings = 5;

    // Stars a customer gives for each outcome.
    public const int StarsOnTime = 5;
    public const int StarsLate = 3;
    public const int StarsCancelled = 2;
    public const int StarsFailed = 1;

    /// <summary>Pay multiplier at 1 star and at 5 stars (linear in between).</summary>
    public const float MinRewardMultiplier = 0.8f;
    public const float MaxRewardMultiplier = 1.1f;

    public event Action? Changed;

    public float Rating { get; private set; } = 5f;
    public int TotalOnTime { get; private set; }
    public int TotalLate { get; private set; }
    /// <summary>Timed jobs that ran out of time.</summary>
    public int TotalFailed { get; private set; }
    /// <summary>Jobs the player cancelled after accepting them.</summary>
    public int TotalCancelled { get; private set; }
    public IReadOnlyList<int> RecentRatings => _recent;

    public float RewardMultiplier =>
        Mathf.Lerp(MinRewardMultiplier, MaxRewardMultiplier, Mathf.Clamp((Rating - 1f) / 4f, 0f, 1f));

    private readonly List<int> _recent = new();

    public Reputation() => Seed();

    /// <summary>Records a finished job and returns the stars the customer gave.</summary>
    public int RecordDelivered(bool late)
    {
        if (late)
            TotalLate++;
        else
            TotalOnTime++;
        return AddRating(late ? StarsLate : StarsOnTime);
    }

    /// <summary>Extra 5-star reviews (special jobs).</summary>
    public void RecordBonus(int count)
    {
        for (var i = 0; i < count; i++)
            AddRating(5);
    }

    public int RecordFailed()
    {
        TotalFailed++;
        return AddRating(StarsFailed);
    }

    public int RecordCancelled()
    {
        TotalCancelled++;
        return AddRating(StarsCancelled);
    }

    public ReputationSaveData ToSaveData() => new()
    {
        RecentRatings = _recent.ToList(),
        TotalOnTime = TotalOnTime,
        TotalLate = TotalLate,
        TotalFailed = TotalFailed,
        TotalCancelled = TotalCancelled,
    };

    public void LoadSaveData(ReputationSaveData data)
    {
        _recent.Clear();
        _recent.AddRange(data.RecentRatings.Select(r => Math.Clamp(r, 1, 5)).TakeLast(Window));
        if (_recent.Count == 0)
            Seed();
        TotalOnTime = data.TotalOnTime;
        TotalLate = data.TotalLate;
        TotalFailed = data.TotalFailed;
        TotalCancelled = data.TotalCancelled;
        Recalculate();
    }

    private void Seed()
    {
        _recent.Clear();
        for (var i = 0; i < StartingRatings; i++)
            _recent.Add(5);
        Recalculate();
    }

    private int AddRating(int stars)
    {
        _recent.Add(stars);
        if (_recent.Count > Window)
            _recent.RemoveAt(0);
        Recalculate();
        return stars;
    }

    private void Recalculate()
    {
        Rating = (float)_recent.Average();
        Changed?.Invoke();
    }
}
