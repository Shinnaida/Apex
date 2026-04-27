namespace Peak;

public sealed record AccessibilityOptions(
    bool LargeTextEnabled,
    bool HighContrastEnabled,
    bool ColorSafeChartsEnabled);

public static class AccessibilityService
{
    private const string LargeTextEnabledKey = "accessibility_large_text_enabled";
    private const string HighContrastEnabledKey = "accessibility_high_contrast_enabled";
    private const string ColorSafeChartsEnabledKey = "accessibility_color_safe_charts_enabled";

    private static readonly BindableProperty BaseFontSizeProperty = BindableProperty.CreateAttached(
        propertyName: "BaseFontSize",
        returnType: typeof(double),
        declaringType: typeof(AccessibilityService),
        defaultValue: -1d);

    public static AccessibilityOptions GetOptions()
    {
        return new AccessibilityOptions(
            LargeTextEnabled: IsLargeTextEnabled,
            HighContrastEnabled: IsHighContrastEnabled,
            ColorSafeChartsEnabled: IsColorSafeChartsEnabled);
    }

    public static bool IsLargeTextEnabled => Preferences.Default.Get(LargeTextEnabledKey, false);

    public static bool IsHighContrastEnabled => Preferences.Default.Get(HighContrastEnabledKey, false);

    public static bool IsColorSafeChartsEnabled => Preferences.Default.Get(ColorSafeChartsEnabledKey, false);

    public static void SetLargeTextEnabled(bool enabled)
    {
        Preferences.Default.Set(LargeTextEnabledKey, enabled);
    }

    public static void SetHighContrastEnabled(bool enabled)
    {
        Preferences.Default.Set(HighContrastEnabledKey, enabled);
    }

    public static void SetColorSafeChartsEnabled(bool enabled)
    {
        Preferences.Default.Set(ColorSafeChartsEnabledKey, enabled);
    }

    public static double GetTextScale()
    {
        return IsLargeTextEnabled ? 1.12 : 1.0;
    }

    public static void ApplyTextScale(Page page)
    {
        if (page is not ContentPage contentPage || contentPage.Content is null)
        {
            return;
        }

        ApplyTextScale(contentPage.Content, GetTextScale());
    }

    private static void ApplyTextScale(Element? element, double scale)
    {
        if (element is null)
        {
            return;
        }

        switch (element)
        {
            case Label label:
                ApplyScaledFont(label, label.FontSize, v => label.FontSize = v, scale);
                break;
            case Button button:
                ApplyScaledFont(button, button.FontSize, v => button.FontSize = v, scale);
                break;
            case Entry entry:
                ApplyScaledFont(entry, entry.FontSize, v => entry.FontSize = v, scale);
                break;
            case Editor editor:
                ApplyScaledFont(editor, editor.FontSize, v => editor.FontSize = v, scale);
                break;
            case SearchBar searchBar:
                ApplyScaledFont(searchBar, searchBar.FontSize, v => searchBar.FontSize = v, scale);
                break;
            case Picker picker:
                ApplyScaledFont(picker, picker.FontSize, v => picker.FontSize = v, scale);
                break;
            case DatePicker datePicker:
                ApplyScaledFont(datePicker, datePicker.FontSize, v => datePicker.FontSize = v, scale);
                break;
            case TimePicker timePicker:
                ApplyScaledFont(timePicker, timePicker.FontSize, v => timePicker.FontSize = v, scale);
                break;
        }

        foreach (var child in GetChildren(element))
        {
            ApplyTextScale(child, scale);
        }
    }

    private static void ApplyScaledFont(BindableObject target, double currentFontSize, Action<double> setFontSize, double scale)
    {
        if (currentFontSize <= 0)
        {
            return;
        }

        var baseSize = (double)target.GetValue(BaseFontSizeProperty);
        if (baseSize <= 0)
        {
            baseSize = currentFontSize;
            target.SetValue(BaseFontSizeProperty, baseSize);
        }

        setFontSize(Math.Round(baseSize * scale, 1));
    }

    private static IEnumerable<Element> GetChildren(Element element)
    {
        switch (element)
        {
            case ContentPage page when page.Content is Element pageContent:
                yield return pageContent;
                break;
            case ScrollView scrollView when scrollView.Content is Element scrollContent:
                yield return scrollContent;
                break;
            case ContentView contentView when contentView.Content is Element content:
                yield return content;
                break;
            case Border border when border.Content is Element borderContent:
                yield return borderContent;
                break;
            case Frame frame when frame.Content is Element frameContent:
                yield return frameContent;
                break;
        }

        if (element is Layout layout)
        {
            foreach (var child in layout.Children.OfType<Element>())
            {
                yield return child;
            }
        }
    }
}
