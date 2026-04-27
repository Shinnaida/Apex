using System.Collections.ObjectModel;

namespace Peak;

public partial class TrainingPickerPage : ContentPage
{
    public ObservableCollection<TrainingSlide> Slides { get; } = new()
{
    new TrainingSlide(
        Emoji: "??",
        CircleColor: "#5B8DEF",
        Title: "Games to\nremember",
        Question: "Would you like to train your memory?",
        DescA: "Boost your memory by playing games such as Wizard, which we developed with ",
        BoldA: "Cambridge University",
        DescB: ".",
        BoldB: "",
        DescC: "",
        Destination: TrainingDestination.Memory
    ),

    new TrainingSlide(
        Emoji: "??",
        CircleColor: "#7E57C2",
        Title: "More than words",
        Question: "Would you like to train your language skills?",
        DescA: "A 2019 study by ",
        BoldA: "The City University of New York",
        DescB: " showed that our language games improved ",
        BoldB: "cognitive abilities",
        DescC: " overall and helped reduce symptoms of depression.",
        Destination: TrainingDestination.Language
    ),

    new TrainingSlide(
        Emoji: "??",
        CircleColor: "#43A047",
        Title: "Figure it out",
        Question: "Would you like to train your problem solving skills?",
        DescA: "Problem solving games challenge your logic. Studies show arithmetic training can ",
        BoldA: "improve",
        DescB: " how fast you process ",
        BoldB: "information",
        DescC: ".",
        Destination: TrainingDestination.ProblemSolving
    ),

    new TrainingSlide(
        Emoji: "???",
        CircleColor: "#EF5350",
        Title: "Get focused",
        Question: "Would you like to train your focus?",
        DescA: "Focus games train your concentration. Studies show that training your focus can ",
        BoldA: "improve",
        DescB: " cognitive ",
        BoldB: "performance",
        DescC: ".",
        Destination: TrainingDestination.Focus
    ),

    new TrainingSlide(
        Emoji: "??",
        CircleColor: "#AB47BC",
        Title: "Hello happiness",
        Question: "Would you like to train your emotional skills?",
        DescA: "Our emotion games encourage you to focus on positive information. Researchers at ",
        BoldA: "UCL",
        DescB: " showed that our game Smile On Me ",
        BoldB: "improved",
        DescC: " players' mood.",
        Destination: TrainingDestination.Emotion
    ),
};


    int _position = 0;

    public TrainingPickerPage()
    {
        InitializeComponent();
        BindingContext = this;
        UpdateQuestion(0);
    }

    void OnPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        _position = e.CurrentPosition;
        UpdateQuestion(_position);
    }

    void UpdateQuestion(int pos)
    {
        if (pos < 0 || pos >= Slides.Count) return;
        QuestionLabel.Text = Slides[pos].Question;
    }

    async void OnNoClicked(object sender, EventArgs e)
    {
        // Professional behavior:
        // "No" just moves to the next category, and on the last one it exits to Home/Game list.
        if (_position < Slides.Count - 1)
        {
            TrainingCarousel.ScrollTo(_position + 1, position: ScrollToPosition.Center, animate: true);
            return;
        }

        // If last slide and they said No, just go to a default page.
        await PageTransitionService.PushAsync(Navigation, () => new MemoryGamePage());
    }

    //async void OnYesClicked(object sender, EventArgs e)
    //{
    //    var slide = Slides[_position];

    //    IQCategory iqCategory = slide.Destination switch
    //    {
    //        TrainingDestination.Memory => IQCategory.Spatial,
    //        TrainingDestination.Language => IQCategory.Verbal,
    //        TrainingDestination.ProblemSolving => IQCategory.LogicMath,
    //        TrainingDestination.Focus => IQCategory.Abstract,
    //        TrainingDestination.Emotion => IQCategory.Verbal, // until you add Emotion category
    //        _ => IQCategory.LogicMath
    //    };

    //    await PageTransitionService.PushAsync(Navigation, new GamePlayPage(iqCategory, count: 10));
    //}
    async void OnYesClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PushAsync(Navigation, new TestYourselfPage(IQCatalog.GeneralChallenge));
    }



}

public enum TrainingDestination
{
    Memory,
    Language,
    ProblemSolving,
    Focus,
    Emotion
}

public record TrainingSlide(
    string Emoji,
    string CircleColor,
    string Title,
    string Question,
    string DescA,
    string BoldA,
    string DescB,
    string BoldB,
    string DescC,
    TrainingDestination Destination
);

