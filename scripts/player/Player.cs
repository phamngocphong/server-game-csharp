using Godot;

namespace ShipperSimulator;

/// <summary>
/// Top-down motorbike controller.
/// W/S throttle and brake/reverse, A/D steer (needs speed to turn), Space handbrake.
/// Crashing into traffic stuns the player (no control) for the vehicle's
/// <see cref="TrafficVehicleData.StunSeconds"/>.
/// </summary>
public partial class Player : CharacterBody2D
{
    private const float CameraLookahead = 0.35f;
    private const float CameraZoomSlow = 1f;
    private const float CameraZoomFast = 0.8f;
    /// <summary>Closing speed (px/s, about 11 km/h) needed for a hit to count as a crash.</summary>
    private const float MinCrashSpeed = 90f;
    /// <summary>Seconds after a stun during which new crashes are ignored.</summary>
    private const float CrashImmunity = 1f;
    private const float CrashKnockback = 140f;

    [Export] public VehicleStats VehicleStats { get; set; } = null!;

    public bool ControlsEnabled { get; private set; } = true;
    /// <summary>Signed speed along the heading (px/s).</summary>
    public float ForwardSpeed { get; private set; }
    /// <summary>Seconds of stun left after a crash; 0 = can drive.</summary>
    public float StunTimeLeft { get; private set; }
    public bool IsStunned => StunTimeLeft > 0f;

    private float _crashImmunity;

    private BikeVisual _visual = null!;
    private Camera2D _camera = null!;

    public override void _Ready()
    {
        VehicleStats ??= new VehicleStats();
        _visual = GetNode<BikeVisual>("BikeVisual");
        _camera = GetNode<Camera2D>("Camera2D");

        var bus = EventBus.Instance;
        bus.PlayerControlsLocked += OnControlsLocked;
        bus.JobPickedUp += OnJobPickedUp;
        bus.JobDelivered += OnJobDelivered;
        bus.JobCancelled += OnJobCancelled;
        bus.JobFailed += OnJobFailed;
    }

    public override void _ExitTree()
    {
        // C# events of [Signal]s declared in C# are plain delegates: Godot does not
        // disconnect them when this node is freed, so unsubscribe explicitly.
        var bus = EventBus.Instance;
        bus.PlayerControlsLocked -= OnControlsLocked;
        bus.JobPickedUp -= OnJobPickedUp;
        bus.JobDelivered -= OnJobDelivered;
        bus.JobCancelled -= OnJobCancelled;
        bus.JobFailed -= OnJobFailed;
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        if (StunTimeLeft > 0f)
        {
            StunTimeLeft = Mathf.Max(0f, StunTimeLeft - dt);
            if (StunTimeLeft <= 0f)
                _crashImmunity = CrashImmunity;
        }
        else
        {
            _crashImmunity = Mathf.Max(0f, _crashImmunity - dt);
        }

        var throttle = 0f;
        var steer = 0f;
        var handbrake = false;
        if (ControlsEnabled && !IsStunned)
        {
            throttle = Input.GetAxis("move_down", "move_up");
            steer = Input.GetAxis("move_left", "move_right");
            handbrake = Input.IsActionPressed("handbrake");
        }

        UpdateSpeed(throttle, handbrake, dt);
        UpdateSteering(steer, dt);

        var forward = Vector2.Right.Rotated(Rotation);
        var grip = handbrake ? VehicleStats.HandbrakeGrip : VehicleStats.Grip;
        Velocity = Velocity.Lerp(forward * ForwardSpeed, Mathf.Clamp(grip * dt, 0f, 1f));
        var velocityBefore = Velocity;
        MoveAndSlide();
        CheckTrafficCrash(velocityBefore);
        if (GetSlideCollisionCount() > 0 && !IsStunned)
        {
            // Hitting a wall eats the speed that went into it.
            ForwardSpeed = Mathf.Clamp(Velocity.Dot(forward), -VehicleStats.ReverseMaxSpeed, VehicleStats.MaxSpeed);
        }

        UpdateVisuals(steer, dt);
        UpdateCamera(dt);
    }

    public float GetSpeedRatio() => Mathf.Clamp(Mathf.Abs(ForwardSpeed) / VehicleStats.MaxSpeed, 0f, 1f);

    public float GetSpeedKmh() => Velocity.Length() * GameManager.SpeedToKmh;

    public void SetCameraLimits(Rect2 rect)
    {
        _camera.LimitLeft = (int)rect.Position.X;
        _camera.LimitTop = (int)rect.Position.Y;
        _camera.LimitRight = (int)rect.End.X;
        _camera.LimitBottom = (int)rect.End.Y;
    }

