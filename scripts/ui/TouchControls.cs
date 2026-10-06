using System.Collections.Generic;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// On-screen controls for mobile builds: a floating joystick on the left half of the screen
/// and BRAKE / E / JOBS buttons on the right (pausing uses the HUD pause button). Shown when
/// <see cref="GameSettings.TouchControlsActive"/> (mobile build, touchscreen, or forced on).
/// It drives the normal input actions (move_*, interact, toggle_job_board), so the
/// gameplay code does not know whether a keyboard or a finger is used. Multi-touch aware.
/// Touches that start on visible UI (<see cref="BlockingControls"/>) are left to the UI.
/// </summary>
public partial class TouchControls : Control
{
    private enum Role { None, Joystick, Brake, Interact, Jobs }

    private const float JoyRadius = 90f;
    private const float KnobRadius = 38f;
    private const float DeadZone = 0.15f;
    /// <summary>Direction mode: how hard the bike steers per radian of heading error.</summary>
    private const float DirectionSteerGain = 2.5f;
    private const int MouseTouchIndex = 0;

    private static readonly string[] MoveActions = { "move_up", "move_down", "move_left", "move_right" };
    private static readonly Color BaseColor = new(1f, 1f, 1f, 0.12f);
    private static readonly Color RingColor = new(1f, 1f, 1f, 0.35f);
    private static readonly Color KnobColor = new(1f, 0.62f, 0.15f, 0.75f);
    private static readonly Color ButtonColor = new(0.08f, 0.09f, 0.12f, 0.55f);
    private static readonly Color PressedColor = new(1f, 0.62f, 0.15f, 0.6f);

    /// <summary>Visible UI where touches must go to the UI, not to the joystick or buttons.</summary>
    [Export] public Godot.Collections.Array<NodePath> BlockingControls { get; set; } = new();
    /// <summary>Keyboard hint label to hide while touch controls are shown.</summary>
    [Export] public NodePath KeyboardHint { get; set; } = new();

    private readonly Dictionary<int, Role> _touches = new();
    private readonly HashSet<string> _pressedByTouch = new();
    private readonly List<Control> _blockers = new();
    private Control? _keyboardHint;
    private bool _active;
    private int _joyTouch = -1;
    private Vector2 _joyCenter;
    private Vector2 _joyVector;
    private bool _interactAvailable;

    private Vector2 RestCenter => new(190f, Size.Y - 250f);
    private Rect2 JoystickZone => new(0f, Size.Y * 0.3f, Size.X * 0.45f, Size.Y * 0.7f);

