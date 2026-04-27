using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Peak;

public partial class ComparePage : ContentPage
{
    private CompareCategory _activeCategory;

    public ObservableCollection<CompareOptionItem> Options { get; } = new();

    public ComparePage()
    {
        InitializeComponent();
        BindingContext = this;

        var selection = CompareBenchmarkService.GetSelection();
        _activeCategory = selection.Category;
        ApplyTabState();
        LoadOptions(selection.Label);
    }

    async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await PageTransitionService.GoToAsync("..");
    }

    void OnAgeTabTapped(object sender, TappedEventArgs e)
    {
        if (_activeCategory == CompareCategory.Age)
        {
            return;
        }

        _activeCategory = CompareCategory.Age;
        ApplyTabState();
        LoadOptions(CompareBenchmarkService.GetSelection().Category == CompareCategory.Age
            ? CompareBenchmarkService.GetSelection().Label
            : string.Empty);
    }

    void OnProfessionTabTapped(object sender, TappedEventArgs e)
    {
        if (_activeCategory == CompareCategory.Profession)
        {
            return;
        }

        _activeCategory = CompareCategory.Profession;
        ApplyTabState();
        LoadOptions(CompareBenchmarkService.GetSelection().Category == CompareCategory.Profession
            ? CompareBenchmarkService.GetSelection().Label
            : string.Empty);
    }

    async void OnOptionTapped(object sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bindable ||
            bindable.BindingContext is not CompareOptionItem option)
        {
            return;
        }

        CompareBenchmarkService.SaveSelection(_activeCategory, option.Label);
        UpdateSelectedOption(option.Label);
        await Task.Delay(120);
        await PageTransitionService.GoToAsync("..");
    }

    private void ApplyTabState()
    {
        AgeTabLabel.TextColor = _activeCategory == CompareCategory.Age
            ? Colors.White
            : Color.FromArgb("#D8F4FF");

        ProfessionTabLabel.TextColor = _activeCategory == CompareCategory.Profession
            ? Colors.White
            : Color.FromArgb("#D8F4FF");

        Grid.SetColumn(TabUnderline, _activeCategory == CompareCategory.Age ? 0 : 1);
    }

    private void LoadOptions(string selectedLabel)
    {
        Options.Clear();

        var items = _activeCategory == CompareCategory.Age
            ? CompareBenchmarkService.GetAgeGroups()
            : CompareBenchmarkService.GetProfessions();

        var fallback = items.FirstOrDefault() ?? string.Empty;
        var effectiveSelection = string.IsNullOrWhiteSpace(selectedLabel) ? fallback : selectedLabel;

        foreach (var item in items)
        {
            Options.Add(new CompareOptionItem(item, string.Equals(item, effectiveSelection, StringComparison.Ordinal)));
        }

        OptionsCollection.ItemsSource = Options;
    }

    private void UpdateSelectedOption(string selectedLabel)
    {
        foreach (var option in Options)
        {
            option.IsSelected = string.Equals(option.Label, selectedLabel, StringComparison.Ordinal);
        }
    }
}

public sealed class CompareOptionItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public CompareOptionItem(string label, bool isSelected)
    {
        Label = label;
        _isSelected = isSelected;
    }

    public string Label { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
