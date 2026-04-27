namespace Peak;

public partial class AgePage : ContentPage
{
    public int? SelectedAge { get; private set; }

    public AgePage()
    {
        InitializeComponent();

        // If you don't have an image yet, remove the Image line in XAML or add a placeholder png.

        // Fill picker
        AgePicker.Items.Add("13–17");
        for (int age = 18; age <= 80; age++)
            AgePicker.Items.Add(age.ToString());
    }

    void OnAgeChanged(object? sender, EventArgs e)
    {
        if (AgePicker.SelectedIndex < 0)
            return;

        var chosen = AgePicker.SelectedItem?.ToString();

        SelectedAge = chosen == "13–17" ? 15 : int.Parse(chosen!);

        ContinueButton.IsEnabled = true;
        ContinueButton.Opacity = 1;
    }

    async void OnContinueClicked(object sender, EventArgs e)
    {
        // Switch to the Shell (dashboard with tabs)
        Application.Current!.MainPage = new AppShell();

        // Give MAUI a moment to load Shell before navigating
        await Task.Yield();

        // Navigate to the Today tab as the new root
        await PageTransitionService.GoToAsync("//today");
    }

}

