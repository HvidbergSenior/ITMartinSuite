namespace ITMartin.Media.Contracts.Contracts.Runtime.Enums
{
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
        LivePhotoVideo,
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
        UnknownImage,

        // ---------- UNHANDLED (unrecognized file type) ----------
        UnknownOther
    }

}