using Godot;

namespace ShipperSimulator;

/// <summary>
/// Composition root of the gameplay scene: picks the city, wires the world into
/// GameManager, restores the save and kicks off the first job board.
/// </summary>
public partial class Main : Node2D
{
    /// <summary>Folder with the city JSON files; one city is picked at random every time the scene starts.</summary>
    [Export(PropertyHint.Dir)] public string CitiesFolder { get; set; } = CityLoader.DefaultFolder;

    public override void _EnterTree()
    {
        // _EnterTree runs parent-first, so this happens before CityMap._Ready() builds the map.
        var cities = CityLoader.LoadAll(CitiesFolder);
        if (cities.Count > 0)
            GetNode<CityMap>("CityMap").Region = cities[(int)(GD.Randi() % (uint)cities.Count)];
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

        SaveManager.Instance.LoadGame(); // restores wallet and stats; player position only if the city matches

        jobManager.RefreshJobs();
        EventBus.Instance.EmitSignal(EventBus.SignalName.NotificationRequested, $"Welcome to {cityMap.Region.DisplayName}!");
        EventBus.Instance.EmitSignal(EventBus.SignalName.JobBoardOpenRequested);
    }

    public override void _ExitTree() => GameManager.Instance.UnregisterWorld();
}
