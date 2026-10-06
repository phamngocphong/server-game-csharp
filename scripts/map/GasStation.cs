using Godot;

namespace ShipperSimulator;

/// <summary>
/// Curbside gas station placed by <see cref="CityMap"/>. Drive in and press E to fill the
/// tank (<see cref="GameManager.BuyFuel"/>). Drawn procedurally: a pad, a pump and a "GAS" sign.
/// </summary>
public partial class GasStation : ServiceStop
{
    private static readonly Color PadColor = new(0.85f, 0.85f, 0.82f, 0.9f);
    private static readonly Color PumpColor = new(0.9f, 0.2f, 0.15f);

    protected override Color AccentColor => new(1f, 0.85f, 0.2f);

    protected override string Prompt()
    {
        var price = GameManager.Instance.CityMap?.Region.FuelPrice ?? 0f;
        return $"[E] Refuel  ({GameManager.FormatPrice(price)} / L)";
    }

    protected override void Use() => GameManager.Instance.BuyFuel(StopName);

    public override void _Draw()
    {
        DrawZone();
        DrawRect(new Rect2(-30, -22, 60, 44), PadColor);
        // Pump: body, display and hose.
        DrawRect(new Rect2(-12, -16, 20, 30), PumpColor);
        DrawRect(new Rect2(-8, -12, 12, 8), new Color(0.15f, 0.2f, 0.25f));
        DrawPolyline(new[] { new Vector2(8, -6), new Vector2(18, -2), new Vector2(18, 12) }, new Color(0.1f, 0.1f, 0.1f), 3f);
        DrawSign("GAS");
    }
}
