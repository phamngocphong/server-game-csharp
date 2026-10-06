using System.Text.Encodings.Web;
using System.Text.Json;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Reads/writes the save file as JSON. GameManager decides what goes inside;
/// this node only handles storage and versioning.
/// </summary>
public partial class SaveManager : Node
{
    public const string DefaultSavePath = "user://savegame.json";

    /// <summary>File used by save/load. Change it for save slots or to keep tests away from the real save.</summary>
    public string SavePath { get; set; } = DefaultSavePath;
    public const int SaveVersion = 4;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // Keep the file human-readable (no > escapes). Never rendered as HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static SaveManager Instance { get; private set; } = null!;

    public override void _EnterTree() => Instance = this;

    public bool HasSave() => FileAccess.FileExists(SavePath);

    public bool SaveGame(bool silent = false)
    {
        var data = new SaveFile
        {
            Version = SaveVersion,
            SavedAt = Time.GetDatetimeStringFromSystem(),
            Game = GameManager.Instance.ToSaveData(),
        };

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushError($"SaveManager: cannot write {SavePath} ({FileAccess.GetOpenError()})");
            return false;
        }
        file.StoreString(JsonSerializer.Serialize(data, JsonOptions));

        EventBus.Instance.EmitSignal(EventBus.SignalName.GameSaved);
        if (!silent)
            EventBus.Instance.EmitSignal(EventBus.SignalName.NotificationRequested, "Game saved");
        return true;
    }

    public bool LoadGame()
    {
        if (!HasSave())
            return false;

        SaveFile? data;
        try
        {
            data = JsonSerializer.Deserialize<SaveFile>(FileAccess.GetFileAsString(SavePath), JsonOptions);
        }
        catch (JsonException e)
        {
            GD.PushWarning($"SaveManager: save file is corrupted, ignoring it. {e.Message}");
            return false;
        }
        if (data == null)
            return false;

        GameManager.Instance.ApplySaveData(Migrate(data).Game);
        EventBus.Instance.EmitSignal(EventBus.SignalName.GameLoaded);
        return true;
    }

    public void DeleteSave()
    {
        if (HasSave())
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SavePath));
    }

    /// <summary>Upgrades older save formats. Add a branch per version bump.</summary>
    private static SaveFile Migrate(SaveFile data)
    {
        // v1 -> v2: v1 had a single map and no region_id. The empty RegionId never
        // matches a city, so the old player position is dropped; wallet and stats are kept.
        // v2 -> v3: adds reputation and total_tips. Missing values load as a new driver
        // (five starting 5-star ratings) and 0 tips, so nothing to convert.
        // v3 -> v4: adds total_violations and total_fines (default 0).
        data.Version = SaveVersion;
        return data;
    }
}
