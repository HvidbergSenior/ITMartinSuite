namespace ITMartinFileSorter.Domain.Enums
{
    // Top-level categories
    public enum MediaMainCategory
    {
        Audio = 0,
        Video = 1,
        Document = 2,
        Image = 3
    }

    // Type-specific subcategories
    public enum MediaSubCategory
    {
        // ---------------- AUDIO ----------------
        Music,
        VoiceMemo,
        UnknownAudio,

        // ---------------- VIDEO ----------------
        Movie,
        Clip,
        ScreenRecording,
        PhoneVideo,
        OtherVideo,
        UnknownVideo,

        // ---------------- DOCUMENT ----------------
        Pdf,
        Word,
        Excel,
        Text,
        Presentation,
        Csv,
        UnknownDocument,

        // ---------------- IMAGE ----------------
        Screenshot,
        Camera,
        PhonePhoto,
        OtherImage,
        UnknownImage,

        // ---------------- SOURCE ----------------
        Download,
        WhatsApp,
        Telegram,
        Meme,
        Social
    }

    // Tertiary categories: finer classification
    public enum MediaTertiaryCategory
    {
        // Location
        RegionMidtjylland,
        Jylland,
        Sjaelland,
        UdenforDenmark,
        UnknownLocation,

        // Device
        iPhone,
        Android,
        UnknownDevice,

        // Other
        Unknown
    }

    // File type
    public enum MediaType
    {
        Audio,
        Video,
        Document,
        Image
    }
}