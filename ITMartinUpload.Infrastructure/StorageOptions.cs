namespace ITMartinUpload.Infrastructure;

/// <summary>Where the customer folders and FileSorter's libraries are (Upload:Root / UploadRoot, Upload:LibraryRoot).</summary>
public sealed class StorageOptions
{
    public string Root { get; set; } = "/app/data";

    /// <summary>FileSorter's library folders, mounted read-only (status overview per customer).</summary>
    public string LibraryRoot { get; set; } = "/app/library";
}
