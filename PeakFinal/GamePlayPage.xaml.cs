namespace Peak;

public partial class GamePlayPage : ContentPage
{
    readonly IQSession _session;
    readonly Button[] _buttons;
    readonly TimeSpan _autoAdvanceDelay = TimeSpan.FromMilliseconds(900);
    bool _answered = false;
    bool _navigating = false;

    //public GamePlayPage(IQCategory category, int count = 10)
    //{
    //    InitializeComponent();

    //    _buttons = new[] { Opt0, Opt1, Opt2, Opt3 };

    //    var pool = IQQuestionBank.All
    //        .Where(q => q.Category == category)
    //        .ToList();

    //    // If that category has no questions yet, fallback to all questions (prevents crash)
    //    if (pool.Count == 0)
    //        pool = IQQuestionBank.All.ToList();

    //    _session = new IQSession(pool, count: Math.Min(count, pool.Count));

    //    LoadQuestion();
    //}
    public GamePlayPage(IQSession session)
    {
        InitializeComponent();

        _buttons = new[] { Opt0, Opt1, Opt2, Opt3 };
        _session = session;

        LoadQuestion();
        NavigationPage.SetHasBackButton(this, false);

    }


    protected override bool OnBackButtonPressed()
    {
        // prevent back-swiping/cheating during test
        return true;
    }

    void LoadQuestion()
    {
        _answered = false;
        //NextButton.IsEnabled = false;
        ExplanationCard.IsVisible = false;

        foreach (var b in _buttons)
        {
            b.IsEnabled = true;
            b.BackgroundColor = Color.FromArgb("#F3F3F3");
            b.TextColor = Color.FromArgb("#333333");
        }

        var q = _session.Current;

        // Category chip label and color
        CategoryChip.Text = q.Category switch
        {
            IQCategory.Spatial => "Memory",
            IQCategory.Verbal => "Language",
            IQCategory.LogicMath => "Problem Solving",
            IQCategory.Abstract => "Focus",
            _ => q.Category.ToString()
        };

        CategoryChip.BackgroundColor = q.Category switch
        {
            IQCategory.Spatial => Color.FromArgb("#F59E0B"),   // orange
            IQCategory.Verbal => Color.FromArgb("#6366F1"),    // purple/indigo
            IQCategory.LogicMath => Color.FromArgb("#22C55E"), // green
            IQCategory.Abstract => Color.FromArgb("#F43F5E"),  // pink/red
            _ => Color.FromArgb("#1DA1F2")
        };

        CounterLabel.Text = $"{_session.Index + 1}/{_session.Questions.Count}";
        TopProgressBar.Progress = (_session.Index + 1) / (double)_session.Questions.Count;

        PromptLabel.Text = q.Prompt;

        if (!string.IsNullOrWhiteSpace(q.ImageSource))
        {
            QuestionImage.Source = q.ImageSource;
            QuestionImage.IsVisible = true;
        }
        else
        {
            QuestionImage.Source = null;
            QuestionImage.IsVisible = false;
        }

        Opt0.Text = q.Options[0];
        Opt1.Text = q.Options[1];
        Opt2.Text = q.Options[2];
        Opt3.Text = q.Options[3];

        Opt0.CommandParameter = 0;
        Opt1.CommandParameter = 1;
        Opt2.CommandParameter = 2;
        Opt3.CommandParameter = 3;

        //NextButton.Text = _session.IsLast ? "Finish" : "Next";
    }

    void OnOptionClicked(object sender, EventArgs e)
    {
        if (_answered) return;
        _answered = true;

        var btn = (Button)sender;
        int chosen = (int)(btn.CommandParameter ?? 0);

        var q = _session.Current;

        _session.SubmitAnswer(chosen);

        foreach (var b in _buttons)
            b.IsEnabled = false;

        int correct = q.CorrectIndex;

        // Correct answer green
        _buttons[correct].BackgroundColor = Color.FromArgb("#22C55E");
        _buttons[correct].TextColor = Colors.White;

        // Wrong chosen red
        if (chosen != correct)
        {
            _buttons[chosen].BackgroundColor = Color.FromArgb("#EF5350");
            _buttons[chosen].TextColor = Colors.White;
        }

        ExplanationLabel.Text = q.Explanation;
        ExplanationCard.IsVisible = true;

        //NextButton.IsEnabled = false;

        _ = AutoAdvanceAsync();
    }

    async void OnNextClicked(object sender, EventArgs e)
    {
        if (!_answered) return;
        await GoNextAsync();
    }

    async Task AutoAdvanceAsync()
    {
        await Task.Delay(_autoAdvanceDelay);
        await MainThread.InvokeOnMainThreadAsync(GoNextAsync);
    }

    async Task GoNextAsync()
    {
        if (_navigating) return;
        _navigating = true;

        try
        {
            if (_session.IsLast)
            {
                await PageTransitionService.PushAsync(Navigation, new IQResultsPage(_session));
                return;
            }

            _session.Next();
            LoadQuestion();
        }
        finally
        {
            _navigating = false;
        }
    }

}

