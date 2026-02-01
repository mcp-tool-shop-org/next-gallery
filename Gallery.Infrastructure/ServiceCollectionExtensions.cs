using Gallery.Application.Interfaces;
using Gallery.Infrastructure.Data;
using Gallery.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Gallery.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGalleryInfrastructure(this IServiceCollection services, string? databasePath = null)
    {
        var dbPath = databasePath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NextGallery",
                "gallery.db");

        // Database
        services.AddSingleton(new GalleryDatabase(dbPath));

        // Stores
        services.AddSingleton<ILibraryStore, LibraryStore>();
        services.AddSingleton<IMediaItemStore, MediaItemStore>();
        services.AddSingleton<IThumbJobStore, ThumbJobStore>();

        // Services
        services.AddSingleton<IThumbCache, ThumbCache>();
        services.AddSingleton<IThumbGenerator, ThumbGenerator>();
        services.AddSingleton<IItemIndexService, ItemIndexService>();

        // Video thumbnail extraction (optional - works when FFmpeg is available)
        services.AddSingleton<FfmpegLocator>();
        services.AddSingleton<IVideoThumbExtractor, FfmpegVideoThumbExtractor>();

        // Thumb worker with video support
        services.AddSingleton<ThumbWorker>(sp => new ThumbWorker(
            sp.GetRequiredService<IThumbJobStore>(),
            sp.GetRequiredService<IMediaItemStore>(),
            sp.GetRequiredService<IThumbCache>(),
            sp.GetRequiredService<IThumbGenerator>(),
            sp.GetService<IVideoThumbExtractor>(),
            maxConcurrency: 2
        ));

        return services;
    }
}
