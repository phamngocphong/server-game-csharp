using System.Collections.Generic;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Owns the delivery flow: board generation, the active job state machine,
/// world markers and interaction. Communicates exclusively via <see cref="EventBus"/>.
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
        ActiveJob = job;
        _availableJobs.Clear();
        EmitJobsUpdated();
        _jobStartedAt = Now();
        SetState(State.ToPickup);
        SpawnMarker(JobMarker.Kind.Pickup, job.PickupPosition, "PICKUP\n" + job.PickupName);
        Bus.EmitSignal(EventBus.SignalName.JobAccepted, job);
        Bus.EmitSignal(EventBus.SignalName.NotificationRequested, "Job accepted - head to the pickup point");
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

    private void PickUpPackage()
    {
        var job = ActiveJob!;
        ClearMarker();
        SetState(State.ToDelivery);
        SpawnMarker(JobMarker.Kind.Delivery, job.DeliveryPosition, "DROP-OFF\n" + job.DeliveryName);
        Bus.EmitSignal(EventBus.SignalName.JobPickedUp, job);
        Bus.EmitSignal(EventBus.SignalName.NotificationRequested, "Package collected - deliver it!");
    }

    private void DeliverPackage()
    {
        var job = ActiveJob!;
        var elapsed = Now() - _jobStartedAt;
        ClearMarker();
        ActiveJob = null;
        SetState(State.Idle);
        var result = GameManager.Instance.CompleteDelivery(job, elapsed);
        Bus.EmitSignal(EventBus.SignalName.JobDelivered, result);
    }

    private void SpawnMarker(JobMarker.Kind kind, Vector2 position, string title)
    {
        _marker = MarkerScene.Instantiate<JobMarker>();
        _marker.Setup(kind, title);
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
        var text = CurrentState == State.ToPickup ? "[E] Pick up package" : "[E] Deliver package";
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
