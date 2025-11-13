using EasyDriveFilesManager;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Diagnostics;
using Tests.Configures;
using File = System.IO.File;
namespace Tests;

public class UnitTests
{
    private readonly DriveService _driveService;
    private readonly string _rootFolderId;

    public UnitTests()
    {
        string json = File.ReadAllText(@"settings.json");
        var settings = JsonConvert.DeserializeObject<MyDriveSettings>(json);
        _rootFolderId = settings!.RootFolderId;

        GoogleCredential credential;
        using (var stream = new FileStream(settings.ServiceAccountKeyPath, FileMode.Open, FileAccess.Read))
        {
            credential = GoogleCredential.FromStream(stream)
                                          .CreateScoped(DriveService.Scope.Drive);
        }

        _driveService = new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = settings.ApplicationName
        });

    }

    [Fact]
    public async Task GivenFolderId_WhenCompressTheFolder_ThenTheFolderCompressedAsZipFile()
    {
        // Arrange
        string folderId = _rootFolderId;

        // Act
        var actual = await _driveService.CompressFolderAsync(folderId);

        // Assert
        Assert.False(string.IsNullOrEmpty(actual.Result));
    }

    [Fact]
    public async Task GivenFileId_WhenDownloadFile_ThenTheFileIsDownloaded()
    {
        // Arrange
        var file = UnitTestsHelpers.CreateFile(1);

        // Act
        var actual = await _driveService.UploadFileAsync(file, string.Empty, new string[] { _rootFolderId }, (progress) =>
        {
            Debug.WriteLine(progress.BytesSent);
        });

        // Assert
        Assert.False(string.IsNullOrEmpty(actual.Result));

    }

    [Fact]
    public async Task GivenFileId_WhenDeleteFolderOrFile_ThenTheFileIsDeleted()
    {
        // Arrange
        var file = UnitTestsHelpers.CreateFile(1);
        var uploadResult = await _driveService.UploadFileAsync(file, string.Empty, _rootFolderId, 
            (progress) =>
            {
                Console.WriteLine(progress.BytesSent);
            });

        // Act
        var actual = _driveService.DeleteFolderOrFile(uploadResult.Result);

        // Assert
        Assert.True(actual.Result);
    }

    [Fact]
    public void GivenFolderName_WhenCreateFolder_ThenTheFolderIsCreated()
    {
        // Arrange
        string folderName = $"New Folder{Random.Shared.NextInt64(100)}";

        // Act
        var result = _driveService.CreateFolder(folderName, _rootFolderId);

        // Assert
        Assert.True(result.IsSucceeded);
        Assert.False(string.IsNullOrEmpty(result.Result));
    }

    [Fact]
    public void GivenFolderName_WhenCreateFolderWithParents_ThenTheFolderIsCreated()
    {
        // Arrange
        string folderName = $"New Folder{Random.Shared.NextInt64(100)}";

        // Act
        var result = _driveService.CreateFolder(folderName, new[] { _rootFolderId });

        // Assert
        Assert.True(result.IsSucceeded);
        Assert.False(string.IsNullOrEmpty(result.Result));
    }

    [Fact]
    public void GivenNull_WhenDeleteFolderOrFile_ThenReturnsFalse()
    {
        // Arrange
        string? fileId = null;

        // Act
        var actual = _driveService.DeleteFolderOrFile(fileId);

        // Assert
        Assert.False(actual.Result);
    }

    [Fact]
    public void GivenNewName_WhenRenameFolder_ThenTheFolderNameIsChanged()
    {
        // Arrange
        string folderName = $"New Folder{Random.Shared.NextInt64(100)}";
        var result = _driveService.CreateFolder(folderName, new[] { _rootFolderId });

        // Arrange
        string newName = folderName + " - Renamed";

        // Act
        var actual = _driveService.RenameFolder(result.Result, newName);

        // Assert
        Assert.True(actual.IsSucceeded);
        Assert.Equal(newName, actual.Result.Name);
    }

    [Fact]
    public async Task GivenFile_WhenUploadFileToDrive_ThenTheFileIsUploaded()
    {
        // Arrange
        var file = UnitTestsHelpers.CreateFile(1);

        // Act
        var actual = await _driveService.UploadFileAsync(file, "", _rootFolderId, (progress) =>
        {
            Debug.WriteLine(progress.BytesSent);
        });

        // Assert
        Assert.False(string.IsNullOrEmpty(actual.Result));
    }


    [Fact]
    public async Task GivenFiles_WhenUploadFilesToDrive_ThenTheFilesAreUploaded()
    {
        // Arrange
        List<IFormFile> files = new();

        for (int i = 2; i < 5; i++)
        {
            files.Add(UnitTestsHelpers.CreateFile(i));
        }
        
        // Act
        var actual = await _driveService.UploadFilesAsync(files.ToList(), _rootFolderId);

        // Assert
        Assert.True(actual.Result.All(x => !string.IsNullOrEmpty(x)));
    }

    [Fact]
    public void GivenDriveFolderId_WhenDownloadAllFiles_ThenFilesAreDownloadedWithoutFolders()
    {
        // Arrange
        var targetPath = @".\";
        var name = Guid.NewGuid().ToString();

        // Act
        var actual = _driveService.DownloadAllFiles(_rootFolderId, targetPath, name);

        // Assert
        Assert.True(File.Exists($"{targetPath}{name}.zip"));
        Assert.True(actual.IsSucceeded);
    }

    [Fact]
    public void GivenDriveFolderId_WhenDownloadAllFilesWithDepth_ThenAllFilesAreDownloadedWithoutFolders()
    {
        // Arrange
        var targetPath = @".\";
        var name = Guid.NewGuid().ToString();
        
        // Act
        var actual = _driveService.DownloadAllFiles(_rootFolderId, targetPath, name, 1);

        // Assert
        Assert.True(File.Exists($"{targetPath}{name}.zip"));
        Assert.True(actual.IsSucceeded);
    }

    [Fact]
    public void GivenDriveFolderId_WhenDownloadFolder_ThenAllFoldersAndFilesAreDownloaded()
    {
        // Arrange
        var targetPath = @".\";
        var name = Guid.NewGuid().ToString();

        // Act
        var actual = _driveService.DownloadFolder(_rootFolderId, targetPath, name);

        // Assert
        Assert.True(File.Exists($"{targetPath}{name}.zip"));
        Assert.True(actual.IsSucceeded);
    }

    [Fact]
    public void GivenDriveFolderId_WhenDownloadFolderWithDepth_ThenFoldersAndFilesAreDownloaded()
    {
        // Arrange
        var targetPath = @".\";
        var name = Guid.NewGuid().ToString();

        // Act
        var actual = _driveService.DownloadFolder(_rootFolderId, targetPath, name, 1);

        // Assert
        Assert.True(File.Exists($"{targetPath}{name}.zip"));
        Assert.True(actual.IsSucceeded);
    }
}
