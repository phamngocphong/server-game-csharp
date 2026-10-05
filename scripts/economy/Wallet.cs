using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ShipperSimulator;

/// <summary>One wallet transaction (positive = income, negative = expense).</summary>
public sealed class WalletEntry
{
    public int Amount { get; set; }
    public string Description { get; set; } = "";
    /// <summary>Unix time in seconds.</summary>
    public long Timestamp { get; set; }
}

/// <summary>
/// Player money plus a capped history of transactions.
/// Spending (fuel, upgrades...) goes through <see cref="Spend"/> so history stays complete.
/// </summary>
public sealed class Wallet
{
    public const int MaxHistory = 50;

    /// <summary>(newBalance, delta)</summary>
    public event Action<int, int>? BalanceChanged;

    private readonly List<WalletEntry> _history = new();

    public int Balance { get; private set; }

    /// <summary>Newest first.</summary>
    public IReadOnlyList<WalletEntry> History => _history;

    public void Add(int amount, string description)
    {
        if (amount == 0)
            return;
        Balance += amount;
        _history.Insert(0, new WalletEntry
        {
            Amount = amount,
            Description = description,
            Timestamp = (long)Time.GetUnixTimeFromSystem(),
        });
        if (_history.Count > MaxHistory)
            _history.RemoveRange(MaxHistory, _history.Count - MaxHistory);
        BalanceChanged?.Invoke(Balance, amount);
    }

    public bool CanAfford(int amount) => Balance >= amount;

    public bool Spend(int amount, string description)
    {
        if (amount <= 0 || !CanAfford(amount))
            return false;
        Add(-amount, description);
        return true;
    }

    public WalletSaveData ToSaveData() => new()
    {
        Balance = Balance,
        History = _history.ToList(),
    };

    public void LoadSaveData(WalletSaveData data)
    {
        Balance = data.Balance;
        _history.Clear();
        _history.AddRange(data.History.Take(MaxHistory));
        BalanceChanged?.Invoke(Balance, 0);
    }
}
