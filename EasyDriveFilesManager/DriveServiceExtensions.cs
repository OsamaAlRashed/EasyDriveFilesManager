using Google.Apis.Drive.v3;
using Google.Apis.Upload;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static EasyDriveFilesManager.DriveMimeTypes;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace EasyDriveFilesManager
{
    public static class DriveServiceExtensions
    {
        public static async Task<Result<string>> UploadFileAsync(this DriveService driveService, IFormFile formFile, string fileDescription, string parentFolder)
            => await driveService.UploadFileAsync(formFile, fileDescription, new string[] { parentFolder });

        public static async Task<Result<string>> UploadFileAsync(this DriveService driveService, IFormFile formFile, string fileDescription, string[] parentFolders)
        {
            var filePath = Path.GetFileName(formFile.FileName);
            try
            {
                using var file = new FileStream(filePath, FileMode.OpenOrCreate);
                formFile.CopyTo(file);

                var driveFile = new DriveFile
                {
                    Name = formFile.FileName,
                    Description = fileDescription,
                    MimeType = GetMime(Path.GetExtension(formFile.FileName)),
                    Parents = parentFolders,
                };

                var request = driveService.Files.Create(driveFile, file, driveFile.MimeType);
                request.Fields = "*";
                var results = await request.UploadAsync(CancellationToken.None);

                if (results.Status == UploadStatus.Failed)
                {
                    return Result.Failed<string>($"Error uploading file: {results.Exception.Message}");
                }

                file.Close();

                return request.ResponseBody.Id;
            }
            catch (Exception ex)
            {
                return Result.Failed<string>(ex);
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        public static async Task<Result<List<string>>> UploadFilesAsync(this DriveService driveService, List<IFormFile> formFiles, string parentFolder)
            => await driveService.UploadFilesAsync(formFiles, new string[] { parentFolder });

        public static async Task<Result<List<string>>> UploadFilesAsync(this DriveService driveService, List<IFormFile> formFiles, string[] parentFolders)
        {
            var tasks = new List<Task<Result<string>>>();
            foreach (var formFile in formFiles)
            {
                tasks.Add(driveService.UploadFileAsync(formFile, "", parentFolders));
            }

            var results = await Task.WhenAll(tasks);

            return Result.Aggregate(results.ToList());
        }

        public static Result<string> CreateFolder(this DriveService driveService, string folderName, string parentFolder)
            => driveService.CreateFolder(folderName, new string[] { parentFolder });

        public static Result<string> CreateFolder(this DriveService driveService, string folderName, string[] parentFolders)
        {
            try
            {
                var driveFolder = new Google.Apis.Drive.v3.Data.File
                {
                    Name = folderName,
                    MimeType = "application/vnd.google-apps.folder",
                    Parents = parentFolders
                };

                var file = driveService.Files.Create(driveFolder).Execute();
                return file.Id;
            }
            catch (Exception ex)
            {
                return Result.Failed<string>(ex);
            }
        }

        public static void DownloadAllFiles(this DriveService driveService, string folderId, string path)
            => driveService.DownloadAllFiles(folderId, path, null);

        public static void DownloadAllFiles(this DriveService driveService, string folderId, string path, string? name)
            => driveService.DownloadAllFiles(folderId, path, name, int.MaxValue);

        public static void DownloadAllFiles(this DriveService driveService, string folderId, string path, string? name, int depth)
            => driveService.DownloadFolderCore(folderId, path, downloadFilesOnly: true, name, depth);

        public static void DownloadFolder(this DriveService driveService, string folderId, string path)
           => driveService.DownloadFolder(folderId, path, null);

        public static void DownloadFolder(this DriveService driveService, string folderId, string path, string? name)
            => driveService.DownloadFolder(folderId, path, name, int.MaxValue);

        public static void DownloadFolder(this DriveService driveService, string folderId, string path, string? name, int depth)
            => driveService.DownloadFolderCore(folderId, path, downloadFilesOnly: false, name, depth);

        public static Result<MemoryStream> DownloadFile(this DriveService driveService, string fileId)
        {
            try
            {
                var file = driveService.GetById(fileId);
                if (file == null)
                    return Result.Failed<MemoryStream>("File not exist");

                var request = driveService.Files.Get(fileId);
                var stream = new MemoryStream();

                request.Download(stream);

                return stream;
            }
            catch (Exception ex)
            {
                return Result.Failed<MemoryStream>(ex);
            }
        }

        #region Private
        private static List<DriveFile> GetByFolderId(this DriveService driveService, string folderId)
        {
            try
            {
                var service = driveService;
                var fileList = service.Files.List();

                fileList.Q = $"'{folderId}' in parents";
                fileList.Fields = "nextPageToken, files(id, name, size, mimeType)";

                var result = new List<DriveFile>();
                string? pageToken = null;
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
            catch
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

        private static void DownloadFolderCore(this DriveService driveService, string folderId, string path, bool downloadFilesOnly, string? name, int depth)
        {
            var file = driveService.GetById(folderId);
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            if (depth <= 0)
                throw new ArgumentOutOfRangeException(nameof(depth));

            if (string.IsNullOrEmpty(name))
                name = file.Name;

            using (var zipArchive = new ZipArchive(File.Create(Path.Combine(path, $"{name}.zip")), ZipArchiveMode.Create))
            {
                driveService.DownloadFolderCore(folderId, zipArchive, "", downloadFilesOnly, depth);
            }
        }

        private static void DownloadFolderCore(this DriveService service, string folderId, ZipArchive zipArchive, string folderPath, bool downloadFilesOnly, int depth)
        {
            var items = service.GetByFolderId(folderId);
            var (subFolders, files) = items.Split(x => x.MimeType == "application/vnd.google-apps.folder");

            foreach (var file in files)
            {
                var fullName = folderPath + file.Name + GetExtension(file.MimeType);
                var fileStream = DownloadFile(service, file.Id).Data;
                if (fileStream == null) continue;

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
                if (depth-- > 0)
                {
                    service.DownloadFolderCore(subfolder.Id, zipArchive, downloadFilesOnly ? "" : folderPath + subfolder.Name + "/", downloadFilesOnly, depth);
                }
            }
        }
        #endregion
    }
}