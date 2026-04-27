using RoundRectangle = Microsoft.Maui.Controls.Shapes.RoundRectangle;

namespace Peak;

public sealed record ArcadeRound(
    string Prompt,
    IReadOnlyList<string> Options,
    int CorrectIndex,
    string HelperText);

public sealed record ArcadeChallengeConfig(
    string SourceId,
    string Title,
    string Subtitle,
    BrainSkill Skill,
    string AccentColor,
    string AccentDeepColor,
    string HelperCaption,
    IReadOnlyList<ArcadeRound> Rounds,
    int SecondsPerRound = 14);

public sealed class ArcadeChallengeGamePage : ContentPage
{
    readonly ArcadeChallengeConfig _config;
    readonly AbsoluteLayout _rootLayout;
    readonly Label _titleLabel;
    readonly Label _subtitleLabel;
    readonly Label _timerLabel;
    readonly Label _scoreLabel;
    readonly Label _roundLabel;
    readonly Label _promptLabel;
    readonly Label _helperLabel;
    readonly Button[] _optionButtons;
    readonly IDispatcherTimer _timer;

    int _roundIndex;
    int _score;
    int _secondsLeft;
    bool _acceptingInput = true;
    bool _isFinishing;

    public ArcadeChallengeGamePage(ArcadeChallengeConfig config)
    {
        _config = config;

        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);
        NavigationPage.SetHasBackButton(this, false);
        BackgroundColor = Color.FromArgb("#F4F7FC");

        _titleLabel = new Label
        {
            Text = config.Title,
            FontAttributes = FontAttributes.Bold,
            FontSize = 26,
            TextColor = Colors.White
        };

        _subtitleLabel = new Label
        {
            Text = config.Subtitle,
            FontSize = 14,
            TextColor = Color.FromArgb("#E9F6FF")
        };

        _timerLabel = new Label
        {
            Text = "14s",
            FontAttributes = FontAttributes.Bold,
            FontSize = 24,
            TextColor = Color.FromArgb("#12314D"),
            HorizontalTextAlignment = TextAlignment.Center
        };

        _scoreLabel = new Label
        {
            Text = "0 pts",
            FontAttributes = FontAttributes.Bold,
            FontSize = 18,
            TextColor = Color.FromArgb("#12314D"),
            HorizontalTextAlignment = TextAlignment.Center
        };

        _roundLabel = new Label
        {
            Text = "Round 1",
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            TextColor = Color.FromArgb("#6B7D92")
        };

        _promptLabel = new Label
        {
            FontAttributes = FontAttributes.Bold,
            FontSize = 24,
            LineBreakMode = LineBreakMode.WordWrap,
            TextColor = Color.FromArgb("#13263F")
        };

        _helperLabel = new Label
        {
            FontSize = 15,
            LineBreakMode = LineBreakMode.WordWrap,
            TextColor = Color.FromArgb("#5C6E84")
        };

        _optionButtons = Enumerable.Range(0, 4)
            .Select(CreateOptionButton)
            .ToArray();

        _rootLayout = new AbsoluteLayout();
        _rootLayout.Children.Add(BuildContent());
        Content = _rootLayout;

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += OnTimerTick;

