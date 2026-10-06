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
    /// <summary>Preferences file (touch controls...); separate from the save so New Game keeps it.</summary>
    public string SettingsPath { get; set; } = "user://settings.cfg";
    public GameSettings Settings { get; private set; } = new();
    public const int SaveVersion = 9;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // Keep the file human-readable (no > escapes). Never rendered as HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static SaveManager Instance { get; private set; } = null!;

    public override void _EnterTree() => Instance = this;

    public override void _Ready() => LoadSettings();

    public void LoadSettings()
    {
        Settings = new GameSettings();
        var config = new ConfigFile();
        if (config.Load(SettingsPath) != Error.Ok)
            return;
        if (System.Enum.TryParse<GameSettings.TouchMode>((string)config.GetValue("controls", "touch", "Auto"), out var touch))
            Settings.TouchControls = touch;
        if (System.Enum.TryParse<GameSettings.JoystickMode>((string)config.GetValue("controls", "joystick", "Direction"), out var joystick))
            Settings.Joystick = joystick;
    }

    /// <summary>Writes the settings and tells listeners (touch controls...) to re-apply them.</summary>
    public void SaveSettings()
    {
        var config = new ConfigFile();
        config.SetValue("controls", "touch", Settings.TouchControls.ToString());
        config.SetValue("controls", "joystick", Settings.Joystick.ToString());
        var error = config.Save(SettingsPath);
        if (error != Error.Ok)
            GD.PushError($"SaveManager: cannot write {SettingsPath} ({error})");
        EventBus.Instance.EmitSignal(EventBus.SignalName.SettingsChanged);
    }

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
        var data = ReadSave();
        if (data == null)
            return false;
        GameManager.Instance.ApplySaveData(data.Game);
        EventBus.Instance.EmitSignal(EventBus.SignalName.GameLoaded);
        return true;
    }

    /// <summary>
    /// Reads and migrates the save without applying it (main menu summary, choosing the city
    /// before the map is built). Null when there is no save or it is corrupted.
    /// </summary>
    public SaveFile? ReadSave()
    {
        if (!HasSave())
            return null;
        try
        {
            var data = JsonSerializer.Deserialize<SaveFile>(FileAccess.GetFileAsString(SavePath), JsonOptions);
            return data == null ? null : Migrate(data);
        }
        catch (JsonException e)
        {
            GD.PushWarning($"SaveManager: save file is corrupted, ignoring it. {e.Message}");
            return null;
        }
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
        // v4 -> v5: adds fuel (missing = full tank) and total_fuel_spent (default 0).
        // v5 -> v6: adds vehicle (missing = starter vehicle) and shop (sold offers of the rotation).
        // v6 -> v7: adds fatigue (default 0) and total_rest_spent (default 0).
        // v7 -> v8: adds phone_id (empty = starter phone), refresh_day and refreshes_used.
        // v8 -> v9: adds housing_id (empty = starter home), last_billed_month (empty = this month,
        // so nobody is billed just for updating) and total_housing_spent.
        data.Version = SaveVersion;
        return data;
    }
}
