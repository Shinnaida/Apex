using System.Collections.Generic;
using Microsoft.Maui.Controls;

namespace Peak;

public partial class MiniAreaCard : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(MiniAreaCard), "", propertyChanged: OnChanged);

    public static readonly BindableProperty TitleColorProperty =
        BindableProperty.Create(nameof(TitleColor), typeof(Color), typeof(MiniAreaCard), Colors.DeepSkyBlue, propertyChanged: OnChanged);

    public static readonly BindableProperty ScoreProperty =
        BindableProperty.Create(nameof(Score), typeof(string), typeof(MiniAreaCard), "0", propertyChanged: OnChanged);

    public static readonly BindableProperty AreaColorProperty =
        BindableProperty.Create(nameof(AreaColor), typeof(Color), typeof(MiniAreaCard), Colors.LightBlue, propertyChanged: OnChanged);

    public static readonly BindableProperty ValuesProperty =
        BindableProperty.Create(nameof(Values), typeof(IList<double>), typeof(MiniAreaCard), null, propertyChanged: OnChanged);

    public static readonly BindableProperty MaxScaleProperty =
        BindableProperty.Create(nameof(MaxScale), typeof(int), typeof(MiniAreaCard), 200, propertyChanged: OnChanged);

    public static readonly BindableProperty StartPeriodLabelProperty =
        BindableProperty.Create(nameof(StartPeriodLabel), typeof(string), typeof(MiniAreaCard), "1w", propertyChanged: OnChanged);

    public static readonly BindableProperty EndPeriodLabelProperty =
        BindableProperty.Create(nameof(EndPeriodLabel), typeof(string), typeof(MiniAreaCard), "Today", propertyChanged: OnChanged);

    public static readonly BindableProperty AxisLabelsProperty =
        BindableProperty.Create(nameof(AxisLabels), typeof(IList<string>), typeof(MiniAreaCard), null, propertyChanged: OnChanged);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public Color TitleColor
    {
        get => (Color)GetValue(TitleColorProperty);
        set => SetValue(TitleColorProperty, value);
    }

    public string Score
    {
        get => (string)GetValue(ScoreProperty);
        set => SetValue(ScoreProperty, value);
    }

    public Color AreaColor
    {
        get => (Color)GetValue(AreaColorProperty);
        set => SetValue(AreaColorProperty, value);
    }

    public IList<double>? Values
    {
        get => (IList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public int MaxScale
    {
        get => (int)GetValue(MaxScaleProperty);
        set => SetValue(MaxScaleProperty, value);
    }

    public string StartPeriodLabel
    {
        get => (string)GetValue(StartPeriodLabelProperty);
        set => SetValue(StartPeriodLabelProperty, value);
    }

    public string EndPeriodLabel
    {
        get => (string)GetValue(EndPeriodLabelProperty);
        set => SetValue(EndPeriodLabelProperty, value);
    }

    public IList<string>? AxisLabels
    {
        get => (IList<string>?)GetValue(AxisLabelsProperty);
        set => SetValue(AxisLabelsProperty, value);
    }

    public MiniAreaCard()
    {
        InitializeComponent();
        Apply();
    }

    static void OnChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MiniAreaCard card)
            card.Apply();
    }

    void Apply()
    {
        if (TitleLabel == null) return;

        TitleLabel.Text = Title;
        TitleLabel.TextColor = TitleColor;

        ScoreLabel.Text = Score;

        Chart.AreaColor = AreaColor;
        Chart.LineColor = TitleColor;
        Chart.Values = Values;
        TopAxisLabel.Text = Math.Max(0, MaxScale).ToString();
        BuildAxisLabels();
    }

    void BuildAxisLabels()
    {
        if (AxisLabelsHost == null)
        {
            return;
        }

        AxisLabelsHost.Children.Clear();

        var labels = AxisLabels is { Count: > 0 }
            ? AxisLabels
            : new List<string> { StartPeriodLabel, EndPeriodLabel };

        for (var i = 0; i < labels.Count; i++)
        {
            var text = labels[i];
            var label = new Label
            {
                Text = text,
                FontSize = 13,
                TextColor = Color.FromArgb("#555555"),
                HorizontalTextAlignment = i == labels.Count - 1
                    ? TextAlignment.End
                    : i == 0
                        ? TextAlignment.Start
                        : TextAlignment.Center,
                HorizontalOptions = LayoutOptions.FillAndExpand
            };

            AxisLabelsHost.Children.Add(label);
        }
    }
}
