namespace EasyDriveFilesManager;

internal static class DriveMimeTypes
{
    private static readonly Dictionary<string, string> driveMimes = new (StringComparer.InvariantCultureIgnoreCase)
    {
        { "/", "application/vnd.google-apps.folder" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".odt", "application/vnd.oasis.opendocument.text" },
        { ".rtf", "application/rtf" },
        { ".pdf", "application/pdf" },
        { ".txt", "text/plain" },
        { ".zip", "application/zip" },
        { ".epub", "application/epub+zip" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { ".ods", "application/x-vnd.oasis.opendocument.spreadsheet" },
        { ".csv", "text/csv" },
        { ".tsv", "text/tab-separated-values" },
        { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
        { ".odp", "application/vnd.oasis.opendocument.presentation" },
        { ".jpg", "image/jpeg" },
        { ".png", "image/png" },
        { ".svg", "image/svg+xml" },
        { ".json", "application/vnd.google-apps.script+json" },
    };

    /// TODO "Two Way Dictionary"
    private static Dictionary<string, string> DriveMimesReversed
        => driveMimes.ToDictionary(x => x.Value, x => x.Key);

    internal static string GetMime(string key)
    {
        if(driveMimes.TryGetValue(key, out var mime))
            return mime;
       
        return "*/*";
    }

    internal static string GetExtension(string key)
    {
        if (DriveMimesReversed.TryGetValue(key, out var extension))
            return extension;

        return "";
    }
}