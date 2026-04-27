using System;
using Microsoft.Extensions.Logging;

namespace Peak
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            AppContext.SetSwitch("System.Net.Http.EnableActivityPropagation", false);
            AppContext.SetSwitch("System.Net.Http.DisableDiagnostics", true);
            AppContext.SetSwitch("System.Diagnostics.Metrics.Meter.IsSupported", false);

            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}