    private (Role Role, Vector2 Center, float Radius, string Label)[] Buttons => new[]
    {
        (Role.Brake, new Vector2(Size.X - 140f, Size.Y - 150f), 68f, "BRAKE"),
        (Role.Interact, new Vector2(Size.X - 290f, Size.Y - 105f), 52f, "E"),
        (Role.Jobs, new Vector2(Size.X - 110f, Size.Y - 310f), 44f, "JOBS"),
    };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var path in BlockingControls)
        {
            if (GetNodeOrNull<Control>(path) is { } control)
                _blockers.Add(control);
        }
        _keyboardHint = KeyboardHint.IsEmpty ? null : GetNodeOrNull<Control>(KeyboardHint);
        EventBus.Instance.InteractionPromptChanged += OnPromptChanged;
        EventBus.Instance.SettingsChanged += ApplySettings;
        ApplySettings();
    }

    public override void _ExitTree()
    {
        // C# events of [Signal]s declared in C# are plain delegates: Godot does not
        // disconnect them when this node is freed, so unsubscribe explicitly.
        EventBus.Instance.InteractionPromptChanged -= OnPromptChanged;
        EventBus.Instance.SettingsChanged -= ApplySettings;
        ReleaseAll();
    }

    public override void _Notification(int what)
    {
        // Paused (pause menu) or app sent to the background: let go of everything.
        if (what is (int)NotificationPaused or (int)NotificationApplicationFocusOut)
            ReleaseAll();
    }

    public override void _Process(double delta)
    {
        if (!_active)
            return;
        ApplyMovement();
        QueueRedraw();
    }

    // --- Input ----------------------------------------------------------------

    public override void _Input(InputEvent @event)
    {
        if (!_active)
            return;
        switch (@event)
        {
            case InputEventScreenTouch touch:
                OnTouch(touch.Index, touch.Position, touch.Pressed);
                break;
            case InputEventScreenDrag drag:
                OnDrag(drag.Index, drag.Position);
                break;
            // Desktop testing with the setting forced On: the left mouse button acts as one finger.
            // Mouse events that Godot emulates from touches are skipped (already handled above).
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse when IsRealMouse(mouse):
                OnTouch(MouseTouchIndex, mouse.Position, mouse.Pressed);
                break;
            case InputEventMouseMotion motion when IsRealMouse(motion) && _touches.ContainsKey(MouseTouchIndex):
                OnDrag(MouseTouchIndex, motion.Position);
                break;
        }
    }

    private static bool IsRealMouse(InputEvent @event) =>
        @event.Device != InputEvent.DeviceIdEmulation && !DisplayServer.IsTouchscreenAvailable();

    private void OnTouch(int index, Vector2 position, bool pressed)
    {
        if (!pressed)
        {
            if (_touches.Remove(index, out var released))
                OnReleased(index, released);
            return;
        }

        var role = HitTest(position);
        if (role == Role.None)
            return; // not ours: the UI (or nothing) handles it
        _touches[index] = role;
        GetViewport().SetInputAsHandled();
        switch (role)
        {
            case Role.Joystick:
                _joyTouch = index;
                _joyCenter = position;
                _joyVector = Vector2.Zero;
                break;
            case Role.Interact:
                SendActionTap("interact");
                break;
            case Role.Jobs:
                SendActionTap("toggle_job_board");
                break;
        }
    }

    private void OnDrag(int index, Vector2 position)
    {
        if (index != _joyTouch)
            return;
        var offset = position - _joyCenter;
        _joyVector = offset.LimitLength(JoyRadius) / JoyRadius;
        GetViewport().SetInputAsHandled();
    }

    private void OnReleased(int index, Role role)
    {
        if (role == Role.Joystick && index == _joyTouch)
        {
            _joyTouch = -1;
            _joyVector = Vector2.Zero;
        }
    }

    private Role HitTest(Vector2 position)
    {
        // Jobs always works: it also closes the board that covers the right side.
        foreach (var (role, center, radius, _) in Buttons)
        {
            if (role == Role.Jobs && position.DistanceTo(center) <= radius * 1.15f)
                return role;
        }
        foreach (var blocker in _blockers)
        {
            if (blocker.IsVisibleInTree() && blocker.GetGlobalRect().HasPoint(position))
                return Role.None;
        }
        foreach (var (role, center, radius, _) in Buttons)
        {
            if (position.DistanceTo(center) <= radius * 1.15f)
                return role;
        }
        return JoystickZone.HasPoint(position) && _joyTouch < 0 ? Role.Joystick : Role.None;
    }

    // --- Output ---------------------------------------------------------------

    private void ApplyMovement()
    {
        var throttle = 0f;
        var steer = 0f;
        var stick = _joyVector;
        if (stick.Length() > DeadZone)
        {
            var player = GameManager.Instance.Player;
            if (SaveManager.Instance.Settings.Joystick == GameSettings.JoystickMode.Steer || player == null)
            {
                steer = stick.X;
                throttle = -stick.Y;
            }
            else
            {
                // Screen and world share the same axes (the camera never rotates).
                var error = Mathf.Wrap(stick.Angle() - player.Rotation, -Mathf.Pi, Mathf.Pi);
                steer = Mathf.Clamp(error * DirectionSteerGain, -1f, 1f);
                // Keep some throttle while turning around: the bike needs speed to steer.
                throttle = stick.Length() * Mathf.Clamp(0.35f + 0.65f * Mathf.Cos(error), 0.3f, 1f);
            }
        }
        if (_touches.ContainsValue(Role.Brake))
            throttle = -1f;

        SetAction("move_up", Mathf.Max(throttle, 0f));
        SetAction("move_down", Mathf.Max(-throttle, 0f));
        SetAction("move_right", Mathf.Max(steer, 0f));
        SetAction("move_left", Mathf.Max(-steer, 0f));
    }

    /// <summary>Presses an action with an analog strength; only releases what touch pressed (not the keyboard).</summary>
    private void SetAction(string action, float strength)
    {
        if (strength > 0.01f)
        {
            Input.ActionPress(action, Mathf.Clamp(strength, 0f, 1f));
            _pressedByTouch.Add(action);
        }
        else if (_pressedByTouch.Remove(action))
        {
            Input.ActionRelease(action);
        }
    }

    /// <summary>Taps go through the event pipeline so _UnhandledInput handlers see IsActionPressed.</summary>
    private static void SendActionTap(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
    }

    private void ReleaseAll()
    {
        _touches.Clear();
        _joyTouch = -1;
        _joyVector = Vector2.Zero;
        foreach (var action in MoveActions)
        {
            if (_pressedByTouch.Remove(action))
                Input.ActionRelease(action);
        }
    }

    private void ApplySettings()
    {
        _active = SaveManager.Instance.Settings.TouchControlsActive;
        Visible = _active;
        if (_keyboardHint != null)
            _keyboardHint.Visible = !_active;
        if (!_active)
            ReleaseAll();
    }

    private void OnPromptChanged(string text) => _interactAvailable = !string.IsNullOrEmpty(text);

    // --- Drawing --------------------------------------------------------------

    public override void _Draw()
    {
        var center = _joyTouch >= 0 ? _joyCenter : RestCenter;
        DrawCircle(center, JoyRadius, BaseColor);
        DrawArc(center, JoyRadius, 0f, Mathf.Tau, 48, RingColor, 3f, true);
        DrawCircle(center + _joyVector * JoyRadius, KnobRadius, KnobColor);

        var font = ThemeDB.FallbackFont;
        foreach (var (role, buttonCenter, radius, label) in Buttons)
        {
            var pressed = _touches.ContainsValue(role);
            var glow = role == Role.Interact && _interactAvailable;
            DrawCircle(buttonCenter, radius, pressed ? PressedColor : glow ? new Color(1f, 0.62f, 0.15f, 0.35f) : ButtonColor);
            DrawArc(buttonCenter, radius, 0f, Mathf.Tau, 40, glow ? KnobColor : RingColor, 3f, true);
            var fontSize = radius > 50f ? 20 : 16;
            var textSize = font.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize);
            DrawString(font, buttonCenter + new Vector2(-textSize.X * 0.5f, fontSize * 0.35f), label,
                HorizontalAlignment.Left, -1, fontSize, Colors.White);
        }
    }
}
