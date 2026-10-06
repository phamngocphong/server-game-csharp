using System.Collections.Generic;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Traffic lights at a share of the city's inner intersections (<see cref="CityRegionData.TrafficLightChance"/>,
/// chosen from the layout seed so they stay put). Each light alternates
/// east-west green -> yellow -> all red -> north-south green -> yellow -> all red, with a random offset.
/// AI traffic asks <see cref="GetSignal"/> before entering; the player is fined for entering on red.
/// Draws the signal heads and stop lines.
/// </summary>
public partial class TrafficLights : Node2D
{
    public enum Signal { None, Green, Yellow, Red }

    [Export] public CityMap Map { get; set; } = null!;

    [ExportGroup("Timing")]
    [Export] public float GreenTime { get; set; } = 8f;
    [Export] public float YellowTime { get; set; } = 2f;
    /// <summary>Both directions red between phases, so the intersection can clear.</summary>
    [Export] public float AllRedTime { get; set; } = 1f;

    [ExportGroup("Fines")]
    [Export] public int BaseFine { get; set; } = 20;
    /// <summary>Each earlier violation within <see cref="RepeatWindow"/> adds one more BaseFine, up to this multiplier.</summary>
    [Export] public int MaxFineMultiplier { get; set; } = 3;
    [Export] public float RepeatWindow { get; set; } = 120f;
    /// <summary>Slower than this (px/s) when entering does not count (e.g. pushed by a crash).</summary>
    [Export] public float MinViolationSpeed { get; set; } = 40f;

    private readonly Dictionary<Vector2I, float> _offsets = new();
    private readonly Dictionary<Vector2I, (Signal Ew, Signal Ns)> _drawnState = new();
    private readonly List<double> _recentViolations = new();
    private double _time;
    private Vector2I? _playerInside;
    private Vector2 _playerLastOutside;

    private float Cycle => 2f * (GreenTime + YellowTime + AllRedTime);

    public IEnumerable<Vector2I> LightNodes => _offsets.Keys;

    public override void _Ready()
    {
        ZIndex = 3;
        if (Map?.Region == null)
            return;
        var rng = new RandomNumberGenerator { Seed = (ulong)Map.ActiveSeed * 31UL + 7UL };
        var grid = Map.Region.GridSize;
        for (var y = 1; y < grid.Y; y++)
        {
            for (var x = 1; x < grid.X; x++)
            {
                if (rng.Randf() < Map.Region.TrafficLightChance)
                    _offsets[new Vector2I(x, y)] = rng.Randf() * Cycle;
            }
        }
    }

    public bool HasLight(Vector2I node) => _offsets.ContainsKey(node);

    /// <summary>Signal shown to traffic moving along <paramref name="dir"/> into <paramref name="node"/>.</summary>
    public Signal GetSignal(Vector2I node, Vector2I dir) => GetSignalForAxis(node, horizontal: dir.X != 0);

    /// <summary>Signal for east-west (<paramref name="horizontal"/>) or north-south traffic.</summary>
    public Signal GetSignalForAxis(Vector2I node, bool horizontal)
    {
        if (!_offsets.TryGetValue(node, out var offset))
            return Signal.None;
        var t = (float)((_time + offset) % Cycle);
        var half = Cycle * 0.5f;
        // First half of the cycle belongs to east-west, second half to north-south.
        var ownHalf = horizontal ? t < half : t >= half;
        if (!ownHalf)
            return Signal.Red;
        var local = t % half;
        if (local < GreenTime)
            return Signal.Green;
        return local < GreenTime + YellowTime ? Signal.Yellow : Signal.Red;
    }

    public override void _PhysicsProcess(double delta)
    {
        _time += delta;
        CheckPlayer();
        RedrawIfChanged();
    }

    // --- Red light enforcement -------------------------------------------------

