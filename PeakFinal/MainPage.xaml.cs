namespace Peak
{
    using System.ComponentModel.DataAnnotations;
    //using Peak.Shared.Helpers;

    public partial class MainPage : ContentPage
    {
        int count = 0;

        public MainPage()
        {
            InitializeComponent();

            // ✅ Test the shared library here
            //var ok = Validators.IsValidName("kibz");
            //TestLabel.Text = ok ? "Shared library works ✅" : "Shared library failed ❌";
        }

        private void OnCounterClicked(object sender, EventArgs e)
        {
            count++;

            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            else
                CounterBtn.Text = $"Clicked {count} times";

            SemanticScreenReader.Announce(CounterBtn.Text);
        }
    }
}