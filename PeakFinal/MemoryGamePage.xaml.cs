using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Peak;

public partial class MemoryGamePage : ContentPage
{
    readonly Random _rng = new();

    readonly string[] _symbols =
    {
        "🍎","🍎","🐶","🐶","⚽","⚽","🎵","🎵",
        "🚗","🚗","🌙","🌙","⭐","⭐","🍕","🍕"
    };

    List<string> _deck = new();
    Button[] _cards = Array.Empty<Button>();

    int _moves = 0;
    int _matches = 0;

    Button? _first;
    Button? _second;
    bool _busy = false;
    bool _isPaused;
    bool _isGameOver;

    int _seconds = 0;
    IDispatcherTimer? _timer;

    public MemoryGamePage()
    {
        InitializeComponent();
        BuildBoard();
        NewGame();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = GameAudioService.StartGameAtmosphereAsync("memory_match");
    }

    protected override void OnDisappearing()
    {
        _ = GameAudioService.StopGameAtmosphereAsync();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        return true;
    }

    void BuildBoard()
    {
        BoardGrid.Children.Clear();
        _cards = new Button[16];

        int index = 0;
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                var btn = new Button
                {
                    Text = "❓",
                    FontSize = 28,
                    CornerRadius = 16,
                    Padding = 0
                };

                btn.Clicked += OnCardClicked;
                btn.CommandParameter = index;

                Grid.SetRow(btn, r);
                Grid.SetColumn(btn, c);

                BoardGrid.Children.Add(btn);
                _cards[index] = btn;
                index++;
            }
        }
    }

    void OnNewGameClicked(object sender, EventArgs e)
    {
        if (_isPaused)
        {
            return;
        }

        NewGame();
    }

    void NewGame()
    {
        _deck = _symbols.OrderBy(_ => _rng.Next()).ToList();

        _moves = 0;
        _matches = 0;
        _first = null;
        _second = null;
        _busy = false;
        _isGameOver = false;

        StatusLabel.Text = "";

        foreach (var card in _cards)
        {
            card.IsEnabled = true;
            card.Text = "❓";
        }

        _seconds = 0;
        TimeLabel.Text = "Time: 0s";

        _timer ??= Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Start();

        UpdateHud();
    }

    void OnTick(object? sender, EventArgs e)
    {
        if (_isPaused)
        {
            return;
        }

        _seconds++;
        TimeLabel.Text = $"Time: {_seconds}s";
    }

    async void OnCardClicked(object? sender, EventArgs e)
    {
        if (_busy || _isPaused || _isGameOver) return;
        if (sender is not Button btn) return;
        if (!btn.IsEnabled) return;

        int idx = (int)btn.CommandParameter;

        btn.Text = _deck[idx];

        if (_first is null)
        {
            _first = btn;
            return;
        }

        if (ReferenceEquals(btn, _first))
            return;

        _second = btn;
        _busy = true;

        _moves++;
        UpdateHud();

        bool isMatch = _first.Text == _second.Text;

        if (isMatch)
        {
            _first.IsEnabled = false;
            _second.IsEnabled = false;

            _matches++;
            UpdateHud();

            _first = null;
            _second = null;
            _busy = false;

            if (_matches == 8)
            {
                _timer?.Stop();
                _isGameOver = true;

                var moveQuality = Math.Clamp((26 - _moves) / 18.0, 0, 1);
                var timeQuality = Math.Clamp((140 - _seconds) / 110.0, 0, 1);
                var normalized = Math.Clamp((moveQuality * 0.65) + (timeQuality * 0.35), 0, 1);
                var finalScore = (int)Math.Round(normalized * 1000);
                var previousBest = BrainScoreService.GetGamePerformance("memory_match")?.BestScore ?? 0;
                var bestScore = Math.Max(previousBest, finalScore);
                var isNewBest = finalScore > previousBest;

                var apexPoints = BrainScoreService.RecordGameScore(
                    sourceId: "memory_match",
                    skill: BrainSkill.Memory,
                    rawScore: finalScore,
                    expectedTopScore: 1000);

                StatusLabel.Text = $"You won!  Score: {finalScore}  Moves: {_moves}  Time: {_seconds}s";
                await PageTransitionService.PushAsync(
                    Navigation,
                    () => new GenericGameSummaryPage(
                        gameTitle: "Memory Match",
                        score: finalScore,
                        bestScore: bestScore,
                        apexPoints: apexPoints,
                        isNewBest: isNewBest,
                        playAgainFactory: () => new MemoryGamePage(),
                        accentHex: "#8EBBFF"));
            }
            return;
        }

        await Task.Delay(650);

        _first.Text = "❓";
        _second.Text = "❓";

        _first = null;
        _second = null;
        _busy = false;
    }


    void UpdateHud()
    {
        MovesLabel.Text = $"Moves: {_moves}";
        MatchesLabel.Text = $"Matches: {_matches}/8";
    }

    void OnPauseClicked(object sender, TappedEventArgs e)
    {
        OnPauseClicked(sender, (EventArgs)e);
    }

    async void OnPauseClicked(object sender, EventArgs e)
    {
        if (_isPaused)
        {
            return;
        }

        _isPaused = true;
        var action = await GamePauseService.ShowAsync(
            this,
            "Memory Match",
            "Flip pairs, remember where the symbols are, and clear the board in as few moves as possible.");

        if (action == GamePauseAction.Restart)
        {
            _timer?.Stop();
            await GamePauseService.RestartCurrentPageAsync(this);
            return;
        }

        if (action == GamePauseAction.Exit)
        {
            _timer?.Stop();
            await PageTransitionService.PopAsync(Navigation);
            return;
        }

        _isPaused = false;
    }
}
