namespace Peak;

public static class CelebrationService
{
    static readonly string[] Palette =
    {
        "#37C6FF",
        "#4FD37B",
        "#FF6483",
        "#FFBC47",
        "#8E68FF",
        "#21C6A8"
    };

    public static async Task RunConfettiAsync(Layout container, int pieces = 54)
    {
        if (container is null)
        {
            return;
        }

        var random = new Random();
        var launched = new List<Task>(pieces);
        var confetti = new List<View>(pieces);

        double width = container.Width <= 1 ? 320 : container.Width;
        double height = container.Height <= 1 ? 640 : container.Height;

        for (int i = 0; i < pieces; i++)
        {
            var color = Color.FromArgb(Palette[random.Next(Palette.Length)]);
            View piece = CreatePiece(random, color, width);

            confetti.Add(piece);
            container.Children.Add(piece);

            launched.Add(AnimatePieceAsync(piece, height, random, width));
        }

        await Task.WhenAll(launched);

        if (pieces >= 36)
        {
            await Task.Delay(90);

            var encoreTasks = new List<Task>(22);
            for (int i = 0; i < 22; i++)
            {
                var sparkle = CreatePiece(random, Color.FromArgb(Palette[random.Next(Palette.Length)]), width, isEncore: true);

                confetti.Add(sparkle);
                container.Children.Add(sparkle);
                encoreTasks.Add(AnimatePieceAsync(sparkle, height, random, width));
            }

            await Task.WhenAll(encoreTasks);
        }

        foreach (var piece in confetti)
        {
            container.Children.Remove(piece);
        }
    }

    static View CreatePiece(Random random, Color color, double width, bool isEncore = false)
    {
        var startX = (width * 0.5) + random.NextDouble() * 120 - 60;
        var startY = -random.Next(50, isEncore ? 120 : 180);
        var isStreamer = random.NextDouble() > 0.58;

        return new BoxView
        {
            WidthRequest = isStreamer ? random.Next(7, 11) : random.Next(10, 18),
            HeightRequest = isStreamer ? random.Next(22, 36) : random.Next(10, 18),
            CornerRadius = isStreamer ? random.Next(2, 4) : random.Next(4, 8),
            Color = color,
            Rotation = random.Next(-25, 25),
            TranslationX = startX,
            TranslationY = startY,
            Opacity = 0
        };
    }

    static async Task AnimatePieceAsync(View piece, double height, Random random, double width)
    {
        var horizontalDrift = random.Next(-(int)(width * 0.42), (int)(width * 0.42));
        var targetY = height + random.Next(40, 180);
        var rotateTo = random.Next(-720, 720);
        var swayX = piece.TranslationX + random.Next(-50, 50);

        await piece.FadeTo(1, 70, Easing.CubicOut);
        await Task.WhenAll(
            piece.TranslateTo(swayX, targetY * 0.45, (uint)random.Next(420, 720), Easing.SinOut),
            piece.RotateTo(rotateTo * 0.5, (uint)random.Next(420, 720), Easing.Linear));
        await Task.WhenAll(
            piece.TranslateTo(piece.TranslationX + horizontalDrift, targetY, (uint)random.Next(760, 1220), Easing.CubicIn),
            piece.RotateTo(rotateTo, (uint)random.Next(760, 1220), Easing.Linear));
        await piece.FadeTo(0, 120, Easing.CubicIn);
    }
}
