namespace Peak;

public static class GameAudioService
{
    public static bool BackgroundEnabled { get; set; } = true;
    public static bool EffectsEnabled { get; set; } = true;

    public static Task StartGameAtmosphereAsync(string gameId)
    {
        if (!BackgroundEnabled)
        {
            return Task.CompletedTask;
        }
        return Task.CompletedTask;
    }

    public static Task StopGameAtmosphereAsync()
    {
        return Task.CompletedTask;
    }

    public static Task PlayTapAsync()
    {
        if (!EffectsEnabled)
        {
            return Task.CompletedTask;
        }
        return Task.CompletedTask;
    }

    public static Task PlayWinAsync()
    {
        if (!EffectsEnabled)
        {
            return Task.CompletedTask;
        }
        return Task.CompletedTask;
    }
}
