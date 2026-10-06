using Godot;

namespace ShipperSimulator;

/// <summary>
/// Player preferences that are not part of a game save (kept when starting a new game).
/// Stored by <see cref="SaveManager"/> in <see cref="SaveManager.SettingsPath"/>.
/// </summary>
public sealed class GameSettings
{
    public enum TouchMode { Auto, On, Off }

    public enum JoystickMode
    {
        /// <summary>Point the stick where you want to go; the bike steers and accelerates towards it.</summary>
        Direction,
        /// <summary>Up = throttle, down = brake/reverse, left/right = steer (like the keyboard).</summary>
        Steer,
    }

    public TouchMode TouchControls { get; set; } = TouchMode.Auto;
    public JoystickMode Joystick { get; set; } = JoystickMode.Direction;

    /// <summary>Auto = on for mobile builds and touchscreen devices.</summary>
    public bool TouchControlsActive => TouchControls switch
    {
        TouchMode.On => true,
        TouchMode.Off => false,
        _ => OS.HasFeature("mobile") || DisplayServer.IsTouchscreenAvailable(),
    };
}
