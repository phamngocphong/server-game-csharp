using Godot;

namespace ShipperSimulator;

/// <summary>
/// Title screen. Lists the available cities; Play loads the gameplay scene,
/// where <see cref="Main"/> picks one of them at random.
/// </summary>
public partial class MainMenu : Control
{
    [Export(PropertyHint.File, "*.tscn")] public string GameScenePath { get; set; } = "res://scenes/main.tscn";

    public override void _Ready()
    {
        var playButton = GetNode<Button>("%PlayButton");
        playButton.Pressed += OnPlayPressed;
        GetNode<Button>("%QuitButton").Pressed += OnQuitPressed;

        var cities = CityLoader.LoadAll();
        var citiesLabel = GetNode<Label>("%CitiesLabel");
        if (cities.Count == 0)
        {
            citiesLabel.Text = $"No valid city found in {CityLoader.DefaultFolder}";
            playButton.Disabled = true;
            return;
        }
        citiesLabel.Text = string.Join("  -  ", cities.ConvertAll(c => c.DisplayName));
        playButton.GrabFocus();
    }

    private void OnPlayPressed() => GetTree().ChangeSceneToFile(GameScenePath);

    private void OnQuitPressed() => GetTree().Quit();
}
