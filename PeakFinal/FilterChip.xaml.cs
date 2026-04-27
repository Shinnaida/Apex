namespace Peak;

public partial class FilterChip : ContentView
{
    public event EventHandler? Clicked;

    public static readonly BindableProperty TextValueProperty =
        BindableProperty.Create(nameof(TextValue), typeof(string), typeof(FilterChip), "1 week",
            propertyChanged: (b, o, n) =>
            {
                if (b is FilterChip c && n is string s && c.ChipText != null)
                    c.ChipText.Text = s;
            });

    public static readonly BindableProperty IconTextProperty =
        BindableProperty.Create(nameof(IconText), typeof(string), typeof(FilterChip), string.Empty,
            propertyChanged: (b, o, n) =>
            {
                if (b is FilterChip chip)
                {
                    chip.ApplyIconState();
                }
            });

    public string TextValue
    {
        get => (string)GetValue(TextValueProperty);
        set => SetValue(TextValueProperty, value);
    }

    public string IconText
    {
        get => (string)GetValue(IconTextProperty);
        set => SetValue(IconTextProperty, value);
    }

    public FilterChip()
    {
        InitializeComponent();
        ChipText.Text = TextValue;
        ApplyIconState();
    }

    void OnTapped(object sender, TappedEventArgs e)
        => Clicked?.Invoke(this, EventArgs.Empty);

    void ApplyIconState()
    {
        var hasIconText = !string.IsNullOrWhiteSpace(IconText);
        IconTextLabel.Text = IconText;
        IconTextLabel.IsVisible = hasIconText;
        IconImage.IsVisible = !hasIconText;
    }
}