        LoadRound();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await GameAudioService.StartGameAtmosphereAsync(_config.SourceId);
        StartRoundTimer();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        _timer.Stop();
        await GameAudioService.StopGameAtmosphereAsync();
    }

    protected override bool OnBackButtonPressed() => true;

    View BuildContent()
    {
        var hero = new Border
        {
            Padding = new Thickness(20, 22, 20, 24),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 28 }
        };

        hero.Background = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromArgb(_config.AccentColor), 0f),
                new(Color.FromArgb(_config.AccentDeepColor), 1f)
            },
            new Point(0, 0),
            new Point(1, 1));

        var pauseGlyph = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Auto),
                new(GridLength.Auto)
            },
            ColumnSpacing = 5,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var leftPauseBar = new BoxView
        {
            WidthRequest = 5,
            HeightRequest = 18,
            Color = Color.FromArgb("#191B28"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var rightPauseBar = new BoxView
        {
            WidthRequest = 5,
            HeightRequest = 18,
            Color = Color.FromArgb("#191B28"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        Grid.SetColumn(rightPauseBar, 1);
        pauseGlyph.Children.Add(leftPauseBar);
        pauseGlyph.Children.Add(rightPauseBar);

        var pauseButton = new Border
        {
            WidthRequest = 46,
            HeightRequest = 46,
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = pauseGlyph
        };
        pauseButton.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await PauseAsync()) });

        var heroGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Auto),
                new(GridLength.Star),
                new(GridLength.Auto)
            },
            ColumnSpacing = 14
        };

        heroGrid.Children.Add(pauseButton);

        var titleStack = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children = { _titleLabel, _subtitleLabel }
        };
        Grid.SetColumn(titleStack, 1);
        heroGrid.Children.Add(titleStack);

        var infoStack = new VerticalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Border
                {
                    Padding = new Thickness(14, 8),
                    BackgroundColor = Colors.White,
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 18 },
                    Content = _timerLabel
                },
                new Border
                {
                    Padding = new Thickness(12, 8),
                    BackgroundColor = Color.FromArgb("#22FFFFFF"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 16 },
                    Content = _scoreLabel
                }
            }
        };
        Grid.SetColumn(infoStack, 2);
        heroGrid.Children.Add(infoStack);

        hero.Content = heroGrid;

        var optionsGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Star),
                new(GridLength.Star)
            },
            RowDefinitions = new RowDefinitionCollection
            {
                new(GridLength.Auto),
                new(GridLength.Auto)
            },
            ColumnSpacing = 12,
            RowSpacing = 12
        };
        Grid.SetRow(_optionButtons[0], 0);
        Grid.SetColumn(_optionButtons[0], 0);
        Grid.SetRow(_optionButtons[1], 0);
        Grid.SetColumn(_optionButtons[1], 1);
        Grid.SetRow(_optionButtons[2], 1);
        Grid.SetColumn(_optionButtons[2], 0);
        Grid.SetRow(_optionButtons[3], 1);
        Grid.SetColumn(_optionButtons[3], 1);
        optionsGrid.Children.Add(_optionButtons[0]);
        optionsGrid.Children.Add(_optionButtons[1]);
        optionsGrid.Children.Add(_optionButtons[2]);
        optionsGrid.Children.Add(_optionButtons[3]);

        var bodyCard = new Border
        {
            Padding = 20,
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 26 },
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Color.FromArgb("#180F172A")),
                Offset = new Point(0, 10),
                Radius = 20,
                Opacity = 0.16f
            },
            Content = new VerticalStackLayout
            {
                Spacing = 18,
                Children =
                {
                    _roundLabel,
                    _promptLabel,
                    new Border
                    {
                        Padding = 14,
                        BackgroundColor = Color.FromArgb("#F6F9FE"),
                        StrokeThickness = 0,
                        StrokeShape = new RoundRectangle { CornerRadius = 18 },
                        Content = _helperLabel
                    },
                    optionsGrid
                }
            }
        };

        var page = new Grid
        {
            Padding = new Thickness(18, 18, 18, 26),
            RowDefinitions = new RowDefinitionCollection
            {
                new(GridLength.Auto),
                new(GridLength.Star)
            },
            RowSpacing = 16
        };

        page.Children.Add(hero);
        Grid.SetRow(bodyCard, 1);
        page.Children.Add(bodyCard);
        return page;
    }

    Button CreateOptionButton(int index)
    {
        var button = new Button
        {
            CornerRadius = 22,
            Padding = new Thickness(16, 18),
            BackgroundColor = Color.FromArgb("#F8FBFF"),
            BorderWidth = 1,
            BorderColor = Color.FromArgb("#DCE7F5"),
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#14314C"),
            LineBreakMode = LineBreakMode.WordWrap,
            CommandParameter = index
        };

        button.Clicked += OnOptionClicked;
        return button;
    }

    void LoadRound()
    {
        var round = _config.Rounds[_roundIndex];
        _roundLabel.Text = $"Round {_roundIndex + 1} of {_config.Rounds.Count}";
        _promptLabel.Text = round.Prompt;
        _helperLabel.Text = string.IsNullOrWhiteSpace(round.HelperText) ? _config.HelperCaption : round.HelperText;

        for (var i = 0; i < _optionButtons.Length; i++)
        {
            var button = _optionButtons[i];
            button.IsEnabled = true;
            button.BackgroundColor = Color.FromArgb("#F8FBFF");
            button.BorderColor = Color.FromArgb("#DCE7F5");
            button.TextColor = Color.FromArgb("#14314C");
            button.Text = i < round.Options.Count ? round.Options[i] : string.Empty;
            button.IsVisible = i < round.Options.Count;
        }

        _acceptingInput = true;
        _secondsLeft = _config.SecondsPerRound;
        UpdateHud();
    }

    void StartRoundTimer()
    {
        _timer.Stop();
        _timer.Start();
    }

    void UpdateHud()
    {
        _timerLabel.Text = $"{_secondsLeft:00}s";
        _scoreLabel.Text = $"{_score} pts";
    }

    async void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_acceptingInput)
        {
            return;
        }

        _secondsLeft--;
        UpdateHud();

        if (_secondsLeft <= 0)
        {
            await ResolveRoundAsync(-1);
        }
    }

    async void OnOptionClicked(object? sender, EventArgs e)
    {
        if (!_acceptingInput || sender is not Button button)
        {
            return;
        }

        await GameAudioService.PlayTapAsync();
        var chosen = button.CommandParameter is int value ? value : -1;
        await ResolveRoundAsync(chosen);
    }

    async Task ResolveRoundAsync(int chosenIndex)
    {
        if (!_acceptingInput)
        {
            return;
        }

        _acceptingInput = false;
        _timer.Stop();

        var round = _config.Rounds[_roundIndex];
        for (var i = 0; i < _optionButtons.Length; i++)
        {
            var button = _optionButtons[i];
            button.IsEnabled = false;

            if (i == round.CorrectIndex)
            {
                button.BackgroundColor = Color.FromArgb("#DBF6E4");
                button.BorderColor = Color.FromArgb("#2DB55D");
                button.TextColor = Color.FromArgb("#166534");
            }
            else if (i == chosenIndex)
            {
                button.BackgroundColor = Color.FromArgb("#FFE7EA");
                button.BorderColor = Color.FromArgb("#F0526D");
                button.TextColor = Color.FromArgb("#BE123C");
            }
        }

        if (chosenIndex == round.CorrectIndex)
        {
            _score += 100 + (_secondsLeft * 5);
        }

        UpdateHud();
        await Task.Delay(720);

        if (_roundIndex >= _config.Rounds.Count - 1)
        {
            await FinishAsync();
            return;
        }

        _roundIndex++;
        LoadRound();
        StartRoundTimer();
    }

    async Task PauseAsync()
    {
        _timer.Stop();
        var action = await GamePauseService.ShowAsync(this, _config.Title, _config.HelperCaption);

        switch (action)
        {
            case GamePauseAction.Resume:
                if (!_isFinishing)
                {
                    _timer.Start();
                }
                break;
            case GamePauseAction.Restart:
                _roundIndex = 0;
                _score = 0;
                LoadRound();
                StartRoundTimer();
                break;
            case GamePauseAction.Exit:
                await GameHubNavigationService.ReturnToNearestHubAsync(Navigation);
                break;
        }
    }

    async Task FinishAsync()
    {
        if (_isFinishing)
        {
            return;
        }

        _isFinishing = true;
        var previousBest = BrainScoreService.GetGamePerformance(_config.SourceId)?.BestScore ?? 0;
        var bestScore = Math.Max(previousBest, _score);
        var isNewBest = _score > previousBest;
        var apexPoints = BrainScoreService.RecordGameScore(_config.SourceId, _config.Skill, _score, _config.Rounds.Count * 170);

        await PageTransitionService.PushAsync(
            Navigation,
            () => new GenericGameSummaryPage(
                gameTitle: _config.Title,
                score: _score,
                bestScore: bestScore,
                apexPoints: apexPoints,
                isNewBest: isNewBest,
                playAgainFactory: () => new ArcadeChallengeGamePage(_config),
                accentHex: _config.AccentColor));
    }
}

