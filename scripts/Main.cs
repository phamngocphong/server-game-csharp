using Godot;

namespace ShipperSimulator;

/// <summary>
/// Composition root of the gameplay scene: picks the city (the saved one when continuing,
/// a random one for a new game), wires the world into GameManager, restores the save
/// and kicks off the first job board.
/// </summary>
public partial class Main : Node2D
{
    /// <summary>Folder with the city JSON files.</summary>
    [Export(PropertyHint.Dir)] public string CitiesFolder { get; set; } = CityLoader.DefaultFolder;

    private bool _isNewGame;

    public override void _EnterTree()
    {
        // _EnterTree runs parent-first, so this happens before CityMap._Ready() builds the map.
        var cities = CityLoader.LoadAll(CitiesFolder);
        if (cities.Count == 0)
            return;
        var cityMap = GetNode<CityMap>("CityMap");

        _isNewGame = GameManager.Instance.NextStart == GameManager.StartMode.NewGame;
        var save = _isNewGame ? null : SaveManager.Instance.ReadSave();
        var savedCity = save == null ? null : cities.Find(c => c.RegionId == save.Game.RegionId);
        if (savedCity != null)
        {
            // Continue: rebuild exactly the saved layout so the saved position is valid.
            cityMap.Region = savedCity;
            cityMap.SeedOverride = save!.Game.LayoutSeed;
            return;
        }
        if (save != null)
            GD.PushWarning($"Main: saved city \"{save.Game.RegionId}\" no longer exists; picking a random one.");
        cityMap.Region = cities[(int)(GD.Randi() % (uint)cities.Count)];
    }

    public override void _Ready()
    {
        var cityMap = GetNode<CityMap>("CityMap");
        var player = GetNode<Player>("Player");
        var jobManager = GetNode<JobManager>("JobManager");
        if (cityMap.Region == null)
        {
            GD.PushError($"Main: no valid city in {CitiesFolder}, returning to the menu.");
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://ui/main_menu.tscn");
            return;
        }

        GameManager.Instance.RegisterWorld(cityMap, player);
        player.GlobalPosition = cityMap.GetSpawnPosition();
        player.SetCameraLimits(cityMap.GetMapRect().Grow(200f));

        string welcome;
        if (_isNewGame)
        {
            // Save right away so "Continue" brings the player back to this city.
            SaveManager.Instance.SaveGame(silent: true);
            GameManager.Instance.NextStart = GameManager.StartMode.Continue;
            welcome = $"New game - welcome to {cityMap.Region.DisplayName}!";
        }
        else
        {
            // Restores wallet, stats, rating and fuel; the position only if the city and seed match.
            welcome = SaveManager.Instance.LoadGame()
                ? $"Welcome back to {cityMap.Region.DisplayName}!"
                : $"Welcome to {cityMap.Region.DisplayName}!";
        }

        jobManager.RefreshJobs();
        EventBus.Instance.EmitSignal(EventBus.SignalName.NotificationRequested, welcome);
        EventBus.Instance.EmitSignal(EventBus.SignalName.JobBoardOpenRequested);
    }

    public override void _ExitTree() => GameManager.Instance.UnregisterWorld();
}
