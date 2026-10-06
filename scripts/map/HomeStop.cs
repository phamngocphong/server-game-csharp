using Godot;

namespace ShipperSimulator;

/// <summary>
/// The player's home on the map, placed by <see cref="CityMap"/>. Press E to rest for free;
/// how fast fatigue recovers depends on the home (<see cref="HousingData.RestSeconds"/>).
/// Drawn as a small house in the current home's color, bigger for better homes.
/// </summary>
public partial class HomeStop : ServiceStop
{
    protected override Color AccentColor => new(1f, 0.6f, 0.85f);

    public override void _Ready()
    {
        base._Ready();
        EventBus.Instance.HousingChanged += QueueRedraw;
    }

    public override void _ExitTree()
    {
        // C# events of [Signal]s declared in C# are plain delegates: Godot does not
        // disconnect them when this node is freed, so unsubscribe explicitly.
        EventBus.Instance.HousingChanged -= QueueRedraw;
    }

    protected override string Prompt()
    {
        var home = GameManager.Instance.Housing;
        return $"[E] Rest at home - free  ({home.DisplayName}, {home.RestSeconds:0} s)";
    }

    protected override void Use() => GameManager.Instance.RestAtHome();

    public override void _Draw()
    {
        DrawZone();
        var home = GameManager.Instance.Housing;
        var size = 1f + home.Tier * 0.15f;
        var wall = home.AccentColor;
        // Roof, walls, door and a window.
        DrawColoredPolygon(new[] { new Vector2(-24, -4) * size, new Vector2(0, -24) * size, new Vector2(24, -4) * size }, wall.Darkened(0.35f));
        DrawRect(new Rect2(new Vector2(-18, -4) * size, new Vector2(36, 24) * size), wall);
        DrawRect(new Rect2(new Vector2(-5, 6) * size, new Vector2(10, 14) * size), wall.Darkened(0.5f));
        DrawRect(new Rect2(new Vector2(8, 0) * size, new Vector2(7, 7) * size), new Color(1f, 0.95f, 0.6f));
        DrawSign("HOME");
    }
}
