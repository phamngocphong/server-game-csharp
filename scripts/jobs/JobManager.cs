using System.Collections.Generic;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Owns the delivery flow: board generation, the active job state machine,
/// time limits, world markers and interaction. Communicates exclusively via <see cref="EventBus"/>.
/// </summary>
public partial class JobManager : Node
{
    public enum State { Idle, ToPickup, ToDelivery }

    [Export] public CityMap Map { get; set; } = null!;
    [Export] public Node2D MarkersRoot { get; set; } = null!;
    [Export] public PackedScene MarkerScene { get; set; } = null!;
    [Export] public Godot.Collections.Array<JobTemplate> JobTemplates { get; set; } = new();
    [Export] public int JobsPerBoard { get; set; } = 5;
    /// <summary>Opening the board after moving this far (px) regenerates nearby jobs.</summary>
    [Export] public float RegenerateDistance { get; set; } = 900f;

    public State CurrentState { get; private set; } = State.Idle;
    public IReadOnlyList<JobData> AvailableJobs => _availableJobs;
    public JobData? ActiveJob { get; private set; }
    public bool IsPlayerInMarker { get; private set; }

    private List<JobData> _availableJobs = new();
    private JobGenerator _generator = null!;
    private JobMarker? _marker;
    private double _jobStartedAt;
    private double _pickedUpAt;
    /// <summary>Game time spent in the current phase (to pickup / to drop-off); drives the time limits.</summary>
    private double _phaseTime;
    private bool _isLate;
    private Vector2 _lastOrigin = Vector2.Inf;

    private static EventBus Bus => EventBus.Instance;

    public override void _Ready()
    {
        _generator = new JobGenerator(Map, JobTemplates);
        Bus.JobAcceptRequested += AcceptJob;
        Bus.JobRefreshRequested += RefreshJobs;
        Bus.JobCancelRequested += CancelActiveJob;
        Bus.JobBoardOpened += OnJobBoardOpened;
        Bus.DeliveryPopupClosed += OnDeliveryPopupClosed;
    }

    public override void _ExitTree()
    {
        // C# events of [Signal]s declared in C# are plain delegates: Godot does not
        // disconnect them when this node is freed, so unsubscribe explicitly.
        Bus.JobAcceptRequested -= AcceptJob;
        Bus.JobRefreshRequested -= RefreshJobs;
        Bus.JobCancelRequested -= CancelActiveJob;
        Bus.JobBoardOpened -= OnJobBoardOpened;
        Bus.DeliveryPopupClosed -= OnDeliveryPopupClosed;
    }

    public override void _Process(double delta)
    {
        var job = ActiveJob;
        if (job == null || !job.IsTimed || CurrentState == State.Idle)
            return;

        _phaseTime += delta;
        var limit = CurrentState == State.ToPickup ? job.PickupTimeLimit : job.DeliveryTimeLimit;
        var remaining = limit - (float)_phaseTime;
        Bus.EmitSignal(EventBus.SignalName.JobTimerUpdated, remaining, limit);
        if (remaining > 0f)
            return;

        if (CurrentState == State.ToPickup)
        {
            FailActiveJob(job.IsPassenger
                ? "The passenger cancelled - you took too long to arrive"
                : "Order cancelled - you took too long to pick it up");
        }
        else if (!_isLate)
        {
            _isLate = true;
            if (job.LatePenalty >= 1f)
            {
                FailActiveJob(job.IsPassenger
                    ? "The passenger gave up and got off"
                    : "The customer refused the late delivery");
                return;
            }
            Bus.EmitSignal(EventBus.SignalName.NotificationRequested,
                $"Running late! Reward cut by {Mathf.RoundToInt(job.LatePenalty * 100f)}%");
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("interact"))
            return;
        if (!IsPlayerInMarker || CurrentState == State.Idle)
            return;
        GetViewport().SetInputAsHandled();
        switch (CurrentState)
        {
            case State.ToPickup:
                PickUpPackage();
                break;
            case State.ToDelivery:
                DeliverPackage();
                break;
        }
    }

    /// <summary>Generates a fresh set of jobs around the player's current position.</summary>
    public void RefreshJobs()
    {
        if (CurrentState != State.Idle)
            return;
        var origin = GameManager.Instance.GetPlayerPosition();
        _lastOrigin = origin;
        _availableJobs = _generator.GenerateJobs(origin, JobsPerBoard);
        EmitJobsUpdated();
    }

    public void AcceptJob(JobData job)
    {
        if (CurrentState != State.Idle || !_availableJobs.Contains(job))
            return;
        _generator.UpdatePickupTimeLimit(job, GameManager.Instance.GetPlayerPosition());
        ActiveJob = job;
        _availableJobs.Clear();
        EmitJobsUpdated();
        _jobStartedAt = Now();
        _phaseTime = 0.0;
        _isLate = false;
        SetState(State.ToPickup);
        SpawnMarker(JobMarker.Kind.Pickup, job.PickupPosition, "PICKUP\n" + job.PickupName, job.IsPassenger);
        Bus.EmitSignal(EventBus.SignalName.JobAccepted, job);
        var message = job.IsPassenger ? "Ride accepted - pick up the passenger" : "Job accepted - head to the pickup point";
        if (job.IsTimed)
            message += $" within {GameManager.FormatTime(job.PickupTimeLimit)}";
        Bus.EmitSignal(EventBus.SignalName.NotificationRequested, message);
    }

