namespace ITMartinFileSorter.Domain.Enums
{
    // =========================
    // MAIN CATEGORY
    // =========================
    public enum MediaMainCategory
    {
        Audio = 0,
        Video = 1,
        Document = 2,
        Image = 3
    }

    // =========================
    // SUB CATEGORY (TYPE-SPECIFIC)
    // =========================
    public enum MediaSubCategory
    {
        // ---------- AUDIO ----------
        Music,
        VoiceMemo,
        UnknownAudio,

        // ---------- VIDEO ----------
        Movie,
        Clip,
        ScreenRecording,
        PhoneVideo,
        OtherVideo,
        UnknownVideo,

        // ---------- DOCUMENT ----------
        Pdf,
        Word,
        Excel,
        Text,
        Presentation,
        Csv,
        UnknownDocument,
        
        // ---------- IMAGE ----------
        Screenshot,
        Camera,
        PhonePhoto,
        Social,
        Meme,
        OtherImage,
        UnknownImage
    }

    // =========================
    // TERTIARY CATEGORY
    // =========================
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

    // =========================
    // FILE TYPE
    // =========================
    public enum MediaType
    {
        Audio,
        Video,
        Document,
        Image
    }
}