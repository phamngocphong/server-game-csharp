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
    public float TotalDistance { get; set; }
    public double PlayTime { get; set; }
}

public sealed class PlayerSaveData
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Rotation { get; set; }
}
