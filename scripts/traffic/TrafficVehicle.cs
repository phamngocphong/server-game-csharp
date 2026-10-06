using System.Collections.Generic;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// AI road user driving on the right-hand lane of the road grid. At every intersection
/// it goes straight or turns at random (U-turn only at the map edge). A sensor in front
/// makes it brake for the player and for vehicles going the same way.
/// Drawn procedurally per <see cref="TrafficVehicleData.Shape"/>; faces +X like the player.
/// </summary>
public partial class TrafficVehicle : AnimatableBody2D
{
    public const uint TrafficLayer = 1u << 3;

    private const float Acceleration = 220f;
    private const float BrakeDeceleration = 650f;
    private const float TurnRate = 9f;
    /// <summary>After being blocked this long by another vehicle, ignore it for a moment (unjams crossings).</summary>
    private const float JamTimeout = 3f;
    private const float HitPause = 1.5f;

    public TrafficVehicleData Data { get; private set; } = null!;
    /// <summary>Movement over the last physics frame, in px/s.</summary>
    public Vector2 CurrentVelocity { get; private set; }
    public Vector2 Heading => _heading;

    private CityMap _map = null!;
    private RandomNumberGenerator _rng = null!;
    private readonly Queue<Vector2> _waypoints = new();
    private Vector2I _target;
    private Vector2I _dir;
    private Vector2 _heading = Vector2.Right;
    private float _laneOffset;
    private float _speed;
    private float _cruiseSpeed;
    private float _blockedTime;
    private float _ignoreTrafficTime;
    private float _hitPauseTime;
    private Color _color;
    private Area2D _sensor = null!;

