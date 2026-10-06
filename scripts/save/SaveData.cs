using System.Collections.Generic;

namespace ShipperSimulator;

// Plain DTOs serialized with System.Text.Json (snake_case keys).
// Bump SaveManager.SaveVersion and add a migration when changing these.

public sealed class SaveFile
{
    public int Version { get; set; } = SaveManager.SaveVersion;
    public string SavedAt { get; set; } = "";
    public GameSaveData Game { get; set; } = new();
}

public sealed class GameSaveData
{
    public WalletSaveData Wallet { get; set; } = new();
    public StatsSaveData Stats { get; set; } = new();
    public ReputationSaveData Reputation { get; set; } = new();
    /// <summary>City and layout seed the player position belongs to.</summary>
    public string RegionId { get; set; } = "";
    public int LayoutSeed { get; set; }
    public PlayerSaveData? Player { get; set; }
}

public sealed class WalletSaveData
{
    public int Balance { get; set; }
    public List<WalletEntry> History { get; set; } = new();
}

public sealed class StatsSaveData
{
    public int TotalDeliveries { get; set; }
    public int TotalEarned { get; set; }
    public int BestReward { get; set; }
    public int TotalTips { get; set; }
    public int TotalViolations { get; set; }
    public int TotalFines { get; set; }
    public float TotalDistance { get; set; }
    public double PlayTime { get; set; }
}

public sealed class ReputationSaveData
{
    /// <summary>Stars (1-5) of the most recent jobs, oldest first. Empty = new driver.</summary>
    public List<int> RecentRatings { get; set; } = new();
    public int TotalOnTime { get; set; }
    public int TotalLate { get; set; }
    public int TotalFailed { get; set; }
    public int TotalCancelled { get; set; }
}

public sealed class PlayerSaveData
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Rotation { get; set; }
}