    private void CheckPlayer()
    {
        var player = GameManager.Instance.Player;
        if (player == null || Map?.Region == null)
            return;
        var pos = player.GlobalPosition;
        var node = NearestNode(pos);
        var center = Map.GetIntersection(node.X, node.Y);
        var halfRoad = Map.Region.RoadWidth * 0.5f;
        var inside = Mathf.Abs(pos.X - center.X) < halfRoad && Mathf.Abs(pos.Y - center.Y) < halfRoad;

        if (!inside)
        {
            _playerInside = null;
            _playerLastOutside = pos;
            return;
        }
        if (_playerInside == node)
            return;
        _playerInside = node;

        // Entered from the side we were on just before: that decides which light applies.
        var from = _playerLastOutside - center;
        var horizontal = Mathf.Abs(from.X) > Mathf.Abs(from.Y);
        if (GetSignalForAxis(node, horizontal) != Signal.Red)
            return;
        if (player.IsStunned || player.Velocity.Length() < MinViolationSpeed)
            return;
        Fine();
    }

    private void Fine()
    {
        _recentViolations.RemoveAll(t => _time - t > RepeatWindow);
        var multiplier = Mathf.Min(1 + _recentViolations.Count, MaxFineMultiplier);
        _recentViolations.Add(_time);
        var amount = BaseFine * multiplier;
        GameManager.Instance.ApplyTrafficFine(amount, "Red light");
        EventBus.Instance.EmitSignal(EventBus.SignalName.TrafficFined, amount, "Ran a red light", multiplier);
    }

    private Vector2I NearestNode(Vector2 pos)
    {
        var rel = (pos - Vector2.One * Map.Region.RoadWidth * 0.5f) / Map.Step;
        var grid = Map.Region.GridSize;
        return new Vector2I(
            Mathf.Clamp(Mathf.RoundToInt(rel.X), 0, grid.X),
            Mathf.Clamp(Mathf.RoundToInt(rel.Y), 0, grid.Y));
    }

    // --- Drawing --------------------------------------------------------------

    private void RedrawIfChanged()
    {
        var changed = false;
        foreach (var node in _offsets.Keys)
        {
            var state = (GetSignalForAxis(node, true), GetSignalForAxis(node, false));
            if (!_drawnState.TryGetValue(node, out var drawn) || drawn != state)
            {
                _drawnState[node] = state;
                changed = true;
            }
        }
        if (changed)
            QueueRedraw();
    }

    public override void _Draw()
    {
        if (Map?.Region == null)
            return;
        var half = Map.Region.RoadWidth * 0.5f;
        Vector2I[] approaches = { Vector2I.Right, Vector2I.Left, Vector2I.Down, Vector2I.Up };
        foreach (var node in _offsets.Keys)
        {
            var center = Map.GetIntersection(node.X, node.Y);
            foreach (var dir in approaches)
            {
                var d = new Vector2(dir.X, dir.Y);
                var right = new Vector2(-d.Y, d.X);
                // Stop line across the incoming (right-hand) lane, at the edge of the intersection.
                var lineStart = center - d * (half + 4f);
                DrawLine(lineStart, lineStart + right * half, new Color(1f, 1f, 1f, 0.85f), 5f);
                // Signal head on the near-right corner, as seen by the approaching driver.
                var headPos = center - d * (half + 14f) + right * (half + 12f);
                DrawSignalHead(headPos, GetSignal(node, dir));
            }
        }
    }

    private void DrawSignalHead(Vector2 pos, Signal signal)
    {
        DrawRect(new Rect2(pos - new Vector2(9, 9), new Vector2(18, 18)), new Color(0.08f, 0.08f, 0.1f));
        var color = signal switch
        {
            Signal.Green => new Color(0.2f, 1f, 0.35f),
            Signal.Yellow => new Color(1f, 0.8f, 0.15f),
            _ => new Color(1f, 0.2f, 0.15f),
        };
        DrawCircle(pos, 11f, new Color(color, 0.25f)); // glow
        DrawCircle(pos, 6.5f, color);
    }
}
