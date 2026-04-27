using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;

namespace Peak;

public partial class TurtleTrafficGamePage : ContentPage
{
    class Obstacle
    {
        public BoxView Top { get; init; } = null!;
        public BoxView Bottom { get; init; } = null!;
        public double CenterX { get; set; }
        public double GapCenter { get; init; }
        public double GapSize { get; init; }
        public double TopHeight { get; init; }
        public double BottomHeight { get; init; }
        public bool Scored { get; set; }
    }

    readonly List<Obstacle> _obstacles = new();
    readonly Random _random = new();

    IDispatcherTimer? _loopTimer;
    IDispatcherTimer? _spawnTimer;

    double _areaWidth;
    double _areaHeight;

    double _playerX;
    double _playerY;
    double _velocity;

    int _score;
    int _timeLeftSeconds = 105;

    bool _isRunning;
    bool _isPaused;
    bool _pendingStart;

    const float PlayerSize = 54; // px

    // UI refs
    Label _timerLabel = null!;
    Label _scoreLabel = null!;
    Frame _player = null!;
    AbsoluteLayout _gameArea = null!;
    Grid _countdownOverlay = null!;
    Label _countdownLabel = null!;
    Frame _tapHint = null!;
    Grid _pauseOverlay = null!;
    Grid _gameOverOverlay = null!;
    Label _finalScoreLabel = null!;

    public TurtleTrafficGamePage()
    {
        BuildUi();
        _pendingStart = true;
    }

    protected override bool OnBackButtonPressed()
    {
        return true;
    }

