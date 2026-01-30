using CommunityToolkit.Maui;
using Gallery.App.Services;
using Gallery.App.ViewModels;
using Gallery.App.Views;
using Gallery.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Gallery.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register infrastructure services
        builder.Services.AddGalleryInfrastructure();

        // Register app services (singletons for state)
        builder.Services.AddSingleton<SelectionService>();
        builder.Services.AddSingleton<PrefetchService>();

        // Register ViewModels
        builder.Services.AddTransient<MainViewModel>();

        // Register Pages and Views
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<QuickPreviewOverlay>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
