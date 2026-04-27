using System.Collections.ObjectModel;

namespace Peak;

public partial class AchievementsPage : ContentPage
{
    private bool _hasLoaded;
    private readonly ObservableCollection<AchievementListItemViewModel> _achievementItems = new();

    public AchievementsPage()
    {
        InitializeComponent();
        AchievementsCollectionView.ItemsSource = _achievementItems;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        AccessibilityService.ApplyTextScale(this);
        await LoadInitialContentAsync();
    }

    private async Task LoadInitialContentAsync()
    {
        AchievementsLoadingCard.IsVisible = true;
        AchievementsLoadingIndicator.IsVisible = true;
        AchievementsLoadingIndicator.IsRunning = true;

        try
        {
            await AchievementsService.RefreshCurrentUserAsync(syncLocalProgress: BrainScoreService.HasResolvedCurrentUserHistory);

            var scores = BrainScoreService.GetCurrentScores();
            var rankName = BrainScoreService.GetPeakRankName(scores.PeakScore);
            var achievements = AchievementsService.GetAchievements();
            var summary = (Unlocked: achievements.Count(x => x.IsUnlocked), Total: achievements.Count);

            AchievementSummaryLabel.Text = $"{summary.Unlocked}/{summary.Total} unlocked";
            AchievementRankLabel.Text = $"Current rank: {rankName}";

            _achievementItems.Clear();
            foreach (var achievement in achievements)
            {
                _achievementItems.Add(AchievementListItemViewModel.FromAchievement(achievement));
            }
        }
        finally
        {
            AchievementsLoadingIndicator.IsRunning = false;
            AchievementsLoadingIndicator.IsVisible = false;
            AchievementsLoadingCard.IsVisible = false;
        }
    }

    private async void OnAchievementSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not AchievementListItemViewModel selected)
        {
            return;
        }

        try
        {
            await PageTransitionService.PushAsync(Navigation, new AchievementSpotlightPage(selected.Achievement));
        }
        finally
        {
            AchievementsCollectionView.SelectedItem = null;
        }
    }
}

public sealed class AchievementListItemViewModel
{
    private AchievementListItemViewModel(
        AchievementItem achievement,
        string backgroundColor,
        string strokeColor,
        string categoryBackgroundColor,
        string categoryTextColor,
        string statusText,
        Color statusColor)
    {
        Achievement = achievement;
        Title = achievement.Title;
        Description = achievement.Description;
        Category = achievement.Category;
        IconSource = achievement.IconSource;
        ProgressText = achievement.ProgressText;
        ProgressFraction = achievement.ProgressFraction;
        BackgroundColor = backgroundColor;
        StrokeColor = strokeColor;
        CategoryBackgroundColor = categoryBackgroundColor;
        CategoryTextColor = categoryTextColor;
        StatusText = statusText;
        StatusColor = statusColor;
    }

    public AchievementItem Achievement { get; }
    public string Title { get; }
    public string Description { get; }
    public string Category { get; }
    public string IconSource { get; }
    public string ProgressText { get; }
    public double ProgressFraction { get; }
    public string BackgroundColor { get; }
    public string StrokeColor { get; }
    public string CategoryBackgroundColor { get; }
    public string CategoryTextColor { get; }
    public string StatusText { get; }
    public Color StatusColor { get; }

    public static AchievementListItemViewModel FromAchievement(AchievementItem achievement)
    {
        var isUnlocked = achievement.IsUnlocked;
        return new AchievementListItemViewModel(
            achievement,
            backgroundColor: isUnlocked ? "#EAF9EF" : "#FFFFFF",
            strokeColor: isUnlocked ? "#A7D7B6" : "#E4E8EC",
            categoryBackgroundColor: isUnlocked ? "#D9F2E1" : "#F1EEFF",
            categoryTextColor: isUnlocked ? "#1E8E3E" : "#6F63E5",
            statusText: isUnlocked ? "UNLOCKED" : "LOCKED",
            statusColor: isUnlocked ? Color.FromArgb("#1E8E3E") : Color.FromArgb("#6F63E5"));
    }
}