    void BuildUi()
    {
        Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Color.FromArgb("#0D73F2"), 0),
                new GradientStop(Color.FromArgb("#063D8C"), 1)
            }
        };

        var pauseGlyph = new Grid
        {
            Padding = new Thickness(12, 10),
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 5
        };
        pauseGlyph.Children.Add(new BoxView
        {
            BackgroundColor = Color.FromArgb("#5D1839"),
            CornerRadius = 2.5f,
            WidthRequest = 4,
            HeightRequest = 16,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        });
        var rightBar = new BoxView
        {
            BackgroundColor = Color.FromArgb("#5D1839"),
            CornerRadius = 2.5f,
            WidthRequest = 4,
            HeightRequest = 16,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        pauseGlyph.Children.Add(rightBar);
        Grid.SetColumn(rightBar, 1);
        pauseGlyph.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => OnPauseTapped()) });

        _timerLabel = new Label
        {
            Text = "01:45",
            FontSize = 16,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center
        };

        _scoreLabel = new Label
        {
            Text = "0",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        };

        _gameArea = new AbsoluteLayout
        {
            BackgroundColor = Colors.Transparent
        };
        _gameArea.SizeChanged += OnGameAreaSizeChanged;

        // Water layers
                var waterTop = new BoxView
        {
            Color = Color.FromArgb("#1A7BE8"),
            Opacity = 0.45
        };
        _gameArea.Children.Add(waterTop);
        AbsoluteLayout.SetLayoutBounds(waterTop, new Rect(0, 0, 1, 0.28));
        AbsoluteLayout.SetLayoutFlags(waterTop, AbsoluteLayoutFlags.All);

        var waterBottom = new BoxView
        {
            Color = Color.FromArgb("#0C3C86"),
            Opacity = 0.65
        };
        _gameArea.Children.Add(waterBottom);
        AbsoluteLayout.SetLayoutBounds(waterBottom, new Rect(0, 1, 1, 0.30));
        AbsoluteLayout.SetLayoutFlags(waterBottom, AbsoluteLayoutFlags.All);

        _tapHint = new Frame
        {
            CornerRadius = 20,
            BackgroundColor = Color.FromArgb("#FFFFFF20"),
            HasShadow = false,
            Padding = new Thickness(10, 6),
            IsVisible = true,
            Content = new HorizontalStackLayout
            {
                Spacing = 8,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = "Tap", FontSize = 16, TextColor = Colors.White },
                    new Label { Text = "??", FontSize = 18, VerticalOptions = LayoutOptions.Center }
                }
            }
        };
        _gameArea.Children.Add(_tapHint);
        AbsoluteLayout.SetLayoutBounds(_tapHint, new Rect(0.5, 0.82, 120, 40));
        AbsoluteLayout.SetLayoutFlags(_tapHint, AbsoluteLayoutFlags.PositionProportional);

        _player = new Frame
        {
            WidthRequest = PlayerSize,
            HeightRequest = PlayerSize,
            CornerRadius = PlayerSize / 2,
            BackgroundColor = Color.FromArgb("#7EE081"),
            HasShadow = false,
            Padding = 0,
            Content = new Label { Text = "??", FontSize = 30, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
        };
        _gameArea.Children.Add(_player);

        _gameArea.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => OnGameAreaTapped()) });

        _countdownLabel = new Label
        {
            Text = "3",
            FontSize = 46,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center
        };
        _countdownOverlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#CC0A2E6C"),
            IsVisible = false,
            RowSpacing = 0,
            ColumnSpacing = 0,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 12,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label { Text = "GET READY", FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center },
                        _countdownLabel
                    }
                }
            }
        };

        _pauseOverlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#99000000"),
            IsVisible = false,
            Children =
            {
                new Frame
                {
                    CornerRadius = 20,
                    BackgroundColor = Colors.White,
                    HasShadow = false,
                    Padding = 20,
                    WidthRequest = 260,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Content = new VerticalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            new Label { Text = "Paused", FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Colors.Black, HorizontalTextAlignment = TextAlignment.Center },
                            new Button { Text = "Resume", BackgroundColor = Color.FromArgb("#0D73F2"), TextColor = Colors.White, CornerRadius = 20, HeightRequest = 50, Command = new Command(() => OnResumeClicked()) },
                            new Button { Text = "Quit", BackgroundColor = Color.FromArgb("#E11D48"), TextColor = Colors.White, CornerRadius = 20, HeightRequest = 50, Command = new Command(async () => await OnQuitClicked()) }
                        }
                    }
                }
            }
        };

        _finalScoreLabel = new Label { Text = "Score: 0", FontSize = 18, TextColor = Color.FromArgb("#444444"), HorizontalTextAlignment = TextAlignment.Center };
        _gameOverOverlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#99000000"),
            IsVisible = false,
            Children =
            {
                new Frame
                {
                    CornerRadius = 24,
                    BackgroundColor = Colors.White,
                    HasShadow = false,
                    Padding = 24,
                    WidthRequest = 280,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Content = new VerticalStackLayout
                    {
                        Spacing = 16,
                        Children =
                        {
                            new Label { Text = "Game Over", FontSize = 28, FontAttributes = FontAttributes.Bold, TextColor = Colors.Black, HorizontalTextAlignment = TextAlignment.Center },
                            _finalScoreLabel,
                            new Button { Text = "PLAY AGAIN", BackgroundColor = Color.FromArgb("#0D73F2"), TextColor = Colors.White, CornerRadius = 24, HeightRequest = 54, Command = new Command(() => OnPlayAgainClicked()) }
                        }
                    }
                }
            }
        };

                var hud = new Grid
        {
            Padding = new Thickness(18, 18, 18, 10),
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 14
        };

        var pauseFrame = new Frame
        {
            WidthRequest = 42,
            HeightRequest = 42,
            CornerRadius = 16,
            BackgroundColor = Color.FromArgb("#FFF8FB"),
            Padding = 0,
            HasShadow = true,
            HorizontalOptions = LayoutOptions.Start,
            Content = pauseGlyph
        };
        hud.Children.Add(pauseFrame);
        Grid.SetColumn(pauseFrame, 0);

        var titleStack = new VerticalStackLayout
        {
            Spacing = 2,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "Turtle Traffic", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center },
                _timerLabel
            }
        };
        hud.Children.Add(titleStack);
        Grid.SetColumn(titleStack, 1);

        var scoreGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 4,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };
        scoreGrid.Children.Add(_scoreLabel);
        var ptsLabel = new Label { Text = "pts", FontSize = 14, TextColor = Color.FromArgb("#DDE8FF"), VerticalTextAlignment = TextAlignment.End };
        scoreGrid.Children.Add(ptsLabel);
        Grid.SetColumn(ptsLabel, 1);

        hud.Children.Add(scoreGrid);
        Grid.SetColumn(scoreGrid, 2);
