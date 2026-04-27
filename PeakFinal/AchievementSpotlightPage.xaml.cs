namespace Peak;

public partial class AchievementSpotlightPage : ContentPage
{
    public AchievementSpotlightPage(AchievementItem achievement, string? ownerName = null)
    {
        InitializeComponent();
        BindAchievement(achievement, ownerName);
    }

    private void BindAchievement(AchievementItem achievement, string? ownerName)
    {
        Title = achievement.Title;
        AchievementCategoryLabel.Text = achievement.Category.ToUpperInvariant();
        AchievementHeroStatusLabel.Text = achievement.IsUnlocked ? "UNLOCKED" : "IN PROGRESS";
        AchievementTitleLabel.Text = achievement.Title;
        AchievementSubtitleLabel.Text = string.IsNullOrWhiteSpace(ownerName)
            ? achievement.IsUnlocked
                ? "Part of your collection"
                : "Still in progress"
            : achievement.IsUnlocked
                ? $"Shown on {ownerName}'s profile"
                : $"Tracking progress for {ownerName}";
        AchievementIconImage.Source = achievement.IconSource;
        AchievementDescriptionLabel.Text = achievement.Description;
        AchievementProgressLabel.Text = achievement.ProgressText;
        AchievementProgressBar.Progress = achievement.ProgressFraction;
        AchievementCategoryChip.BackgroundColor = ResolveCategoryColor(achievement.Category);

        if (achievement.IsUnlocked)
        {
            HeroCard.BackgroundColor = Color.FromArgb("#2F9E62");
            AchievementStatusChip.BackgroundColor = Color.FromArgb("#1F6F44");
            AchievementProgressBar.ProgressColor = Color.FromArgb("#2F9E62");
            AchievementStatusLabel.Text = "Unlocked badges are ready to be shown off on player profiles.";
            AchievementStatusLabel.TextColor = Color.FromArgb("#2F9E62");
            AchievementProgressLabel.TextColor = Color.FromArgb("#2F9E62");
            return;
        }

        HeroCard.BackgroundColor = Color.FromArgb("#6F63E5");
        AchievementStatusChip.BackgroundColor = Color.FromArgb("#334EAF");
        AchievementProgressBar.ProgressColor = Color.FromArgb("#6F63E5");
        AchievementStatusLabel.Text = "Keep training and this badge will light up automatically once you hit the target.";
        AchievementStatusLabel.TextColor = Color.FromArgb("#6F63E5");
        AchievementProgressLabel.TextColor = Color.FromArgb("#6F63E5");
    }

    private static Color ResolveCategoryColor(string category)
    {
        return category switch
        {
            "Journey" => Color.FromArgb("#A03D68"),
            "Consistency" => Color.FromArgb("#2B6F95"),
            "Arcade" => Color.FromArgb("#B14D2C"),
            "Rank" => Color.FromArgb("#2C4FA3"),
            "Skill" => Color.FromArgb("#5A3EA6"),
            _ => Color.FromArgb("#4D42B8")
        };
    }
}
