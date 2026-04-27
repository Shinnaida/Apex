namespace Peak;

public enum GamePauseAction
{
    Resume,
    Restart,
    Exit
}

public static class GamePauseService
{
    public static async Task<GamePauseAction> ShowAsync(Page hostPage, string helpTitle, string helpMessage)
    {
        var pausePage = new GamePausePage(helpTitle, helpMessage);
        await hostPage.Navigation.PushModalAsync(pausePage, false);
        return await pausePage.WaitForActionAsync();
    }

    public static async Task RestartCurrentPageAsync(Page currentPage)
    {
        if (Activator.CreateInstance(currentPage.GetType()) is not Page replacement)
        {
            return;
        }

        currentPage.Navigation.InsertPageBefore(replacement, currentPage);
        await currentPage.Navigation.PopAsync(false);
    }

    private sealed class GamePausePage : ContentPage
    {
        private readonly TaskCompletionSource<GamePauseAction> _tcs = new();
        private readonly string _helpTitle;
        private readonly string _helpMessage;
        private bool _soundEnabled;

        public GamePausePage(string helpTitle, string helpMessage)
        {
            _helpTitle = helpTitle;
            _helpMessage = helpMessage;
            _soundEnabled = GameAudioService.BackgroundEnabled || GameAudioService.EffectsEnabled;

            Shell.SetNavBarIsVisible(this, false);
            Shell.SetTabBarIsVisible(this, false);
            NavigationPage.SetHasNavigationBar(this, false);
            NavigationPage.SetHasBackButton(this, false);

            BackgroundColor = Color.FromArgb("#493A67");
            Content = BuildContent();
        }

        public Task<GamePauseAction> WaitForActionAsync() => _tcs.Task;

        protected override bool OnBackButtonPressed()
        {
            _ = CloseAsync(GamePauseAction.Resume);
            return true;
        }

        View BuildContent()
        {
            var root = new Grid();

            root.Children.Add(BuildPattern());

            var stack = new VerticalStackLayout
            {
                Spacing = 18,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                WidthRequest = 290
            };

            stack.Children.Add(new Label
            {
                Text = "GAME PAUSED",
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                CharacterSpacing = 1.2,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = Color.FromArgb("#F4C72E")
            });

            stack.Children.Add(BuildActionButton("RESUME", "#2C84E8", async () => await CloseAsync(GamePauseAction.Resume)));
            stack.Children.Add(BuildActionButton("HELP", "#434343", OnHelpAsync));
            stack.Children.Add(BuildActionButton("RESTART", "#434343", async () => await CloseAsync(GamePauseAction.Restart)));
            stack.Children.Add(BuildActionButton("EXIT", "#434343", async () => await CloseAsync(GamePauseAction.Exit)));

            var soundRow = new Grid
            {
                Margin = new Thickness(0, 8, 0, 0),
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new(GridLength.Star),
                    new(GridLength.Auto)
                }
            };

            soundRow.Children.Add(new Label
            {
                Text = "SOUND",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Color.FromArgb("#D8D5DE")
            });

            var toggle = BuildSoundToggle();
            Grid.SetColumn(toggle, 1);
            soundRow.Children.Add(toggle);

            stack.Children.Add(soundRow);
            root.Children.Add(stack);

            return root;
        }

        View BuildPattern()
        {
            var layout = new AbsoluteLayout { InputTransparent = true, Opacity = 0.22 };
            var shapes = new[]
            {
                new Rect(20, 30, 120, 90), new Rect(170, 18, 120, 92), new Rect(300, 42, 110, 86),
                new Rect(0, 140, 140, 96), new Rect(152, 160, 120, 92), new Rect(286, 138, 126, 96),
                new Rect(28, 278, 132, 100), new Rect(176, 262, 128, 96), new Rect(314, 296, 112, 88),
                new Rect(0, 402, 140, 96), new Rect(142, 430, 120, 94), new Rect(286, 414, 124, 98),
                new Rect(24, 548, 118, 90), new Rect(174, 530, 128, 96), new Rect(300, 568, 116, 90),
                new Rect(8, 676, 134, 96), new Rect(154, 700, 132, 96), new Rect(300, 684, 120, 92)
            };

            foreach (var rect in shapes)
            {
                var polygon = new Border
                {
                    BackgroundColor = Color.FromArgb("#5B4A7C"),
                    StrokeThickness = 0,
                    Rotation = (rect.X / 10) % 2 == 0 ? 0 : -8
                };

                AbsoluteLayout.SetLayoutFlags(polygon, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);
                AbsoluteLayout.SetLayoutBounds(polygon, rect);
                layout.Children.Add(polygon);
            }

            return layout;
        }

        Button BuildActionButton(string text, string textHex, Func<Task> onTap)
        {
            var button = new Button
            {
                Text = text,
                HeightRequest = 64,
                CornerRadius = 0,
                BackgroundColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                FontSize = 18,
                TextColor = Color.FromArgb(textHex)
            };

            button.Clicked += async (_, _) => await onTap();
            return button;
        }

        View BuildSoundToggle()
        {
            var track = new Border
            {
                WidthRequest = 118,
                HeightRequest = 38,
                BackgroundColor = _soundEnabled ? Color.FromArgb("#2C95F0") : Color.FromArgb("#8E95A3"),
                StrokeThickness = 0
            };

            var thumb = new Border
            {
                WidthRequest = 54,
                HeightRequest = 32,
                BackgroundColor = Colors.White,
                StrokeThickness = 0,
                HorizontalOptions = _soundEnabled ? LayoutOptions.End : LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(3, 0)
            };

            var grid = new Grid();
            grid.Children.Add(track);
            grid.Children.Add(thumb);

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                _soundEnabled = !_soundEnabled;
                GameAudioService.BackgroundEnabled = _soundEnabled;
                GameAudioService.EffectsEnabled = _soundEnabled;
                track.BackgroundColor = _soundEnabled ? Color.FromArgb("#2C95F0") : Color.FromArgb("#8E95A3");
                thumb.HorizontalOptions = _soundEnabled ? LayoutOptions.End : LayoutOptions.Start;
            };

            grid.GestureRecognizers.Add(tap);
            return grid;
        }

        async Task OnHelpAsync()
        {
            await DisplayAlert(_helpTitle, _helpMessage, "OK");
        }

        async Task CloseAsync(GamePauseAction action)
        {
            if (_tcs.Task.IsCompleted)
            {
                return;
            }

            await Navigation.PopModalAsync(false);
            _tcs.TrySetResult(action);
        }
    }
}