public static class ArcadeChallengeFactory
{
    public static ContentPage Create(string title)
    {
        return title switch
        {
            "Baggage Claim" => new ArcadeChallengeGamePage(new ArcadeChallengeConfig(
                "baggage_claim",
                "Baggage Claim",
                "Memory",
                BrainSkill.Memory,
                "#F7B038",
                "#E18A00",
                "Find the luggage tag that best fits the announced claim.",
                new[]
                {
                    new ArcadeRound("Flight A12 just landed. Which tag belongs to gate A12?", new[] { "A12 - Green Stripe", "B14 - Red Dot", "C09 - Blue Wave", "D18 - Gold Zip" }, 0, "Use the flight code to claim the right bag quickly."),
                    new ArcadeRound("The carousel is now serving family luggage for gate C07.", new[] { "C07 - Twin Stickers", "B07 - Twin Stickers", "C17 - Yellow Handle", "A07 - Grey Strap" }, 0, "Tiny differences matter under pressure."),
                    new ArcadeRound("Pick the luggage label that matches a premium red route to gate D04.", new[] { "D14 - Red Priority", "D04 - Red Priority", "D04 - Blue Priority", "A04 - Red Priority" }, 1, "Match both the gate and the tag style."),
                    new ArcadeRound("A fragile bag for gate B22 is missing. Which one is yours?", new[] { "B22 - Fragile / White", "B12 - Fragile / White", "B22 - Standard / White", "B22 - Fragile / Black" }, 0, "Gate + handling symbol + color should all line up."),
                    new ArcadeRound("Which tag belongs to transfer flight E03 with a teal strap?", new[] { "E30 - Teal", "E03 - Teal", "E03 - Orange", "F03 - Teal" }, 1, "Stay calm and scan left to right."),
                    new ArcadeRound("Airport staff announce carousel F09 for silver express luggage. Choose it.", new[] { "F09 - Silver Express", "F90 - Silver Express", "F09 - Grey Basic", "G09 - Silver Express" }, 0, "The best choice is the exact match."),
                    new ArcadeRound("The final call is for gate C15 with a bold yellow badge.", new[] { "C15 - Yellow Badge", "C15 - White Badge", "D15 - Yellow Badge", "C51 - Yellow Badge" }, 0, "The closer the details, the trickier the claim."),
                    new ArcadeRound("A carry-on from gate A08 has a navy zipper. Which is it?", new[] { "A08 - Navy Zipper", "A08 - Black Zipper", "B08 - Navy Zipper", "A80 - Navy Zipper" }, 0, "Exact visual recall wins the round.")
                })),
            "Low Pop" => new ArcadeChallengeGamePage(new ArcadeChallengeConfig(
                "low_pop",
                "Low Pop",
                "Problem Solving",
                BrainSkill.ProblemSolving,
                "#67E66F",
                "#1E9F48",
                "Tap the lowest value before the bubbles burst.",
                new[]
                {
                    new ArcadeRound("Pick the lowest value.", new[] { "12", "9", "15", "18" }, 1, "The smallest number always wins."),
                    new ArcadeRound("Which result is the lowest?", new[] { "7 + 3", "4 + 2", "9 - 1", "5 + 4" }, 1, "Solve quickly and compare."),
                    new ArcadeRound("Choose the smallest total.", new[] { "14", "11", "13", "10" }, 3, "Keep an eye on the lowest edge value."),
                    new ArcadeRound("Find the lowest answer.", new[] { "3 x 4", "18 - 7", "5 + 6", "20 - 10" }, 3, "A fast subtraction can beat multiplication."),
                    new ArcadeRound("Which bubble should you pop?", new[] { "16", "8", "12", "9" }, 1, "The safest play is the minimum."),
                    new ArcadeRound("Tap the lowest result.", new[] { "6 + 8", "15 - 4", "7 + 3", "5 + 7" }, 2, "Do the lightest math first."),
                    new ArcadeRound("Pick the smallest number.", new[] { "22", "17", "19", "21" }, 1, "Numbers can look close; compare carefully."),
                    new ArcadeRound("Choose the lowest expression.", new[] { "30 / 3", "14 - 4", "3 + 8", "2 x 6" }, 0, "Division can drop lower than you expect.")
                })),
            "Face Switch" => new ArcadeChallengeGamePage(new ArcadeChallengeConfig(
                "face_switch",
                "Face Switch",
                "Mental Agility",
                BrainSkill.MentalAgility,
                "#44A7FF",
                "#197BDB",
                "The rule changes often. Find the face that matches the current rule.",
                new[]
                {
                    new ArcadeRound("Rule: tap the face with the blue hat.", new[] { "🙂 Red Hat", "🙂 Blue Hat", "😎 Blue Glasses", "😀 Green Hat" }, 1, "Only the hat color counts."),
                    new ArcadeRound("Rule switch: tap the face wearing glasses.", new[] { "🙂 Blue Hat", "😎 No Hat", "😀 Green Hat", "🙂 Yellow Hat" }, 1, "Ignore the hat now."),
                    new ArcadeRound("Rule switch: choose the only smiling face.", new[] { "😐 Blue Hat", "🙂 Red Hat", "😐 Glasses", "😐 Green Hat" }, 1, "Stay flexible when the sorting rule changes."),
                    new ArcadeRound("Rule: tap the face with no hat and no glasses.", new[] { "🙂 No Hat", "😎 No Hat", "🙂 Blue Hat", "😐 Glasses" }, 0, "Two traits can matter at once."),
                    new ArcadeRound("Rule switch: find the face with glasses and a smile.", new[] { "🙂 Blue Hat", "😎 Smile", "😎 Neutral", "🙂 No Hat" }, 1, "Update the filter instantly."),
                    new ArcadeRound("Rule: choose the neutral face.", new[] { "🙂 Green Hat", "😐 No Hat", "😎 Smile", "🙂 Red Hat" }, 1, "Expression beats accessories here."),
                    new ArcadeRound("Rule switch: choose the yellow hat.", new[] { "😐 Yellow Hat", "🙂 Green Hat", "😎 No Hat", "🙂 Red Hat" }, 0, "Reset and hunt the new visual cue."),
                    new ArcadeRound("Rule: choose the face with blue glasses.", new[] { "😎 Blue Glasses", "😎 Dark Glasses", "🙂 Blue Hat", "😀 Green Hat" }, 0, "One last fast filter.")
                })),
            "Speed Spotting" => new ArcadeChallengeGamePage(new ArcadeChallengeConfig(
                "speed_spotting",
                "Speed Spotting",
                "Mental Agility",
                BrainSkill.MentalAgility,
                "#44A7FF",
                "#197BDB",
                "Find the exact target as fast as you can.",
                new[]
                {
                    new ArcadeRound("Target: blue triangle", new[] { "Blue Triangle", "Blue Circle", "Red Triangle", "Green Triangle" }, 0, "Match both color and shape."),
                    new ArcadeRound("Target: yellow square", new[] { "Orange Square", "Yellow Square", "Yellow Circle", "Blue Square" }, 1, "One mismatch means it is wrong."),
                    new ArcadeRound("Target: red diamond", new[] { "Red Diamond", "Red Triangle", "Pink Diamond", "Blue Diamond" }, 0, "Look for the exact pairing."),
                    new ArcadeRound("Target: green circle", new[] { "Green Circle", "Green Hexagon", "Blue Circle", "Yellow Circle" }, 0, "Shape first, then color."),
                    new ArcadeRound("Target: purple star", new[] { "Purple Star", "Purple Cross", "Blue Star", "Pink Star" }, 0, "Peripheral scanning helps here."),
                    new ArcadeRound("Target: cyan hexagon", new[] { "Cyan Circle", "Teal Hexagon", "Cyan Hexagon", "Blue Hexagon" }, 2, "Some distractors are very close."),
                    new ArcadeRound("Target: orange bolt", new[] { "Orange Bolt", "Orange Bar", "Yellow Bolt", "Red Bolt" }, 0, "Go for the perfect match."),
                    new ArcadeRound("Target: white ring", new[] { "Grey Ring", "White Ring", "White Dot", "Blue Ring" }, 1, "Clean, exact spotting wins.")
                })),
            "Smile On Me" => new ArcadeChallengeGamePage(new ArcadeChallengeConfig(
                "smile_on_me",
                "Smile On Me",
                "Emotion",
                BrainSkill.Emotion,
                "#B65AF6",
                "#9747E5",
                "Choose the warmest or most encouraging emotional signal.",
                new[]
                {
                    new ArcadeRound("Which face feels most encouraging?", new[] { "😊 Warm smile", "😐 Flat", "😟 Worried", "😠 Angry" }, 0, "Pick the clearest positive signal."),
                    new ArcadeRound("Which reaction feels kindest?", new[] { "🙄 Eye roll", "🙂 Gentle smile", "😤 Frustrated", "😶 Blank" }, 1, "Go for supportive energy."),
                    new ArcadeRound("Which face is happiest to see you?", new[] { "😁 Big grin", "😑 Bored", "😥 Uneasy", "😠 Upset" }, 0, "The strongest positive cue stands out."),
                    new ArcadeRound("Which one shows calm reassurance?", new[] { "😬 Tense grin", "🙂 Soft smile", "😳 Shocked", "😴 Sleepy" }, 1, "Subtle warmth can be the correct answer."),
                    new ArcadeRound("Which response looks welcoming?", new[] { "😊 Open smile", "😒 Dismissive", "😨 Fearful", "😶 Neutral" }, 0, "Trust the clearest invitation."),
                    new ArcadeRound("Which face feels safest to approach?", new[] { "😠 Angry", "🙂 Relaxed", "😐 Closed off", "😫 Overwhelmed" }, 1, "Emotion reading is about approachability."),
                    new ArcadeRound("Which expression is most upbeat?", new[] { "😁 Joyful", "😔 Sad", "😟 Worried", "😤 Irritated" }, 0, "Positive emotion should be easy to spot."),
                    new ArcadeRound("Which face gives the nicest feedback?", new[] { "🙂 Friendly", "🙄 Annoyed", "😑 Unmoved", "😵 Confused" }, 0, "Finish with the kindest choice.")
                })),
            "Face To Face" => new ArcadeChallengeGamePage(new ArcadeChallengeConfig(
                "face_to_face",
                "Face To Face",
                "Emotion",
                BrainSkill.Emotion,
                "#B65AF6",
                "#9747E5",
                "Read the situation and choose the best face-to-face response.",
                new[]
                {
                    new ArcadeRound("A friend looks nervous before a presentation. What should you say?", new[] { "You will fail if you panic.", "Take one breath. I know you can do this.", "Why are you always stressed?", "Ignore them and walk away." }, 1, "Choose the response that helps regulate emotion."),
                    new ArcadeRound("Someone is quiet after receiving bad news. What fits best?", new[] { "Tell them to get over it.", "Sit with them and ask if they want support.", "Make a joke immediately.", "Change the topic." }, 1, "Empathy usually starts with presence."),
                    new ArcadeRound("A teammate seems frustrated with a mistake. Best move?", new[] { "Blame them harder.", "Ask what would help and offer a calm reset.", "Laugh about it.", "Pretend not to notice." }, 1, "Look for constructive support."),
                    new ArcadeRound("Your sibling seems proud of their progress. Best reply?", new[] { "That is nothing special.", "You worked hard for that, nice job.", "You got lucky.", "Maybe next time do better." }, 1, "Validation matters."),
                    new ArcadeRound("A classmate looks left out in a group. Best response?", new[] { "Leave them out too.", "Invite them into the conversation.", "Point it out loudly.", "Tell them it is their problem." }, 1, "Face-to-face care is inclusive."),
                    new ArcadeRound("A friend sounds disappointed in themselves. Best reply?", new[] { "Everyone is disappointed in you.", "It is okay to reset. What can we learn from it?", "You should quit.", "Just stop feeling that way." }, 1, "Compassion + perspective is strongest."),
                    new ArcadeRound("Someone is excited about a win. Best response?", new[] { "That is not impressive.", "Celebrate with them and ask how they did it.", "Ignore them.", "Change the subject." }, 1, "Shared joy builds trust."),
                    new ArcadeRound("A teammate looks overwhelmed. Best response?", new[] { "Work faster.", "Let us break this into smaller steps together.", "It is not my problem.", "Complain with them and do nothing." }, 1, "Good support creates clarity.")
                })),
            "Mood Match" => new ArcadeChallengeGamePage(new ArcadeChallengeConfig(
                "mood_match",
                "Mood Match",
                "Emotion",
                BrainSkill.Emotion,
                "#B65AF6",
                "#9747E5",
                "Match the mood to the situation as cleanly as you can.",
                new[]
                {
                    new ArcadeRound("You finished a goal you worked on for weeks. Which mood fits best?", new[] { "Proud", "Jealous", "Bored", "Embarrassed" }, 0, "Choose the emotion that naturally follows the event."),
                    new ArcadeRound("A loud crash happens behind you unexpectedly. Which mood appears first?", new[] { "Fear", "Joy", "Pride", "Relief" }, 0, "Immediate reactions are often instinctive."),
                    new ArcadeRound("A friend cancels plans after you prepared all day. Which mood fits?", new[] { "Relief", "Disappointment", "Amusement", "Pride" }, 1, "The most likely feeling usually comes from loss."),
                    new ArcadeRound("You receive warm praise from someone you respect. Which mood fits?", new[] { "Embarrassment", "Gratitude", "Anger", "Worry" }, 1, "Pick the most grounded emotional response."),
                    new ArcadeRound("You are waiting for exam results. Which mood is most likely?", new[] { "Calm certainty", "Anxiety", "Delight", "Pride" }, 1, "Anticipation often carries tension."),
                    new ArcadeRound("You helped someone solve a hard problem. Which mood fits?", new[] { "Resentment", "Satisfaction", "Shame", "Confusion" }, 1, "Helping well usually feels rewarding."),
                    new ArcadeRound("A plan changes suddenly right before a trip. Which mood fits first?", new[] { "Annoyance", "Joy", "Pride", "Tenderness" }, 0, "Think about the first emotional spike."),
                    new ArcadeRound("A teammate thanks you sincerely for support. Which mood fits best?", new[] { "Bitterness", "Connection", "Boredom", "Fear" }, 1, "Social warmth has its own emotional signal.")
                })),
            _ => new ArcadeChallengeGamePage(new ArcadeChallengeConfig(
                "fallback_arcade",
                title,
                "Quick challenge",
                BrainSkill.Focus,
                "#44A7FF",
                "#197BDB",
                "Tap the correct answer as quickly as possible.",
                new[]
                {
                    new ArcadeRound("Choose the best match.", new[] { "A", "B", "C", "D" }, 0, "A fast fallback challenge keeps the tile playable.")
                }))
        };
    }
}
