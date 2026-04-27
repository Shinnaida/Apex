using Microsoft.Maui.Controls.Shapes;

namespace Peak;

public enum MatchaLayerKind
{
    Solid,
    Ring
}

public sealed record MatchaLayerDescriptor(
    MatchaLayerKind Kind,
    string ColorKey,
    double Scale);

public static class MatchaMadnessVisuals
{
    private static readonly Dictionary<string, Color> Palette = new(StringComparer.Ordinal)
    {
        ["teal"] = Color.FromArgb("#2D8A5C"),
        ["lime"] = Color.FromArgb("#9AD018"),
        ["orange"] = Color.FromArgb("#FF7C1E"),
        ["yellow"] = Color.FromArgb("#F4D260"),
        ["magenta"] = Color.FromArgb("#B2047C"),
        ["pink"] = Color.FromArgb("#F58ABC"),
        ["coral"] = Color.FromArgb("#D95663"),
        ["sky"] = Color.FromArgb("#6FC6DB"),
        ["olive"] = Color.FromArgb("#8A8520"),
        ["mint"] = Color.FromArgb("#61D07D")
    };

    public static View CreatePatternView(IReadOnlyList<MatchaLayerDescriptor> layers, double tileSize = 84)
    {
        var layout = new AbsoluteLayout
        {
            WidthRequest = tileSize,
            HeightRequest = tileSize
        };

        foreach (var layer in layers.Reverse())
        {
            var view = CreateLayer(layer, tileSize);
            var x = (tileSize - view.WidthRequest) / 2;
            var y = (tileSize - view.HeightRequest) / 2;
            AbsoluteLayout.SetLayoutBounds(view, new Rect(x, y, view.WidthRequest, view.HeightRequest));
            layout.Children.Add(view);
        }

        return layout;
    }

    public static Color ResolveColor(string key)
    {
        return Palette.TryGetValue(key, out var color)
            ? color
            : Color.FromArgb("#9AD018");
    }

    public static IReadOnlyList<MatchaLayerDescriptor> GetPreviewStack(int index)
    {
        return index switch
        {
            0 => new[]
            {
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "teal", 0.88),
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "orange", 0.60),
                new MatchaLayerDescriptor(MatchaLayerKind.Solid, "lime", 0.28)
            },
            1 => new[]
            {
                new MatchaLayerDescriptor(MatchaLayerKind.Solid, "magenta", 0.84),
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "yellow", 0.28)
            },
            2 => new[]
            {
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "teal", 0.86),
                new MatchaLayerDescriptor(MatchaLayerKind.Solid, "olive", 0.58),
                new MatchaLayerDescriptor(MatchaLayerKind.Solid, "lime", 0.24)
            },
            3 => new[]
            {
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "coral", 0.88),
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "sky", 0.68),
                new MatchaLayerDescriptor(MatchaLayerKind.Solid, "pink", 0.36),
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "sky", 0.18)
            },
            4 => new[]
            {
                new MatchaLayerDescriptor(MatchaLayerKind.Solid, "magenta", 0.82),
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "pink", 0.52),
                new MatchaLayerDescriptor(MatchaLayerKind.Solid, "lime", 0.24)
            },
            5 => new[]
            {
                new MatchaLayerDescriptor(MatchaLayerKind.Solid, "magenta", 0.82),
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "yellow", 0.26)
            },
            6 => new[]
            {
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "teal", 0.76),
                new MatchaLayerDescriptor(MatchaLayerKind.Solid, "olive", 0.50),
                new MatchaLayerDescriptor(MatchaLayerKind.Solid, "pink", 0.24),
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "sky", 0.16)
            },
            7 => new[]
            {
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "coral", 0.86),
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "sky", 0.64),
                new MatchaLayerDescriptor(MatchaLayerKind.Solid, "pink", 0.34)
            },
            8 => new[]
            {
                new MatchaLayerDescriptor(MatchaLayerKind.Solid, "magenta", 0.82),
                new MatchaLayerDescriptor(MatchaLayerKind.Solid, "orange", 0.52),
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "yellow", 0.20)
            },
            9 => new[]
            {
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "coral", 0.88),
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "sky", 0.68),
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "magenta", 0.50),
                new MatchaLayerDescriptor(MatchaLayerKind.Solid, "pink", 0.24)
            },
            10 => new[]
            {
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "teal", 0.82),
                new MatchaLayerDescriptor(MatchaLayerKind.Solid, "olive", 0.58),
                new MatchaLayerDescriptor(MatchaLayerKind.Solid, "pink", 0.28)
            },
            _ => new[]
            {
                new MatchaLayerDescriptor(MatchaLayerKind.Solid, "orange", 0.82),
                new MatchaLayerDescriptor(MatchaLayerKind.Ring, "yellow", 0.24)
            }
        };
    }

    private static View CreateLayer(MatchaLayerDescriptor layer, double tileSize)
    {
        var size = Math.Max(10, tileSize * layer.Scale);
        var radius = (float)(size / 2);

        if (layer.Kind == MatchaLayerKind.Solid)
        {
            return new Border
            {
                WidthRequest = size,
                HeightRequest = size,
                BackgroundColor = ResolveColor(layer.ColorKey),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = radius }
            };
        }

        return new Border
        {
            WidthRequest = size,
            HeightRequest = size,
            BackgroundColor = Colors.Transparent,
            Stroke = ResolveColor(layer.ColorKey),
            StrokeThickness = Math.Max(4, size * 0.11),
            StrokeShape = new RoundRectangle { CornerRadius = radius }
        };
    }
}
