using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyDriveFilesManager;

internal static class DriveMimeTypes
{
    private static bool _reversed = false;

    private static Dictionary<string, string> _driveMimes = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
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

    private static Dictionary<string, string> _driveMimesReversed 
    { 
        get {
            if(!_reversed)
                return _driveMimes.ToDictionary(x => x.Value, x => x.Key);

            _reversed = true;
            return _driveMimesReversed;
        }
    }

    internal static string GetMime(string key)
    {
        if(_driveMimes.TryGetValue(key, out var mime))
            return mime;
       
        return "*/*";
    }

    internal static string GetExtension(string key)
    {
        if (_driveMimesReversed.TryGetValue(key, out var extension))
            return extension;

        return "";
    }
}
