using Godot;

namespace ShipperSimulator;

/// <summary>
/// Curbside rest stop (a street food stall with a table) placed by <see cref="CityMap"/>.
/// Drive in and press E to eat and rest: costs the city's rest price and clears fatigue
/// quickly (<see cref="GameManager.Rest"/>).
/// </summary>
public partial class RestStop : ServiceStop
{
    private static readonly Color AwningColor = new(0.2f, 0.7f, 0.65f);
    private static readonly Color TableColor = new(0.55f, 0.38f, 0.22f);

    protected override Color AccentColor => new(0.35f, 0.95f, 0.85f);

    protected override string Prompt()
    {
        var price = GameManager.Instance.CityMap?.Region.RestPrice ?? 0;
        return $"[E] Eat & rest  ({GameManager.FormatMoney(price)})";
    }

    protected override void Use() => GameManager.Instance.Rest(StopName);

    public override void _Draw()
    {
        DrawZone();
        // Striped awning.
        for (var i = 0; i < 4; i++)
            DrawRect(new Rect2(-30 + i * 15, -22, 15, 12), i % 2 == 0 ? AwningColor : Colors.White);
        // Table with a bowl and two stools.
        DrawRect(new Rect2(-16, -4, 32, 18), TableColor);
        DrawCircle(new Vector2(0, 5), 5f, Colors.White);
        DrawCircle(new Vector2(-24, 5), 5f, new Color(0.9f, 0.3f, 0.25f));
        DrawCircle(new Vector2(24, 5), 5f, new Color(0.9f, 0.3f, 0.25f));
        DrawSign("REST");
    }
}
