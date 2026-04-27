using System.Globalization;
using System.Linq;

namespace Peak;

public partial class OverTimePage : ContentPage
{
    private const string FilterLifetime = "Lifetime";
    private const string FilterSixMonths = "6 months";
    private const string FilterThreeMonths = "3 months";
    private const string FilterFourWeeks = "4 weeks";
    private const string FilterOneWeek = "1 week";

    private readonly Dictionary<string, int?> _lookbackDaysByFilter = new(StringComparer.Ordinal)
    {
        [FilterLifetime] = null,
        [FilterSixMonths] = 182,
        [FilterThreeMonths] = 91,
        [FilterFourWeeks] = 28,
        [FilterOneWeek] = 7
    };

    private string _activeFilter = FilterOneWeek;
    private DateTime _customStartDate = DateTime.Today.AddDays(-6);
    private bool _isSyncingCalendarDate;
    private bool _isFilterMenuAnimating;

    private static readonly (Color Title, Color Area)[] DefaultCardPalette =
    {
        (Color.FromArgb("#11A7D8"), Color.FromArgb("#64CDEB")),
        (Color.FromArgb("#F5A623"), Color.FromArgb("#FFC46B")),
        (Color.FromArgb("#42C66C"), Color.FromArgb("#8FE7AB")),
        (Color.FromArgb("#5E6DE0"), Color.FromArgb("#9AA5FF")),
        (Color.FromArgb("#2B7BFF"), Color.FromArgb("#86B3FF")),
        (Color.FromArgb("#FF4B6E"), Color.FromArgb("#FF9DB2"))
    };

    private static readonly (Color Title, Color Area)[] ColorSafeCardPalette =
    {
        (Color.FromArgb("#0072B2"), Color.FromArgb("#7FC8EF")),
        (Color.FromArgb("#E69F00"), Color.FromArgb("#F4CE76")),
        (Color.FromArgb("#009E73"), Color.FromArgb("#73D7BB")),
        (Color.FromArgb("#CC79A7"), Color.FromArgb("#E5B7D3")),
        (Color.FromArgb("#56B4E9"), Color.FromArgb("#A9DCF6")),
        (Color.FromArgb("#D55E00"), Color.FromArgb("#EFAF82"))
    };