    public PlayerSaveData GetSaveData() => new()
    {
        X = GlobalPosition.X,
        Y = GlobalPosition.Y,
        Rotation = Rotation,
    };

    public void ApplySaveData(PlayerSaveData data)
    {
        GlobalPosition = new Vector2(data.X, data.Y);
        Rotation = data.Rotation;
        Velocity = Vector2.Zero;
        ForwardSpeed = 0f;
        _camera.ResetSmoothing();
    }

    private void CheckTrafficCrash(Vector2 velocityBefore)
    {
        if (IsStunned || _crashImmunity > 0f)
            return;
        for (var i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);
            if (collision.GetCollider() is not TrafficVehicle vehicle)
                continue;
            // Closing speed along the contact normal (the normal points from the vehicle to the player).
            var normal = collision.GetNormal();
            var impact = (velocityBefore - vehicle.CurrentVelocity).Dot(-normal);
            if (impact < MinCrashSpeed)
                continue;
            Crash(vehicle, normal);
            return;
        }
    }

    private void Crash(TrafficVehicle vehicle, Vector2 normal)
    {
        var data = vehicle.Data;
        StunTimeLeft = data.StunSeconds;
        ForwardSpeed = 0f;
        Velocity = normal * CrashKnockback;
        vehicle.OnHitByPlayer();
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerCrashed, data.DisplayName, data.CollisionScore, data.StunSeconds);
    }

    private void UpdateSpeed(float throttle, bool handbrake, float dt)
    {
        var s = VehicleStats;
        if (throttle > 0f)
        {
            ForwardSpeed = ForwardSpeed < 0f
                ? Mathf.MoveToward(ForwardSpeed, 0f, s.BrakeDeceleration * dt)
                : Mathf.MoveToward(ForwardSpeed, s.MaxSpeed * throttle, s.Acceleration * dt);
        }
        else if (throttle < 0f)
        {
            ForwardSpeed = ForwardSpeed > 0f
                ? Mathf.MoveToward(ForwardSpeed, 0f, s.BrakeDeceleration * dt)
                : Mathf.MoveToward(ForwardSpeed, -s.ReverseMaxSpeed, s.Acceleration * 0.5f * dt);
        }
        else
        {
            ForwardSpeed = Mathf.MoveToward(ForwardSpeed, 0f, s.CoastDeceleration * dt);
        }

        if (handbrake)
            ForwardSpeed = Mathf.MoveToward(ForwardSpeed, 0f, s.BrakeDeceleration * 0.4f * dt);
    }

    private void UpdateSteering(float steer, float dt)
    {
        var ratio = GetSpeedRatio();
        // A bike needs some speed to turn and gets a little less twitchy at top speed.
        var steerFactor = Mathf.Clamp(ratio * 4f, 0f, 1f) * Mathf.Lerp(1f, 0.65f, ratio);
        Rotation += steer * VehicleStats.TurnSpeed * steerFactor * Mathf.Sign(ForwardSpeed) * dt;
    }

    private void UpdateVisuals(float steer, float dt)
    {
        _visual.Lean = Mathf.Lerp(_visual.Lean, steer * GetSpeedRatio(), Mathf.Clamp(8f * dt, 0f, 1f));
        // Blink while stunned.
        var blink = IsStunned && (int)(StunTimeLeft * 8f) % 2 == 0;
        _visual.Modulate = blink ? new Color(1f, 0.45f, 0.45f, 0.6f) : Colors.White;
    }

    private void UpdateCamera(float dt)
    {
        _camera.Offset = _camera.Offset.Lerp(Velocity * CameraLookahead, Mathf.Clamp(2.5f * dt, 0f, 1f));
        var targetZoom = Mathf.Lerp(CameraZoomSlow, CameraZoomFast, GetSpeedRatio());
        _camera.Zoom = _camera.Zoom.Lerp(Vector2.One * targetZoom, Mathf.Clamp(1.5f * dt, 0f, 1f));
    }

    private void OnControlsLocked(bool locked) => ControlsEnabled = !locked;
    private void OnJobPickedUp(JobData job)
    {
        _visual.HasPassenger = job.IsPassenger;
        _visual.HasPackage = !job.IsPassenger;
    }

    private void OnJobDelivered(DeliveryResult result) => ClearCargo();
    private void OnJobCancelled(JobData job) => ClearCargo();
    private void OnJobFailed(JobData job, string reason) => ClearCargo();

    private void ClearCargo()
    {
        _visual.HasPackage = false;
        _visual.HasPassenger = false;
    }
}
