namespace Peak;

public static class InteractionEffects
{
    public static async Task AnimateTapAsync(VisualElement element)
    {
        if (element is null)
        {
            return;
        }

        element.AbortAnimation("TapPulse");
        await element.ScaleTo(0.97, 70, Easing.CubicOut);
        await element.ScaleTo(1, 110, Easing.CubicIn);
    }

    public static async Task AnimateEntranceAsync(VisualElement element, uint duration = 180)
    {
        if (element is null)
        {
            return;
        }

        element.Opacity = 0;
        element.TranslationY = 12;

        await Task.WhenAll(
            element.FadeTo(1, duration, Easing.CubicOut),
            element.TranslateTo(0, 0, duration, Easing.CubicOut));
    }
}
