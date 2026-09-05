using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Helpers;

public static class CategoryHelper
{
    // Danish folder names as of 2026-08-20 (user preference, code default for
    // future QuickSort sorts only - existing already-sorted libraries are NOT
    // renamed, see feedback_danish_folder_names memory). LivePhotos is kept
    // untranslated (Apple's own feature name, not a natural Danish word).
    public static string GetCategory(MediaFile file) =>
        file.SubCategory == MediaSubCategory.Screenshot ? "Skærmbilleder" :
        file.SubCategory == MediaSubCategory.LivePhotoVideo ? "LivePhotos" :
        file.SubCategory == MediaSubCategory.Meme ? "Memes" :
        file.SubCategory == MediaSubCategory.Gif ? "Gifs" :
        file.SubCategory == MediaSubCategory.Movie ? "Film" :
        file.SubCategory == MediaSubCategory.Chat ? "Chat" :
        file.SubCategory == MediaSubCategory.AlbumArt ? "Musik" :
        file.MainCategory switch
        {
            MediaMainCategory.Image => "Billeder",
            MediaMainCategory.Video => "Videoer",
            MediaMainCategory.Document => "Dokumenter",
            MediaMainCategory.Audio => "Musik",
            MediaMainCategory.Other => "Ikke_identificeret",
            _ => "Ikke_identificeret"
        };
}