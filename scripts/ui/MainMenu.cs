using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Title screen. "Continue" resumes the saved game (same city, layout and position) and
/// shows a summary of the save; "New Game" asks for confirmation when a save exists,
/// deletes it and starts fresh in a random city.
/// </summary>
public partial class MainMenu : Control
{
    [Export(PropertyHint.File, "*.tscn")] public string GameScenePath { get; set; } = GameManager.GameScenePath;

    private Button _continueButton = null!;
    private Button _newGameButton = null!;
    private Label _saveLabel = null!;
    private ConfirmationDialog _confirmNewGame = null!;

    public override void _Ready()
    {
        _continueButton = GetNode<Button>("%ContinueButton");
        _newGameButton = GetNode<Button>("%NewGameButton");
        _saveLabel = GetNode<Label>("%SaveLabel");
        _confirmNewGame = GetNode<ConfirmationDialog>("%ConfirmNewGame");
        _continueButton.Pressed += OnContinuePressed;
        _newGameButton.Pressed += OnNewGamePressed;
        _confirmNewGame.Confirmed += StartNewGame;
        GetNode<Button>("%QuitButton").Pressed += OnQuitPressed;

        var cities = CityLoader.LoadAll();
        var citiesLabel = GetNode<Label>("%CitiesLabel");
        if (cities.Count == 0)
        {
            citiesLabel.Text = $"No valid city found in {CityLoader.DefaultFolder}";
            _continueButton.Disabled = true;
            _newGameButton.Disabled = true;
            _saveLabel.Hide();
            return;
        }
        citiesLabel.Text = string.Join("  -  ", cities.ConvertAll(c => c.DisplayName));

        var save = SaveManager.Instance.ReadSave();
        _continueButton.Disabled = save == null;
        _saveLabel.Text = save == null ? "No saved game yet." : DescribeSave(save, cities);
        if (save != null)
        {
            _confirmNewGame.DialogText =
                "Start a new game?\n\nYour saved progress will be deleted:\n" +
                $"{GameManager.FormatMoney(save.Game.Wallet.Balance)}, {save.Game.Stats.TotalDeliveries} deliveries.";
        }
        (save != null ? _continueButton : _newGameButton).GrabFocus();
    }

    private static string DescribeSave(SaveFile save, List<CityRegionData> cities)
    {
        var game = save.Game;
        var city = cities.Find(c => c.RegionId == game.RegionId)?.DisplayName ?? "unknown city";
        var reputation = new Reputation();
        reputation.LoadSaveData(game.Reputation);
        // SavedAt is "YYYY-MM-DDTHH:MM:SS"; show it without the T and the seconds.
        var savedAt = save.SavedAt.Length >= 16 ? save.SavedAt[..16].Replace('T', ' ') : save.SavedAt;
        return $"{city}  -  {GameManager.FormatMoney(game.Wallet.Balance)}  -  " +
            $"{game.Stats.TotalDeliveries} deliveries  -  " +
            $"rating {reputation.Rating.ToString("0.0", CultureInfo.InvariantCulture)}\nSaved {savedAt}";
    }

    private void OnContinuePressed()
    {
        GameManager.Instance.NextStart = GameManager.StartMode.Continue;
        GetTree().ChangeSceneToFile(GameScenePath);
    }

    private void OnNewGamePressed()
    {
        if (SaveManager.Instance.HasSave())
            _confirmNewGame.PopupCentered();
        else
            StartNewGame();
    }

    private void StartNewGame()
    {
        GameManager.Instance.StartNewGame();
        GetTree().ChangeSceneToFile(GameScenePath);
    }

    private void OnQuitPressed() => GetTree().Quit();
}
