namespace ITMartin.Media.Contracts.Contracts.Runtime.Enums
{
    // =========================
    // MAIN CATEGORY
    // =========================
    public enum MediaMainCategory
    {
        Audio = 0,
        Video = 1,
        Document = 2,
        Image = 3,

        // Unrecognized file type - still gets a home (Unhandled/{year}/{month})
        // instead of being silently dropped during discovery, so Package3 has
        // something to polish/reclassify later.
        Other = 4
    }

}