    /// <summary>Places the vehicle on the lane between intersection <paramref name="from"/> and from + dir, <paramref name="t"/> (0..1) of the way.</summary>
    public void Setup(TrafficVehicleData data, CityMap map, Vector2I from, Vector2I dir, float t, RandomNumberGenerator rng)
    {
        Data = data;
        _map = map;
        _rng = rng;
        _dir = dir;
        _target = from + dir;
        _laneOffset = map.Region.RoadWidth * 0.25f;
        _cruiseSpeed = rng.RandfRange(data.MinSpeed, data.MaxSpeed);
        _speed = _cruiseSpeed;
        _color = data.Colors.Length > 0 ? data.Colors[rng.RandiRange(0, data.Colors.Length - 1)] : Colors.Gray;
        _heading = new Vector2(dir.X, dir.Y);

        var start = ExitPoint(from, dir);
        var end = EntryPoint(_target, dir);
        Position = start.Lerp(end, t);
        Rotation = _heading.Angle();
        _waypoints.Enqueue(end);

        CollisionLayer = TrafficLayer;
        CollisionMask = 0;
        // Moved by setting Position each physics frame; SyncToPhysics would reset it to the physics state.
        SyncToPhysics = false;
        ZIndex = 4;
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(data.Length, data.Width) } });
        BuildSensor();
    }

    /// <summary>The player crashed into this vehicle: it stops for a moment.</summary>
    public void OnHitByPlayer()
    {
        _hitPauseTime = HitPause;
        _speed = 0f;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Data == null)
            return;
        var dt = (float)delta;
        _hitPauseTime = Mathf.Max(0f, _hitPauseTime - dt);
        _ignoreTrafficTime = Mathf.Max(0f, _ignoreTrafficTime - dt);

        var blocked = IsBlocked(out var blockedByTraffic);
        if (blocked && blockedByTraffic)
        {
            _blockedTime += dt;
            if (_blockedTime > JamTimeout)
            {
                _ignoreTrafficTime = 1.5f;
                _blockedTime = 0f;
            }
        }
        else
        {
            _blockedTime = 0f;
        }

        var targetSpeed = blocked || _hitPauseTime > 0f ? 0f : _cruiseSpeed;
        var rate = targetSpeed < _speed ? BrakeDeceleration : Acceleration;
        _speed = Mathf.MoveToward(_speed, targetSpeed, rate * dt);

        var before = Position;
        Advance(_speed * dt);
        CurrentVelocity = (Position - before) / dt;
        if (CurrentVelocity.LengthSquared() > 1f)
            _heading = CurrentVelocity.Normalized();
        Rotation = Mathf.LerpAngle(Rotation, _heading.Angle(), Mathf.Clamp(TurnRate * dt, 0f, 1f));
    }

    // --- Route ----------------------------------------------------------------

    private void Advance(float distance)
    {
        var guard = 0;
        while (distance > 0f && guard++ < 8)
        {
            if (_waypoints.Count == 0)
                PlanTurn();
            var next = _waypoints.Peek();
            var toNext = Position.DistanceTo(next);
            if (toNext > distance)
            {
                Position = Position.MoveToward(next, distance);
                return;
            }
            Position = next;
            distance -= toNext;
            _waypoints.Dequeue();
        }
    }

    /// <summary>Reached the entry point of <see cref="_target"/>: pick the next direction and queue the crossing.</summary>
    private void PlanTurn()
    {
        var at = _target;
        var left = new Vector2I(_dir.Y, -_dir.X);
        var right = new Vector2I(-_dir.Y, _dir.X);
        var options = new List<(Vector2I Dir, float Weight)> { (_dir, 0.6f), (left, 0.2f), (right, 0.2f) };
        options.RemoveAll(o => !IsInsideGrid(at + o.Dir));

        Vector2I next;
        if (options.Count == 0)
        {
            next = -_dir; // dead end at the map edge: U-turn
        }
        else
        {
            var total = 0f;
            foreach (var o in options)
                total += o.Weight;
            var roll = _rng.Randf() * total;
            next = options[^1].Dir;
            foreach (var o in options)
            {
                roll -= o.Weight;
                if (roll <= 0f)
                {
                    next = o.Dir;
                    break;
                }
            }
        }

        _dir = next;
        _target = at + next;
        _waypoints.Enqueue(ExitPoint(at, next));
        _waypoints.Enqueue(EntryPoint(_target, next));
    }

    private bool IsInsideGrid(Vector2I node) =>
        node.X >= 0 && node.Y >= 0 && node.X <= _map.Region.GridSize.X && node.Y <= _map.Region.GridSize.Y;

    private static Vector2 Dir(Vector2I d) => new(d.X, d.Y);

    /// <summary>Right-hand side of a heading in screen space (y points down).</summary>
    private static Vector2 RightOf(Vector2I d) => new(-d.Y, d.X);

    /// <summary>Where a vehicle heading <paramref name="dir"/> enters intersection <paramref name="node"/>.</summary>
    private Vector2 EntryPoint(Vector2I node, Vector2I dir) =>
        _map.GetIntersection(node.X, node.Y) + (RightOf(dir) - Dir(dir)) * _laneOffset;

    /// <summary>Where a vehicle leaves intersection <paramref name="node"/> heading <paramref name="dir"/>.</summary>
    private Vector2 ExitPoint(Vector2I node, Vector2I dir) =>
        _map.GetIntersection(node.X, node.Y) + (RightOf(dir) + Dir(dir)) * _laneOffset;

    // --- Sensing --------------------------------------------------------------

    private void BuildSensor()
    {
        var length = 40f + Data.Length * 0.4f;
        _sensor = new Area2D
        {
            CollisionLayer = 0,
            CollisionMask = 1u | TrafficLayer, // player + traffic
            Monitorable = false,
            Position = new Vector2(Data.Length * 0.5f + length * 0.5f, 0f),
        };
        _sensor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(length, Data.Width * 0.9f) } });
        AddChild(_sensor);
    }

    private bool IsBlocked(out bool byTraffic)
    {
        byTraffic = false;
        foreach (var body in _sensor.GetOverlappingBodies())
        {
            if (body == this)
                continue;
            if (body is TrafficVehicle other)
            {
                // Only queue behind vehicles going roughly the same way; crossing traffic is ignored.
                if (_ignoreTrafficTime <= 0f && other.Heading.Dot(_heading) > 0.5f)
                {
                    byTraffic = true;
                    return true;
                }
                continue;
            }
            if (body.IsInGroup("player"))
                return true;
        }
        return false;
    }

    // --- Drawing --------------------------------------------------------------

    public override void _Draw()
    {
        if (Data == null)
            return;
        var l = Data.Length;
        var w = Data.Width;
        var body = new Rect2(-l * 0.5f, -w * 0.5f, l, w);
        DrawRect(new Rect2(body.Position + new Vector2(4, 5), body.Size), new Color(0, 0, 0, 0.25f)); // shadow

        switch (Data.Shape)
        {
            case TrafficVehicleData.BodyShape.Bicycle:
                DrawLine(new Vector2(-l * 0.5f, 0), new Vector2(l * 0.5f, 0), new Color(0.1f, 0.1f, 0.1f), 3f); // wheels + frame
                DrawLine(new Vector2(l * 0.3f, -w * 0.5f), new Vector2(l * 0.3f, w * 0.5f), new Color(0.2f, 0.2f, 0.2f), 2f); // handlebar
                DrawCircle(new Vector2(-2, 0), w * 0.55f, _color); // rider
                DrawCircle(new Vector2(2, 0), w * 0.35f, new Color(0.25f, 0.18f, 0.12f)); // hair
                break;

            case TrafficVehicleData.BodyShape.Motorbike:
                DrawRect(new Rect2(l * 0.3f, -2.5f, l * 0.2f, 5f), new Color(0.08f, 0.08f, 0.08f)); // front wheel
                DrawRect(new Rect2(-l * 0.5f, -2.5f, l * 0.2f, 5f), new Color(0.08f, 0.08f, 0.08f)); // rear wheel
                DrawRect(new Rect2(-l * 0.32f, -w * 0.3f, l * 0.62f, w * 0.6f), _color.Darkened(0.2f)); // body
                DrawLine(new Vector2(l * 0.2f, -w * 0.5f), new Vector2(l * 0.2f, w * 0.5f), new Color(0.2f, 0.2f, 0.2f), 2.5f);
                DrawCircle(new Vector2(-2, 0), w * 0.42f, _color); // rider jacket
                DrawCircle(new Vector2(3, 0), w * 0.3f, _color.Lightened(0.5f)); // helmet
                break;

            case TrafficVehicleData.BodyShape.Car:
                DrawRect(body, _color.Darkened(0.25f));
                DrawRect(body.Grow(-3f), _color);
                DrawRect(new Rect2(l * 0.12f, -w * 0.4f, l * 0.18f, w * 0.8f), new Color(0.15f, 0.2f, 0.28f)); // windshield
                DrawRect(new Rect2(-l * 0.4f, -w * 0.38f, l * 0.12f, w * 0.76f), new Color(0.15f, 0.2f, 0.28f)); // rear window
                DrawRect(new Rect2(-l * 0.1f, -w * 0.4f, l * 0.2f, w * 0.8f), _color.Lightened(0.15f)); // roof
                DrawRect(new Rect2(l * 0.5f - 4f, -w * 0.4f, 4f, 6f), new Color(1f, 0.95f, 0.6f)); // headlights
                DrawRect(new Rect2(l * 0.5f - 4f, w * 0.4f - 6f, 4f, 6f), new Color(1f, 0.95f, 0.6f));
                DrawRect(new Rect2(-l * 0.5f, -w * 0.4f, 3f, 6f), new Color(0.9f, 0.15f, 0.1f)); // tail lights
                DrawRect(new Rect2(-l * 0.5f, w * 0.4f - 6f, 3f, 6f), new Color(0.9f, 0.15f, 0.1f));
                break;

            case TrafficVehicleData.BodyShape.Bus:
                DrawRect(body, _color.Darkened(0.3f));
                DrawRect(body.Grow(-3f), _color);
                DrawRect(new Rect2(l * 0.5f - 12f, -w * 0.42f, 8f, w * 0.84f), new Color(0.15f, 0.2f, 0.28f)); // windshield
                for (var x = -l * 0.42f; x < l * 0.3f; x += 18f)
                    DrawRect(new Rect2(x, -w * 0.3f, 12f, w * 0.6f), _color.Lightened(0.2f)); // roof hatches
                DrawRect(new Rect2(l * 0.5f - 3f, -w * 0.42f, 3f, 7f), new Color(1f, 0.95f, 0.6f));
                DrawRect(new Rect2(l * 0.5f - 3f, w * 0.42f - 7f, 3f, 7f), new Color(1f, 0.95f, 0.6f));
                break;
        }
    }
}
