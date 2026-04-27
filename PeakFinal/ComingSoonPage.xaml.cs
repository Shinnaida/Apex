namespace Peak;

public partial class ComingSoonPage : ContentPage
{
    public ComingSoonPage(string title)
    {
        InitializeComponent();
        TitleLabel.Text = title + "\n(Coming Soon)";
    }
}
