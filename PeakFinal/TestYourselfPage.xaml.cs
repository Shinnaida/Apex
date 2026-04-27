namespace Peak;

public partial class TestYourselfPage : ContentPage
{
    readonly IQTestDefinition _definition;

    public TestYourselfPage(IQTestDefinition definition)
    {
        InitializeComponent();
        _definition = definition;

        NavigationPage.SetHasBackButton(this, false);
        ApplyDefinition();
    }

    void ApplyDefinition()
    {
        SubtitleLabel.Text = _definition.Subtitle;
        ModeTitleLabel.Text = _definition.Title;
        ModeDescriptionLabel.Text = _definition.Description;
        QuestionCountLabel.Text = $"{_definition.QuestionCount} questions";
        TimeLimitLabel.Text = $"{(int)_definition.TimeLimit.TotalMinutes} minutes";
        BeginButton.Text = _definition.ActionLabel;
        BeginButton.BackgroundColor = Color.FromArgb(_definition.AccentColor);
        HeroGradientStart.Color = Color.FromArgb(_definition.AccentColor);
        HeroGradientEnd.Color = Color.FromArgb(_definition.GradientEndColor);
        HeroImage.Source = _definition.HeroImageSource;
    }

    async void OnBeginClicked(object sender, EventArgs e)
    {
        var session = IQSession.Create(_definition);
        await PageTransitionService.PushAsync(Navigation, () => new IQGamePage(session));
    }
}
