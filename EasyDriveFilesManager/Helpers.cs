using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace EasyDriveFilesManager
{
    internal static class Helpers
    {
        internal static (List<T>, List<T>) Split<T>(this List<T> source, Func<T, bool> predicate)
        {
            var leftSide = new List<T>();
            var rightSide = new List<T>();
            foreach (var item in source)
            {
                if (predicate(item))
                {
                    leftSide.Add(item);
                }
                else
                {
                    rightSide.Add(item);
                }
            }
            return (leftSide, rightSide);
        }

        internal static void SaveStream(MemoryStream memoryStream, string path, string name)
        {
            using FileStream fileStream = new FileStream(Path.Combine(path, $"{name}.zip"), FileMode.Create, FileAccess.Write);
            memoryStream.WriteTo(fileStream);
        }

        internal static IFormFile MemoryStreamToIFormFileAsZip(DriveFile folder, MemoryStream memoryStream) 
            => new FormFile(memoryStream, 0, memoryStream.Length, "Data", $"{folder.Name}.zip")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/zip",
        };
    }
}