    public OverTimePage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        CalendarPicker.MaximumDate = DateTime.Today;
        _isSyncingCalendarDate = true;
        CalendarPicker.Date = _customStartDate;
        _isSyncingCalendarDate = false;
        ApplyFilter(_activeFilter);
        ApplyAccessibility();
    }

    async void OnBrainTapped(object sender, EventArgs e)
    {
        if (Shell.Current is not null)
        {
            await PageTransitionService.GoToAsync("//stats");
            return;
        }

        await PageTransitionService.PushAsync(Navigation, new StatsPage());
    }

    void OnOverTimeTapped(object sender, EventArgs e)
    {
        // Already in Over Time.
    }

    async void OnGamesTapped(object sender, EventArgs e)
    {
        await PageTransitionService.GoToAsync(nameof(GamesStatsPage));
    }

    async void OnLeaderboardsTapped(object sender, EventArgs e)
    {
        await PageTransitionService.GoToAsync(nameof(LeaderboardsPage));
    }

    async void OnCalendarClicked(object sender, EventArgs e)
    {
        if (FilterOverlay.IsVisible)
        {
            await HideFilterMenuAsync();
            return;
        }

        await ShowFilterMenuAsync();
    }

    async void OnCloseOverlay(object sender, EventArgs e)
    {
        await HideFilterMenuAsync();
    }

    async void OnOverlayTapped(object sender, EventArgs e)
    {
        await HideFilterMenuAsync();
    }

    async void OnFilterClicked(object sender, EventArgs e)
    {
        if (sender is FilterChip chip && _lookbackDaysByFilter.ContainsKey(chip.TextValue))
        {
            ApplyFilter(chip.TextValue);
        }

        await HideFilterMenuAsync();
    }

    void OnCalendarDateSelected(object sender, DateChangedEventArgs e)
    {
        if (_isSyncingCalendarDate)
        {
            return;
        }

        var selectedDate = e.NewDate.Date;
        if (selectedDate > DateTime.Today)
        {
            selectedDate = DateTime.Today;
        }

        _customStartDate = selectedDate;
        ApplyCustomDateRange(_customStartDate);
    }

    private void ApplyFilter(string filter)
    {
        _activeFilter = _lookbackDaysByFilter.ContainsKey(filter) ? filter : FilterOneWeek;
        var trend = BrainScoreService.GetOverTimeTrend(_lookbackDaysByFilter[_activeFilter], maxPoints: 36);

        if (trend.Count == 0)
        {
            return;
        }

        var startLabel = GetStartLabel(_activeFilter, trend[0].DateUtc);
        OverTimeTitleLabel.Text = BuildTitle(_activeFilter);

        var sessions = BrainScoreService.GetRecordedSessionCount();
        var activeDays = BrainScoreService.GetActiveDayCount();
        OverTimeSubtitleLabel.Text = sessions == 0
            ? "Play a game or take a test to start your over time trend."
            : $"{sessions} sessions across {activeDays} active days.";

        BindTrendCards(trend, startLabel);
    }

    private void ApplyCustomDateRange(DateTime startDate)
    {
        var clampedStart = startDate.Date > DateTime.Today ? DateTime.Today : startDate.Date;
        var days = Math.Max(1, (DateTime.Today - clampedStart).Days + 1);
        var trend = BrainScoreService.GetOverTimeTrend(days, maxPoints: 36);
        if (trend.Count == 0)
        {
            return;
        }

        _activeFilter = "Custom";
        OverTimeTitleLabel.Text = $"SINCE {clampedStart:dd MMM yyyy}".ToUpperInvariant();
        var sessions = BrainScoreService.GetRecordedSessionCount();
        var activeDays = BrainScoreService.GetActiveDayCount();
        OverTimeSubtitleLabel.Text = sessions == 0
            ? "Play a game or take a test to start your over time trend."
            : $"{sessions} sessions across {activeDays} active days.";

        var startLabel = clampedStart.ToString("d MMM", CultureInfo.InvariantCulture);
        BindTrendCards(trend, startLabel);
    }

    private void BindTrendCards(IReadOnlyList<BrainScoreTrendPoint> trend, string startLabel)
    {
        var peakValues = trend.Select(x => x.Scores.PeakScore).ToList();
        var memoryValues = trend.Select(x => x.Scores.Memory).ToList();
        var problemValues = trend.Select(x => x.Scores.ProblemSolving).ToList();
        var languageValues = trend.Select(x => x.Scores.Language).ToList();
        var agilityValues = trend.Select(x => x.Scores.MentalAgility).ToList();
        var focusValues = trend.Select(x => x.Scores.Focus).ToList();

        var axisLabels = BuildAxisLabelsForFilter(_activeFilter, startLabel, trend.Count);

        BindCard(PeakCard, peakValues, startLabel, CalculateAxisMax(peakValues, 100, 600), axisLabels);
        BindCard(MemoryCard, memoryValues, startLabel, CalculateAxisMax(memoryValues, 20, 200), axisLabels);
        BindCard(ProblemCard, problemValues, startLabel, CalculateAxisMax(problemValues, 20, 200), axisLabels);
        BindCard(LanguageCard, languageValues, startLabel, CalculateAxisMax(languageValues, 20, 200), axisLabels);
        BindCard(AgilityCard, agilityValues, startLabel, CalculateAxisMax(agilityValues, 20, 200), axisLabels);
        BindCard(FocusCard, focusValues, startLabel, CalculateAxisMax(focusValues, 20, 200), axisLabels);
    }

    private static void BindCard(
        MiniAreaCard card,
        IReadOnlyList<int> values,
        string startLabel,
        int axisMax,
        IReadOnlyList<string> axisLabels)
    {
        var safeAxisMax = Math.Max(1, axisMax);
        var normalized = new List<double>(values.Count);

        for (int i = 0; i < values.Count; i++)
        {
            normalized.Add(Math.Clamp(values[i] / (double)safeAxisMax, 0, 1));
        }

        card.Score = values[^1].ToString(CultureInfo.InvariantCulture);
        card.MaxScale = safeAxisMax;
        card.StartPeriodLabel = startLabel;
        card.EndPeriodLabel = "Today";
        card.AxisLabels = axisLabels.ToList();
        card.Values = normalized;
    }

    private static IReadOnlyList<string> BuildAxisLabelsForFilter(string filter, string startLabel, int pointCount)
    {
        return filter switch
        {
            FilterLifetime => BuildAxisSequence(startLabel, "Today", Math.Min(6, Math.Max(2, pointCount))),
            FilterSixMonths => new[] { "6m", "5m", "4m", "3m", "2m", "1m", "Today" },
            FilterThreeMonths => new[] { "3m", "2m", "1m", "Today" },
            FilterFourWeeks => new[] { "4w", "3w", "2w", "1w", "Today" },
            "Custom" => new[] { startLabel, "Today" },
            _ => new[] { "1w", "Today" }
        };
    }

    private static IReadOnlyList<string> BuildAxisSequence(string firstLabel, string lastLabel, int count)
    {
        if (count <= 2)
        {
            return new[] { firstLabel, lastLabel };
        }

        var labels = new List<string>(count) { firstLabel };
        for (var i = 0; i < count - 2; i++)
        {
            labels.Add(string.Empty);
        }

        labels.Add(lastLabel);
        return labels;
    }

    private static int CalculateAxisMax(IReadOnlyList<int> values, int step, int floor)
    {
        if (values.Count == 0)
        {
            return floor;
        }

        var maxValue = Math.Max(floor, values.Max());
        return ((maxValue + step - 1) / step) * step;
    }

    private static string BuildTitle(string filter)
    {
        return filter switch
        {
            FilterLifetime => "LIFETIME PROGRESS",
            FilterSixMonths => "LAST 6 MONTHS OF PROGRESS",
            FilterThreeMonths => "LAST 3 MONTHS OF PROGRESS",
            FilterFourWeeks => "LAST 4 WEEKS OF PROGRESS",
            _ => "LAST WEEK OF PROGRESS"
        };
    }

    private static string GetStartLabel(string filter, DateTime firstPointUtc)
    {
        return filter switch
        {
            FilterOneWeek => "1w",
            FilterFourWeeks => "4w",
            FilterThreeMonths => "3m",
            FilterSixMonths => "6m",
            FilterLifetime => firstPointUtc.ToString("MMM yy", CultureInfo.InvariantCulture),
            _ => "Start"
        };
    }

    private void ApplyAccessibility()
    {
        var options = AccessibilityService.GetOptions();
        AccessibilityService.ApplyTextScale(this);

        BackgroundColor = options.HighContrastEnabled
            ? Color.FromArgb("#FFFFFF")
            : Color.FromArgb("#F3F5F7");

        OverTimeTitleLabel.TextColor = options.HighContrastEnabled
            ? Color.FromArgb("#1A222B")
            : Color.FromArgb("#4A4A4A");
        OverTimeSubtitleLabel.TextColor = options.HighContrastEnabled
            ? Color.FromArgb("#3D4A57")
            : Color.FromArgb("#8A8A8A");

        CalendarFab.BackgroundColor = options.ColorSafeChartsEnabled
            ? Color.FromArgb("#0072B2")
            : Color.FromArgb("#11A7D8");
        CloseFab.BackgroundColor = CalendarFab.BackgroundColor;

        ApplyCardPalette(options.ColorSafeChartsEnabled);
    }

    private void ApplyCardPalette(bool colorSafe)
    {
        var palette = colorSafe ? ColorSafeCardPalette : DefaultCardPalette;
        ApplyPaletteToCard(PeakCard, palette[0]);
        ApplyPaletteToCard(MemoryCard, palette[1]);
        ApplyPaletteToCard(ProblemCard, palette[2]);
        ApplyPaletteToCard(LanguageCard, palette[3]);
        ApplyPaletteToCard(AgilityCard, palette[4]);
        ApplyPaletteToCard(FocusCard, palette[5]);
    }

    private static void ApplyPaletteToCard(MiniAreaCard card, (Color Title, Color Area) palette)
    {
        card.TitleColor = palette.Title;
        card.AreaColor = palette.Area;
    }

    private async Task ShowFilterMenuAsync()
    {
        if (_isFilterMenuAnimating || FilterOverlay.IsVisible)
        {
            return;
        }

        _isFilterMenuAnimating = true;
        FilterOverlay.IsVisible = true;
        FilterOverlay.Opacity = 0;
        CloseFab.IsVisible = true;
        CloseFab.Opacity = 0;
        CloseFab.Scale = 0.8;
        CalendarFab.Opacity = 1;

        await Task.WhenAll(
            FilterOverlay.FadeTo(1, 140, Easing.CubicOut),
            CalendarFab.FadeTo(0, 90, Easing.CubicIn),
            CloseFab.FadeTo(1, 150, Easing.CubicOut),
            CloseFab.ScaleTo(1, 150, Easing.CubicOut));

        CalendarFab.IsVisible = false;
        _isFilterMenuAnimating = false;
    }

    private async Task HideFilterMenuAsync()
    {
        if (_isFilterMenuAnimating || !FilterOverlay.IsVisible)
        {
            return;
        }

        _isFilterMenuAnimating = true;
        CalendarFab.IsVisible = true;
        CalendarFab.Opacity = 0;

        await Task.WhenAll(
            FilterOverlay.FadeTo(0, 120, Easing.CubicIn),
            CloseFab.FadeTo(0, 100, Easing.CubicIn),
            CloseFab.ScaleTo(0.84, 100, Easing.CubicIn),
            CalendarFab.FadeTo(1, 120, Easing.CubicOut));

        FilterOverlay.IsVisible = false;
        CloseFab.IsVisible = false;
        CloseFab.Scale = 1;
        _isFilterMenuAnimating = false;
    }
}