    public void CancelActiveJob()
    {
        if (CurrentState == State.Idle || ActiveJob == null)
            return;
        var job = ActiveJob;
        ClearMarker();
        ActiveJob = null;
        SetState(State.Idle);
        Bus.EmitSignal(EventBus.SignalName.JobCancelled, job);
        Bus.EmitSignal(EventBus.SignalName.NotificationRequested, "Job cancelled");
        RefreshJobs();
    }

    /// <summary>A timed job ran out of time: drop it without pay and offer new jobs.</summary>
    public void FailActiveJob(string reason)
    {
        if (CurrentState == State.Idle || ActiveJob == null)
            return;
        var job = ActiveJob;
        ClearMarker();
        ActiveJob = null;
        SetState(State.Idle);
        Bus.EmitSignal(EventBus.SignalName.JobFailed, job, reason);
        Bus.EmitSignal(EventBus.SignalName.NotificationRequested, reason);
        RefreshJobs();
        Bus.EmitSignal(EventBus.SignalName.JobBoardOpenRequested);
    }

    private void PickUpPackage()
    {
        var job = ActiveJob!;
        ClearMarker();
        _pickedUpAt = Now();
        _phaseTime = 0.0;
        SetState(State.ToDelivery);
        SpawnMarker(JobMarker.Kind.Delivery, job.DeliveryPosition, "DROP-OFF\n" + job.DeliveryName, job.IsPassenger);
        Bus.EmitSignal(EventBus.SignalName.JobPickedUp, job);
        var message = job.IsPassenger ? "Passenger on board - drive safely!" : "Package collected - deliver it!";
        if (job.IsTimed)
            message += $" ({GameManager.FormatTime(job.DeliveryTimeLimit)})";
        Bus.EmitSignal(EventBus.SignalName.NotificationRequested, message);
    }

    private void DeliverPackage()
    {
        var job = ActiveJob!;
        var elapsed = Now() - _jobStartedAt;
        var deliveryTime = Now() - _pickedUpAt;
        ClearMarker();
        ActiveJob = null;
        SetState(State.Idle);
        var result = GameManager.Instance.CompleteDelivery(job, elapsed, deliveryTime, _isLate);
        Bus.EmitSignal(EventBus.SignalName.JobDelivered, result);
    }

    private void SpawnMarker(JobMarker.Kind kind, Vector2 position, string title, bool passenger)
    {
        _marker = MarkerScene.Instantiate<JobMarker>();
        _marker.Setup(kind, title, passenger);
        _marker.Position = position;
        _marker.PlayerEntered += OnMarkerEntered;
        _marker.PlayerExited += OnMarkerExited;
        MarkersRoot.AddChild(_marker);
        Bus.EmitSignal(EventBus.SignalName.NavigationTargetChanged, position, title, _marker.GetColor());
    }

    private void ClearMarker()
    {
        if (_marker != null)
        {
            _marker.PlayerEntered -= OnMarkerEntered;
            _marker.PlayerExited -= OnMarkerExited;
            _marker.QueueFree();
            _marker = null;
        }
        IsPlayerInMarker = false;
        Bus.EmitSignal(EventBus.SignalName.InteractionPromptChanged, "");
        Bus.EmitSignal(EventBus.SignalName.NavigationTargetCleared);
    }

    private void SetState(State state)
    {
        CurrentState = state;
        Bus.EmitSignal(EventBus.SignalName.JobStateChanged, (int)state, ActiveJob!);
    }

    private void EmitJobsUpdated() =>
        Bus.EmitSignal(EventBus.SignalName.JobsUpdated, new Godot.Collections.Array<JobData>(_availableJobs));

    private void OnMarkerEntered()
    {
        IsPlayerInMarker = true;
        var passenger = ActiveJob?.IsPassenger ?? false;
        var text = CurrentState == State.ToPickup
            ? passenger ? "[E] Pick up passenger" : "[E] Pick up package"
            : passenger ? "[E] Drop off passenger" : "[E] Deliver package";
        Bus.EmitSignal(EventBus.SignalName.InteractionPromptChanged, text);
    }

    private void OnMarkerExited()
    {
        IsPlayerInMarker = false;
        Bus.EmitSignal(EventBus.SignalName.InteractionPromptChanged, "");
    }

    private void OnJobBoardOpened()
    {
        if (CurrentState != State.Idle)
            return;
        var moved = GameManager.Instance.GetPlayerPosition().DistanceTo(_lastOrigin);
        if (_availableJobs.Count == 0 || moved > RegenerateDistance)
            RefreshJobs();
    }

    private void OnDeliveryPopupClosed()
    {
        RefreshJobs();
        Bus.EmitSignal(EventBus.SignalName.JobBoardOpenRequested);
    }

    private static double Now() => Time.GetTicksMsec() / 1000.0;
}
