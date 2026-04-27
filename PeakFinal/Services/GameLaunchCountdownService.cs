namespace Peak;

using RoundRectangle = Microsoft.Maui.Controls.Shapes.RoundRectangle;

public static class GameLaunchCountdownService
{
    public static async Task ShowAsync(Page? hostPage)
    {
        if (hostPage is not ContentPage contentPage || contentPage.Content is not Layout root)
        {
            return;
        }

        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#D90F1F3A"),
            Opacity = 0
        };

        var backgroundGlow = new Border
        {
            HeightRequest = 320,
            WidthRequest = 320,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            BackgroundColor = Color.FromArgb("#2A9CC9FF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 160 },
            Opacity = 0.95,
            Scale = 0.84
        };

        var content = new VerticalStackLayout
        {
            Spacing = 10,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "GET READY",
                    FontSize = 24,
                    FontAttributes = FontAttributes.Bold,
                    CharacterSpacing = 2.8,
                    HorizontalTextAlignment = TextAlignment.Center,
                    TextColor = Colors.White
                },
                new Label
                {
                    Text = "3",
                    FontSize = 110,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalTextAlignment = TextAlignment.Center,
                    TextColor = Colors.White,
                    Margin = new Thickness(0, 2, 0, 0)
                }
            }
        };

        var subLabel = new Label
        {
            Text = "Focus. Breathe. Start strong.",
            FontSize = 17,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Color.FromArgb("#D7EBFF")
        };
        content.Children.Add(subLabel);

        var contentHost = new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        contentHost.Children.Add(backgroundGlow);
        contentHost.Children.Add(content);

        var topAccent = new Border
        {
            HeightRequest = 260,
            WidthRequest = 260,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            TranslationX = 90,
            TranslationY = -70,
            BackgroundColor = Color.FromArgb("#1FFFFFFF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 130 }
        };

        var bottomAccent = new Border
        {
            HeightRequest = 220,
            WidthRequest = 220,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.End,
            TranslationX = -65,
            TranslationY = 60,
            BackgroundColor = Color.FromArgb("#149CC9FF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 110 }
        };

        overlay.Children.Add(topAccent);
        overlay.Children.Add(bottomAccent);
        overlay.Children.Add(contentHost);
        PrepareOverlayForHost(contentPage, root, overlay);
        root.Children.Add(overlay);

        var countdownLabel = content.Children.OfType<Label>().ElementAt(1);

        try
        {
            await Task.WhenAll(
                overlay.FadeTo(1, 140, Easing.CubicOut),
                backgroundGlow.ScaleTo(1, 220, Easing.CubicOut));

            foreach (var value in new[] { "3", "2", "1" })
            {
                countdownLabel.Text = value;
                countdownLabel.Scale = 0.82;
                countdownLabel.Opacity = 0.85;

                await Task.WhenAll(
                    countdownLabel.ScaleTo(1, 180, Easing.CubicOut),
                    countdownLabel.FadeTo(1, 120, Easing.CubicOut));

                await Task.Delay(260);
            }

            countdownLabel.Text = "GO";
            countdownLabel.TextColor = Color.FromArgb("#16A34A");
            subLabel.Text = "You've got this.";
            await Task.WhenAll(
                countdownLabel.ScaleTo(1.08, 120, Easing.CubicOut),
                overlay.FadeTo(0, 180, Easing.CubicIn));
        }
        finally
        {
            root.Children.Remove(overlay);
        }
    }

    static void PrepareOverlayForHost(ContentPage page, Layout root, Grid overlay)
    {
        overlay.ZIndex = 9999;

        if (page.Padding != default)
        {
            overlay.Margin = new Thickness(
                -page.Padding.Left,
                -page.Padding.Top,
                -page.Padding.Right,
                -page.Padding.Bottom);
        }

        if (root is Grid grid)
        {
            Grid.SetRow(overlay, 0);
            Grid.SetColumn(overlay, 0);
            Grid.SetRowSpan(overlay, Math.Max(1, grid.RowDefinitions.Count));
            Grid.SetColumnSpan(overlay, Math.Max(1, grid.ColumnDefinitions.Count));
        }
    }
}