var gameFrame = new Frame
        {
            CornerRadius = 26,
            BackgroundColor = Color.FromArgb("#124FAF"),
            HasShadow = false,
            Padding = 0,
            Content = _gameArea
        };

        var root = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };

        root.Add(hud, 0, 0);
        root.Add(new Grid { Padding = new Thickness(18, 0, 18, 20), Children = { gameFrame } }, 0, 1);
        root.Add(_countdownOverlay);
        root.Add(_pauseOverlay);
        root.Add(_gameOverOverlay);

        Content = root;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = GameAudioService.StartGameAtmosphereAsync("turtle_traffic");
        _pendingStart = true;
        TryStartWhenReady();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopGame();
        _ = GameAudioService.StopGameAtmosphereAsync();
    }

    void OnGameAreaSizeChanged(object? sender, EventArgs e)
    {
        _areaWidth = _gameArea.Width;
        _areaHeight = _gameArea.Height;
        TryStartWhenReady();
    }

    void TryStartWhenReady()
    {
        if (!_pendingStart)
            return;

        if (_areaWidth <= 0 || _areaHeight <= 0)
            return;

        _pendingStart = false;
        _playerX = _areaWidth * 0.18;
        StartGame();
    }

    async void StartGame()
    {
        StopGame();

        _score = 0;
        _timeLeftSeconds = 105; // 1:45
        _velocity = 0;
        _playerY = _areaHeight * 0.5;
        _isPaused = false;
        _isRunning = false;

        _scoreLabel.Text = "0";
        _timerLabel.Text = FormatTime(_timeLeftSeconds);
        _finalScoreLabel.Text = "Score: 0";
        _gameOverOverlay.IsVisible = false;
        _pauseOverlay.IsVisible = false;
        _tapHint.IsVisible = true;

        foreach (var obs in _obstacles)
        {
            _gameArea.Children.Remove(obs.Top);
            _gameArea.Children.Remove(obs.Bottom);
        }
        _obstacles.Clear();

        UpdatePlayerPosition();

        await RunCountdownAsync();
        BeginLoops();
    }

    async Task RunCountdownAsync()
    {
        _countdownOverlay.IsVisible = true;
        _countdownLabel.Scale = 1.0;

        for (int i = 3; i >= 1; i--)
        {
            _countdownLabel.Text = i.ToString();
            await _countdownLabel.ScaleTo(1.3, 180, Easing.CubicOut);
            await _countdownLabel.ScaleTo(1.0, 120, Easing.CubicIn);
            await Task.Delay(140);
        }

        _countdownOverlay.IsVisible = false;
    }

    void BeginLoops()
    {
        _isRunning = true;

        _loopTimer = Dispatcher.CreateTimer();
        _loopTimer.Interval = TimeSpan.FromMilliseconds(16);
        _loopTimer.Tick += OnLoopTick;
        _loopTimer.Start();

        _spawnTimer = Dispatcher.CreateTimer();
        _spawnTimer.Interval = TimeSpan.FromMilliseconds(1400);
        _spawnTimer.Tick += (_, _) =>
        {
            if (_isRunning && !_isPaused)
                SpawnObstacle();
        };
        _spawnTimer.Start();

        Device.StartTimer(TimeSpan.FromSeconds(1), () =>
        {
            if (!_isRunning)
                return false;

            if (_isPaused)
                return true;

            _timeLeftSeconds--;
            _timerLabel.Text = FormatTime(_timeLeftSeconds);

            if (_timeLeftSeconds <= 0)
            {
                EndGame();
                return false;
            }

            return true;
        });
    }

    void OnLoopTick(object? sender, EventArgs e)
    {
        if (!_isRunning || _isPaused)
            return;

        StepPlayer();
        MoveObstacles();
        CheckCollisions();
    }

    void StepPlayer()
    {
        const double gravity = 0.18; // px per tick^2
        _velocity += gravity;
        _playerY += _velocity;

        double minY = (PlayerSize / 2) + 6;
        double maxY = _areaHeight - (PlayerSize / 2) - 6;

        if (_playerY < minY)
        {
            _playerY = minY;
            _velocity = 0;
        }
        else if (_playerY > maxY)
        {
            _playerY = maxY;
            _velocity = 0;
        }

        UpdatePlayerPosition();
    }

    void UpdatePlayerPosition()
    {
        double x = _playerX - (PlayerSize / 2);
        double y = _playerY - (PlayerSize / 2);
        AbsoluteLayout.SetLayoutBounds(_player, new Rect(x, y, PlayerSize, PlayerSize));
        AbsoluteLayout.SetLayoutFlags(_player, AbsoluteLayoutFlags.None);
    }

    void SpawnObstacle()
    {
        if (_areaWidth <= 0 || _areaHeight <= 0)
            return;

        double gapSize = Math.Max(120, _areaHeight * 0.24);
        double minGapCenter = gapSize / 2 + 30;
        double maxGapCenter = _areaHeight - gapSize / 2 - 30;
        double gapCenter = _random.NextDouble() * (maxGapCenter - minGapCenter) + minGapCenter;

        double topHeight = gapCenter - (gapSize / 2);
        double bottomHeight = _areaHeight - (gapCenter + gapSize / 2);

        var top = new BoxView
        {
            Color = Color.FromArgb("#FF5A6E"),
            WidthRequest = 70,
            HeightRequest = topHeight
        };

        var bottom = new BoxView
        {
            Color = Color.FromArgb("#FDB11D"),
            WidthRequest = 70,
            HeightRequest = bottomHeight
        };

        double startCenterX = _areaWidth + 90;

        var obstacle = new Obstacle
        {
            Top = top,
            Bottom = bottom,
            CenterX = startCenterX,
            GapCenter = gapCenter,
            GapSize = gapSize,
            TopHeight = topHeight,
            BottomHeight = bottomHeight,
            Scored = false
        };

        _gameArea.Children.Add(top);
        _gameArea.Children.Add(bottom);
        LayoutObstacle(obstacle);

        _obstacles.Add(obstacle);
    }

    void LayoutObstacle(Obstacle obs)
    {
        double xLeft = obs.CenterX - (obs.Top.WidthRequest / 2);
        double bottomY = obs.GapCenter + (obs.GapSize / 2);

        AbsoluteLayout.SetLayoutBounds(obs.Top, new Rect(xLeft, 0, obs.Top.WidthRequest, obs.TopHeight));
        AbsoluteLayout.SetLayoutFlags(obs.Top, AbsoluteLayoutFlags.None);

        AbsoluteLayout.SetLayoutBounds(obs.Bottom, new Rect(xLeft, bottomY, obs.Bottom.WidthRequest, obs.BottomHeight));
        AbsoluteLayout.SetLayoutFlags(obs.Bottom, AbsoluteLayoutFlags.None);
    }

    void MoveObstacles()
    {
        const double speed = 5.2; // px per tick
        var toRemove = new List<Obstacle>();

        foreach (var obs in _obstacles)
        {
            obs.CenterX -= speed;
            LayoutObstacle(obs);

            if (!obs.Scored && obs.CenterX < _playerX)
            {
                obs.Scored = true;
                _score += 50;
                _scoreLabel.Text = _score.ToString();
            }

            if (obs.CenterX < -100)
                toRemove.Add(obs);
        }

        foreach (var obs in toRemove)
        {
            _obstacles.Remove(obs);
            _gameArea.Children.Remove(obs.Top);
            _gameArea.Children.Remove(obs.Bottom);
        }
    }

    void CheckCollisions()
    {
        double playerLeft = _playerX - (PlayerSize / 2);
        double playerTop = _playerY - (PlayerSize / 2);
        var playerRect = new Rect(playerLeft, playerTop, PlayerSize, PlayerSize);

        foreach (var obs in _obstacles)
        {
            double xLeft = obs.CenterX - (obs.Top.WidthRequest / 2);
            double bottomY = obs.GapCenter + (obs.GapSize / 2);

            var topRect = new Rect(xLeft, 0, obs.Top.WidthRequest, obs.TopHeight);
            var bottomRect = new Rect(xLeft, bottomY, obs.Bottom.WidthRequest, obs.BottomHeight);

            if (playerRect.IntersectsWith(topRect) || playerRect.IntersectsWith(bottomRect))
            {
                EndGame();
                return;
            }
        }
    }

    void StopGame()
    {
        _isRunning = false;

        if (_loopTimer != null)
        {
            _loopTimer.Stop();
            _loopTimer.Tick -= OnLoopTick;
            _loopTimer = null;
        }

        if (_spawnTimer != null)
        {
            _spawnTimer.Stop();
            _spawnTimer = null;
        }
    }

    void EndGame()
    {
        if (!_isRunning)
            return;

        StopGame();
        int previousBest = BrainScoreService.GetGamePerformance("turtle_traffic")?.BestScore ?? 0;
        int bestScore = Math.Max(previousBest, _score);
        int apexPoints = BrainScoreService.RecordGameScore(
            sourceId: "turtle_traffic",
            skill: BrainSkill.Focus,
            rawScore: _score,
            expectedTopScore: 1500);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await PageTransitionService.PushAsync(
                Navigation,
                () => new GenericGameSummaryPage(
                    gameTitle: "Turtle Traffic",
                    score: _score,
                    bestScore: bestScore,
                    apexPoints: apexPoints,
                    isNewBest: _score > previousBest,
                    playAgainFactory: () => new TurtleTrafficGamePage(),
                    accentHex: "#0D73F2"));
        });
    }

    void OnPlayAgainClicked(object? sender = null, EventArgs? e = null) => StartGame();

    async void OnPauseTapped(object? sender = null, TappedEventArgs? e = null)
    {
        if (!_isRunning)
            return;

        _isPaused = true;

        var action = await GamePauseService.ShowAsync(
            this,
            "Turtle Traffic",
            "Guide the turtle through gaps, time your taps carefully, and avoid collisions.");

        if (action == GamePauseAction.Restart)
        {
            StopGame();
            await GamePauseService.RestartCurrentPageAsync(this);
            return;
        }

        if (action == GamePauseAction.Exit)
        {
            await OnQuitClicked();
            return;
        }

        _isPaused = false;
    }

    void OnResumeClicked(object? sender = null, EventArgs? e = null)
    {
        _isPaused = false;
        _pauseOverlay.IsVisible = false;
    }

    async Task OnQuitClicked()
    {
        StopGame();
        if (Navigation?.NavigationStack?.Any() == true)
            await PageTransitionService.PopAsync(Navigation);
    }

    async void OnQuitClicked(object? sender, EventArgs e)
    {
        await OnQuitClicked();
    }

    void OnGameAreaTapped(object? sender = null, TappedEventArgs? e = null)
    {
        if (!_isRunning || _isPaused)
            return;

        _tapHint.IsVisible = false;
        _velocity = -4.8; // upward impulse
    }

    static string FormatTime(int totalSeconds)
    {
        if (totalSeconds < 0)
            totalSeconds = 0;

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }
}











