using Godot;

namespace ShipperSimulator;

/// <summary>
/// Composition root of the gameplay scene: wires the world into GameManager,
/// restores the save and kicks off the first job board.
/// </summary>
public partial class Main : Node2D
{
    public override void _Ready()
    {
        var cityMap = GetNode<CityMap>("CityMap");
        var player = GetNode<Player>("Player");
        var jobManager = GetNode<JobManager>("JobManager");

        GameManager.Instance.RegisterWorld(cityMap, player);
        player.GlobalPosition = cityMap.GetSpawnPosition();
        player.SetCameraLimits(cityMap.GetMapRect().Grow(200f));

        SaveManager.Instance.LoadGame(); // restores wallet, stats and player position if a save exists

        jobManager.RefreshJobs();
        EventBus.Instance.EmitSignal(EventBus.SignalName.JobBoardOpenRequested);
    }
}
