using Godot;

namespace ShipperSimulator;

/// <summary>
/// Arrow orbiting the player that points to the current navigation target.
/// Must be TopLevel so it doesn't inherit the bike's rotation.
/// </summary>
public partial class TargetIndicator : Node2D
{
    private const float OrbitRadius = 72f;
    private const float HideDistance = 150f;

    private Vector2 _target;
    private bool _active;
    private Color _color = Colors.White;
    private float _time;

    public override void _Ready()
    {
        Visible = false;
        EventBus.Instance.NavigationTargetChanged += OnTargetChanged;
        EventBus.Instance.NavigationTargetCleared += OnTargetCleared;
    }

    public override void _ExitTree()
    {
        // C# events of [Signal]s declared in C# are plain delegates: Godot does not
        // disconnect them when this node is freed, so unsubscribe explicitly.
        EventBus.Instance.NavigationTargetChanged -= OnTargetChanged;
        EventBus.Instance.NavigationTargetCleared -= OnTargetCleared;
    }

    public override void _Process(double delta)
    {
        if (!_active)
        {
            Visible = false;
            return;
        }
        GlobalPosition = GetParent<Node2D>().GlobalPosition;
        var toTarget = _target - GlobalPosition;
        Visible = toTarget.Length() > HideDistance;
        Rotation = toTarget.Angle();
        _time += (float)delta;
        Modulate = new Color(Modulate, 0.7f + 0.3f * Mathf.Sin(_time * 5f));
    }

    public override void _Draw()
    {
        var tip = OrbitRadius + 20f;
        Vector2[] points =
        {
            new(tip, 0),
            new(OrbitRadius, -13),
            new(OrbitRadius + 5f, 0),
            new(OrbitRadius, 13),
        };
        DrawColoredPolygon(points, _color);
        DrawPolyline(new[] { points[0], points[1], points[2], points[3], points[0] }, new Color(0, 0, 0, 0.6f), 2f);
    }

    private void OnTargetChanged(Vector2 target, string label, Color color)
    {
        _target = target;
        _color = color;
        _active = true;
        QueueRedraw();
    }

    private void OnTargetCleared() => _active = false;
}
