using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DriveFile = Google.Apis.Drive.v3.Data.File;
using static EasyDriveFilesManager.DriveMimeTypes;
using Google.Apis.Drive.v3;
using Microsoft.AspNetCore.Http;
using Google.Apis.Upload;
using Google.Apis.Download;

namespace EasyDriveFilesManager;

public static class DriveServiceExtensions
{
    public static async Task<string> UploadFile(this DriveService driveService, IFormFile formFile, string fileDescription, string parentFolderId)
    {
        var filePath = Path.GetFileName(formFile.FileName);
        try
        {
            using (var file = new FileStream(filePath, FileMode.OpenOrCreate))
            {
                formFile.CopyTo(file);

                var driveFile = new DriveFile
                {
                    Name = formFile.FileName,
                    Description = fileDescription,
                    MimeType = GetMime(Path.GetExtension(formFile.FileName)),
                    Parents = new string[] { parentFolderId },
                };

                var request = driveService.Files.Create(driveFile, file, driveFile.MimeType);
                request.Fields = "*";
                var results = await request.UploadAsync(CancellationToken.None);

                if (results.Status == UploadStatus.Failed)
                {
                    Console.WriteLine($"Error uploading file: {results.Exception.Message}");
                    return null;
                }
                var id = request.ResponseBody?.Id;

                file.Close();

                return id;
            }
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    public static async Task<string> UploadFolder(this DriveService driveService, string sourcePath, string parentFolderId) 
    {
        
    }

    public static void DownloadAllFiles(this DriveService driveService, string folderId, string path)
        => driveService.DownloadFolderCore(folderId, path, true);

    public static void DownloadFolder(this DriveService driveService, string folderId, string path)
        => driveService.DownloadFolderCore(folderId, path, false);

    public static MemoryStream DownloadFile(this DriveService driveService, string fileId)
    {
        try
        {
            var request = driveService.Files.Get(fileId);
            var stream = new MemoryStream();

            request.MediaDownloader.ProgressChanged +=
                progress =>
                {
                    switch (progress.Status)
                    {
                        case DownloadStatus.Downloading:
                            {
                                Console.WriteLine(progress.BytesDownloaded);
                                break;
                            }
                        case DownloadStatus.Completed:
                            {
                                Console.WriteLine("Download complete.");
                                break;
                            }
                        case DownloadStatus.Failed:
                            {
                                Console.WriteLine("Download failed.");
                                break;
                            }
                    }
                };
            request.Download(stream);

            return stream;
        }
        catch (Exception e)
        {
            // TODO(developer) - handle error appropriately
            if (e is AggregateException)
            {
                Console.WriteLine("Credential Not found");
            }
            else
            {
                throw;
            }
        }
        return null;
    }

    private static List<DriveFile> GetByFolderId(this DriveService driveService, string folderId)
    {
        try
        {
            var service = driveService;
            var fileList = service.Files.List();

            fileList.Q = $"'{folderId}' in parents";
            fileList.Fields = "nextPageToken, files(id, name, size, mimeType)";

            var result = new List<DriveFile>();
            string pageToken = null;
            do
            {
                fileList.PageToken = pageToken;
                var filesResult = fileList.Execute();
                var files = filesResult.Files;
                pageToken = filesResult.NextPageToken;
                result.AddRange(files);
            } while (pageToken != null);

            return result;
        }
        catch (Exception)
        {

            throw;
        }
    }

    private static DriveFile GetById(this DriveService driveService, string fileId)
    {
        try
        {
            return driveService.Files.Get(fileId).Execute();
        }
        catch (Exception)
        {
            throw;
        }
    }

    private static void DownloadFolderCore(this DriveService driveService, string folderId, string path, bool downloadFilesOnly)
    {
        var file = driveService.GetById(folderId);
        if (file == null)
            throw new ArgumentNullException(nameof(file));

        using (var zipArchive = new ZipArchive(File.Create(Path.Combine(path, $"{file.Name}.zip")), ZipArchiveMode.Create))
        {
            driveService.DownloadFolderCore(folderId, zipArchive, "", downloadFilesOnly);
        }
    }

    private static void DownloadFolderCore(this DriveService service, string folderId, ZipArchive zipArchive, string folderPath, bool downloadFilesOnly)
    {
        var items = service.GetByFolderId(folderId);
        var (subFolders, files) = items.Split(x => x.MimeType == "application/vnd.google-apps.folder");

        foreach (var file in files)
        {
            var fullName = folderPath + file.Name + GetExtension(file.MimeType);
            var fileStream = DownloadFile(service, file.Id);

            ZipArchiveEntry entry = zipArchive.CreateEntry(fullName);
            using (Stream zipStream = entry.Open())
            {
                fileStream.Position = 0;
                fileStream.CopyTo(zipStream);
            }
        }

        foreach (var subfolder in subFolders)
        {
            if (!downloadFilesOnly)
            {
                var fullName = folderPath + subfolder.Name + GetExtension(subfolder.MimeType);
                ZipArchiveEntry entry = zipArchive.CreateEntry(fullName);
            }

            service.DownloadFolderCore(subfolder.Id, zipArchive, downloadFilesOnly ? "" : folderPath + subfolder.Name + "/", downloadFilesOnly);
        }
    }

}
