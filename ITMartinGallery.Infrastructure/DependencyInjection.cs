using ITMartinGallery.Application;
using ITMartinGallery.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinGallery.Infrastructure;

// Gallery:DataDir = the writable folder (birthday page edits); StudioDb = Musik Studio's database.
public sealed record GalleryOptions(string DataDir, string StudioDb);

public static class DependencyInjection
{
    /// <summary>The galleries from Galleries__N__* (magic.env) - every entry, served or only closed.</summary>
    public static GalleryCatalog ReadGalleries(IConfiguration config) => new(
        config.GetSection("Galleries").GetChildren().Select(s => new Gallery(
            Slug: s["Slug"] ?? "",
            Name: s["Name"] ?? "",
            Path: s["Path"] ?? "",
            Password: s["Password"],
            ShowSummary: s.GetValue<bool>("ShowSummary"),
            HideScreenshots: s.GetValue<bool>("HideScreenshots"),
            OnThisDayEnabled: s.GetValue<bool>("OnThisDayEnabled"),
            SearchEnabled: s.GetValue<bool>("SearchEnabled"),
            HideAddons: s.GetValue<bool>("HideAddons"),
            CoreCategoriesOnly: s.GetValue<bool>("CoreCategoriesOnly"),
            ViewOnly: s.GetValue<bool>("ViewOnly"),
            Theme: s["Theme"], Headline: s["Headline"], Tagline: s["Tagline"],
            CelebrationFolder: s["CelebrationFolder"], CelebrationName: s["CelebrationName"], BirthDate: s["BirthDate"],
            EditPin: s["EditPin"], MusicName: s["MusicName"], RootFolders: s["RootFolders"], UploadUrl: s["UploadUrl"],
            ClosesAt: DateTime.TryParse(s["ClosesAt"], out var at) ? at : null,
            Concerts: s.GetValue<bool>("Concerts"))));

    /// <summary>The disk, manifest, collections, studio.db, TagLib, EXIF and edit-file adapters behind the ports, plus the use cases.</summary>
    public static IServiceCollection AddGallery(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(ReadGalleries(config));
        services.AddSingleton(new GalleryOptions(config["Gallery:DataDir"] ?? "/data", config["StudioDb"] ?? "/studio/studio.db"));
        services.AddSingleton<ILibraryDisk, LibraryDisk>();
        services.AddSingleton<IManifests, ManifestReader>();
        services.AddSingleton<ICollections, CollectionsFile>();
        services.AddSingleton<ISongSheetSource, StudioSongSheets>();
        services.AddSingleton<IEmbeddedCovers, TagLibCovers>();
        services.AddSingleton<ICaptureDates, ExifCaptureDates>();
        services.AddSingleton<ICelebrationEdits, CelebrationEditsFile>();
        services.AddSingleton<LibraryLinks>();
        services.AddSingleton<PeopleIndex>();
        services.AddSingleton<FolderBrowser>();
        services.AddSingleton<LibrarySummary>();
        services.AddSingleton<CollectionsView>();
        services.AddSingleton<ManifestViews>();
        services.AddSingleton<SongSheets>();
        services.AddSingleton<LibraryFiles>();
        services.AddSingleton<Celebrations>();
        services.AddSingleton<Concerts>();
        return services;
    }
}
