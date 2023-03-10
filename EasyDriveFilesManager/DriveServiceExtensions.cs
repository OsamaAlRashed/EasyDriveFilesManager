using Google.Apis.Drive.v3;
using Google.Apis.Upload;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
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
    /// <summary>
    /// Extension methods for <code>Drive service</code> class in Google.Apis.Drive.v3 
    /// </summary>
    public static class DriveServiceExtensions
    {
        #region Zip Folder

        /// <summary>
        /// Compresses folder to zip file
        /// </summary>
        /// <param name="driveService">The drive service.</param>
        /// <param name="folderId">Drive folder id</param>
        /// <returns>Returns result object with file drive id if the operation succeeded.</returns>
        public static async Task<Result<string>> CompressFolderAsync(this DriveService driveService, string folderId)
        {
            var folder = driveService.GetById(folderId);
            if(folder == null)
            {
                return Result.Failed<string>("Folder not exist.");
            }

            var downloadAsStream = driveService.DownloadFolder(folderId);
            if (!downloadAsStream.IsSucceded)
            {
                return Result.Failed<string>(downloadAsStream.Message);
            }

            var memoryStream = downloadAsStream.Data;
            var file = new FormFile(memoryStream, 0, memoryStream.Length, "Data", $"{folder.Name}.zip")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/zip",
            };

            return await driveService.UploadFileAsync(file, "", folder.Parents.ToArray());
        }
        #endregion

        #region Upload File(s)
        /// <summary>
        /// Uploads file to drive
        /// </summary>
        /// <param name="driveService">The drive service.</param>
        /// <param name="formFile">The file that we need upload it.</param>
        /// <param name="fileDescription">Description of file</param>
        /// <param name="parentFolder">Parent folder id in drive</param>
        /// <returns>Returns result object with file drive id if the operation succeeded.</returns>
        public static async Task<Result<string>> UploadFileAsync(this DriveService driveService, IFormFile formFile, string fileDescription, string parentFolder)
            => await driveService.UploadFileAsync(formFile, fileDescription, new string[] { parentFolder });

        /// <summary>
        /// Uploads file to drive
        /// </summary>
        /// <param name="driveService">The drive service.</param>
        /// <param name="formFile">The file that we need upload it.</param>
        /// <param name="fileDescription">Description of file</param>
        /// <param name="parentFolders">Parent folders in drive</param>
        /// <returns>Returns result object with file drive id if the operation succeeded.</returns>
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

        /// <summary>
        /// Uploads file to drive
        /// </summary>
        /// <param name="driveService">The drive service.</param>
        /// <param name="formFiles">The files that we need upload them.</param>
        /// <param name="parentFolder">Parent folder id in drive</param>
        /// <returns>Returns result object with file drive id if the operation succeeded.</returns>
        public static async Task<Result<List<string>>> UploadFilesAsync(this DriveService driveService, List<IFormFile> formFiles, string parentFolder)
            => await driveService.UploadFilesAsync(formFiles, new string[] { parentFolder });

        /// <summary>
        /// Uploads file to drive
        /// </summary>
        /// <param name="driveService">The drive service.</param>
        /// <param name="formFiles">The files that we need upload them.</param>
        /// <param name="parentFolders">Parent folders in drive</param>
        /// <returns>Returns result object with file drive id if the operation succeeded.</returns>
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

        #endregion

        #region Create Folder

        /// <summary>
        /// Create a folder in drive
        /// </summary>
        /// <param name="driveService">The drive service</param>
        /// <param name="folderName">The name of folder</param>
        /// <param name="parentFolder">Parent drive id for the new folder.</param>
        /// <returns>Returns result object with folder drive id if the operation succeeded.</returns>
        public static Result<string> CreateFolder(this DriveService driveService, string folderName, string parentFolder)
            => driveService.CreateFolder(folderName, new string[] { parentFolder });

        /// <summary>
        /// Create a folder in drive
        /// </summary>
        /// <param name="driveService">The drive service</param>
        /// <param name="folderName">The name of folder</param>
        /// <param name="parentFolders">Parent drive ids for the new folder.</param>
        /// <returns>Returns result object with folder drive id if the operation succeeded.</returns>
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

        #endregion

        #region Download All Files

        /// <summary>
        /// Downloads only the files of a folder to specific path. 
        /// </summary>
        /// <param name="driveService">The drive service</param>
        /// <param name="folderId">Drive folder id</param>
        /// <param name="path">The path that the file save it.</param>
        /// <returns>Returns result object with true if the operation succeeded</returns>
        public static Result<bool> DownloadAllFiles(this DriveService driveService, string folderId, string path)
            => driveService.DownloadAllFiles(folderId, path);

        /// <summary>
        /// Downloads only the files of a folder to specific path. 
        /// </summary>
        /// <param name="driveService">The drive service</param>
        /// <param name="folderId">Drive folder id</param>
        /// <param name="path">The path that the file save it.</param>
        /// <param name="name">The name of a zip file.</param>
        /// <returns>Returns result object with true if the operation succeeded</returns>
        public static Result<bool> DownloadAllFiles(this DriveService driveService, string folderId, string path, string name)
            => driveService.DownloadAllFiles(folderId, path, name, int.MaxValue);

        /// <summary>
        /// Downloads only the files of a folder to specific path. 
        /// </summary>
        /// <param name="driveService">The drive service</param>
        /// <param name="folderId">Drive folder id</param>
        /// <param name="path">The path that the file save it.</param>
        /// <param name="depth">The depth of search.</param>
        /// <returns>Returns result object with true if the operation succeeded</returns>
        public static Result<bool> DownloadAllFiles(this DriveService driveService, string folderId, string path, int depth)
            => driveService.DownloadAllFiles(folderId, path, null, depth);

        /// <summary>
        /// Downloads only the files of a folder to specific path. 
        /// </summary>
        /// <param name="driveService">The drive service</param>
        /// <param name="folderId">Drive folder id</param>
        /// <param name="path">The path that the file save it.</param>
        /// <param name="name">The name of a zip file.</param>
        /// <param name="depth">The depth of search.</param>
        /// <returns>Returns result object with true if the operation succeeded</returns>
        public static Result<bool> DownloadAllFiles(this DriveService driveService, string folderId, string path, string name, int depth)
        {
            var file = driveService.GetById(folderId);
            if (file == null)
                return Result.Failed<bool>("File not exist.");

            var downloadResult = driveService.DownloadAllFiles(folderId, depth);
            if (!downloadResult.IsSucceded)
            {
                return Result.Failed<bool>(downloadResult.Message);
            }

            var memoryStream = downloadResult.Data;
            var fileName = name ?? file.Name;

            SaveStream(memoryStream, path, fileName);

            return true;
        }

        /// <summary>
        /// Downloads only the files of a folder as memory stream. 
        /// </summary>
        /// <param name="driveService">The drive service</param>
        /// <param name="folderId">Drive folder id</param>
        /// <returns>Returns result object with Memory stream if the operation succeeded</returns>
        public static Result<MemoryStream> DownloadAllFiles(this DriveService driveService, string folderId)
            => driveService.DownloadAllFiles(folderId, int.MaxValue);


        /// <summary>
        /// Downloads only the files of a folder as memory stream. 
        /// </summary>
        /// <param name="driveService">The drive service</param>
        /// <param name="folderId">Drive folder id</param>
        /// <param name="depth">The depth of search.</param>
        /// <returns>Returns result object with Memory stream if the operation succeeded</returns>
        public static Result<MemoryStream> DownloadAllFiles(this DriveService driveService, string folderId, int depth)
            => driveService.DownloadFolderCore(folderId, downloadFilesOnly: true, depth);

        #endregion

        #region Download Folder

        /// <summary>
        /// Downloads subfolders and files for a specific folder hierarchically to specific path.
        /// </summary>
        /// <param name="driveService">The drive service</param>
        /// <param name="folderId">Drive folder id</param>
        /// <param name="path">The path that the file save it.</param>
        /// <returns>Returns result object with true if the operation succeeded</returns>
        public static Result<bool> DownloadFolder(this DriveService driveService, string folderId, string path)
            => driveService.DownloadFolder(folderId, path);

        /// <summary>
        /// Downloads subfolders and files for a specific folder hierarchically to specific path.
        /// </summary>
        /// <param name="driveService">The drive service</param>
        /// <param name="folderId">Drive folder id</param>
        /// <param name="path">The path that the file save it.</param>
        /// <param name="name">The name of a zip file.</param>
        /// <returns>Returns result object with true if the operation succeeded</returns>
        public static Result<bool> DownloadFolder(this DriveService driveService, string folderId, string path, string name)
            => driveService.DownloadFolder(folderId, path, name, int.MaxValue);

        /// <summary>
        /// Downloads subfolders and files for a specific folder hierarchically to specific path.
        /// </summary>
        /// <param name="driveService">The drive service</param>
        /// <param name="folderId">Drive folder id</param>
        /// <param name="path">The path that the file save it.</param>
        /// <param name="depth">The depth of search.</param>
        /// <returns>Returns result object with true if the operation succeeded</returns>
        public static Result<bool> DownloadFolder(this DriveService driveService, string folderId, string path, int depth)
            => driveService.DownloadFolder(folderId, path, null, depth);

        /// <summary>
        /// Downloads subfolders and files for a specific folder hierarchically to specific path.
        /// </summary>
        /// <param name="driveService">The drive service</param>
        /// <param name="folderId">Drive folder id</param>
        /// <param name="path">The path that the file save it.</param>
        /// <param name="name">The name of a zip file.</param>
        /// <param name="depth">The depth of search.</param>
        /// <returns>Returns result object with true if the operation succeeded</returns>
        public static Result<bool> DownloadFolder(this DriveService driveService, string folderId, string path, string name, int depth)
        {
            var file = driveService.GetById(folderId);
            if (file == null)
                return Result.Failed<bool>("File not exist.");

            var downloadResult = driveService.DownloadFolder(folderId, depth);
            if (!downloadResult.IsSucceded)
            {
                return Result.Failed<bool>(downloadResult.Message);
            }

            var memoryStream = downloadResult.Data;
            var fileName = name ?? file.Name;

            SaveStream(memoryStream, path, fileName);

            return true;
        }

        /// <summary>
        /// Downloads subfolders and files for a specific folder hierarchically as memory stream.
        /// </summary>
        /// <param name="driveService">The drive service</param>
        /// <param name="folderId">Drive folder id</param>
        /// <returns>Returns result object with Memory stream if the operation succeeded</returns>
        public static Result<MemoryStream> DownloadFolder(this DriveService driveService, string folderId)
            => driveService.DownloadFolder(folderId, int.MaxValue);

        /// <summary>
        /// Downloads subfolders and files for a specific folder hierarchically as memory stream.
        /// </summary>
        /// <param name="driveService">The drive service</param>
        /// <param name="folderId">Drive folder id</param>
        /// <param name="depth">The depth of search.</param>
        /// <returns>Returns result object with Memory stream if the operation succeeded</returns>
        public static Result<MemoryStream> DownloadFolder(this DriveService driveService, string folderId, int depth)
            => driveService.DownloadFolderCore(folderId, downloadFilesOnly: true, depth);

        #endregion

        #region Download File

        /// <summary>
        /// Download a single file as memory stream.
        /// </summary>
        /// <param name="driveService">The drive service</param>
        /// <param name="fileId">Drive file id</param>
        /// <returns>Returns the <code>Result</code> object with file as memory stream if the operation succeeded.</returns>
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

        /// <summary>
        /// Download a single file to specific file.
        /// </summary>
        /// <param name="driveService">The drive service</param>
        /// <param name="fileId">Drive file id</param>
        /// <returns>Returns the <code>Result</code> object with file as memory stream if the operation succeeded.</returns>
        public static Result<bool> DownloadFile(this DriveService driveService, string fileId, string path)
            => driveService.DownloadFile(fileId, path, null);

        /// <summary>
        /// Download a single file to specific file.
        /// </summary>
        /// <param name="driveService">The drive service</param>
        /// <param name="fileId">Drive file id</param>
        /// <returns>Returns the <code>Result</code> object with file as memory stream if the operation succeeded.</returns>
        public static Result<bool> DownloadFile(this DriveService driveService, string fileId, string path, string name)
        {
            var file = driveService.GetById(fileId);
            if (file == null)
                return Result.Failed<bool>("File not exist.");

            var downloadResult = driveService.DownloadFile(fileId);
            if (!downloadResult.IsSucceded)
            {
                return Result.Failed<bool>(downloadResult.Message);
            }

            var memoryStream = downloadResult.Data;
            var fileName = name ?? file.Name;

            SaveStream(memoryStream, path, fileName);

            return true;
        }

        #endregion

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

        private static Result<MemoryStream> DownloadFolderCore(this DriveService driveService, string folderId, bool downloadFilesOnly, int depth)
        {
            if (depth <= 0)
                return Result.Failed<MemoryStream>("Depth must be positive number.");

            MemoryStream memoryStream = new MemoryStream();
            using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                driveService.DownloadFolderCore(folderId, zipArchive, "", downloadFilesOnly, depth);
            }
            memoryStream.Position = 0;

            return memoryStream;
        }

        private static void SaveStream(MemoryStream memoryStream, string path, string name)
        {
            using FileStream fileStream = new FileStream(Path.Combine(path, $"{name}.zip"), FileMode.Create, FileAccess.Write);
            memoryStream.WriteTo(fileStream);
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