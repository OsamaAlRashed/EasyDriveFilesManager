using Newtonsoft.Json;
using Google.Apis.Drive.v3;
using Tests.Configures;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using static Google.Apis.Drive.v3.DriveService;
using Microsoft.AspNetCore.Http;
using EasyDriveFilesManager;
using System.Diagnostics;
using Google.Apis.Drive.v3.Data;
using DriveFile = Google.Apis.Drive.v3.Data.File;
using File = System.IO.File;
namespace Tests;

public class UnitTests
{
    private readonly DriveService _driveService;

    public UnitTests()
    {
        string json = File.ReadAllText(@"C:\Users\osama\source\repos\EasyDriveFilesManager\Tests\Configures\settings.json");
        var settings = JsonConvert.DeserializeObject<MyDriveSettings>(json);

        _driveService = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = new UserCredential(
                    new GoogleAuthorizationCodeFlow(
                        new GoogleAuthorizationCodeFlow.Initializer
                        {
                            ClientSecrets = new ClientSecrets
                            {
                                ClientId = settings!.ClientId,
                                ClientSecret = settings.ClientSecret
                            },
                            Scopes = new[] { Scope.Drive },
                        }), settings.Username, new TokenResponse
                        {
                            AccessToken = settings.AccessToken,
                            RefreshToken = settings.RefreshToken,
                        }),
            ApplicationName = settings.ApplicationName
        });
    }

    private static IFormFile CreateFile(int i)
    {
        var ms = new MemoryStream();
        var writer = new StreamWriter(ms);
        writer.Write("Test file content");
        writer.Flush();
        ms.Position = 0;

        var file = new FormFile(ms, 0, ms.Length, "Data", $"test{i}.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        return file;
    }

    [Fact]
    public async Task GivenFileId_WhenDeleteFolderOrFile_ThenTheFileIsDeleted()
    {
        // Arrange
        var file = CreateFile(1);
        var uploadResult = await _driveService.UploadFileAsync(file, "", "12sXAZ1EA7ocpon5Bx-fbtU7jxJgItJGi", (progress) =>
        {
            Debug.WriteLine(progress.BytesSent);
        });

        // Act
        var actual = _driveService.DeleteFolderOrFile(uploadResult.Result);

        // Assert
        Assert.True(actual.Result);
    }

    [Fact]
    public async Task GivenNull_WhenDeleteFolderOrFile_ThenReturnsFalse()
    {
        // Arrange
        string? fileId = null;

        // Act
        var actual = _driveService.DeleteFolderOrFile(fileId);

        // Assert
        Assert.False(actual.Result);
    }

    [Fact]
    public async Task GivenNewName_WhenRenameFolder_ThenTheFolderNameIsChanged()
    {
        // Arrange
        string newName = "New Name";

        // Act
        var actual = _driveService.RenameFolder("12sXAZ1EA7ocpon5Bx-fbtU7jxJgItJGi", newName);

        // Assert
        Assert.True(actual.Result.Name == newName);
    }

    [Fact]
    public async Task GivenFile_WhenUploadFileToDrive_ThenTheFileIsUploaded()
    {
        // Arrange
        var file = CreateFile(1);

        // Act
        var actual = await _driveService.UploadFileAsync(file, "", "12sXAZ1EA7ocpon5Bx-fbtU7jxJgItJGi", (progress) =>
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
        List<IFormFile> files = new List<IFormFile>();

        for (int i = 2; i < 5; i++)
        {
            files.Add(CreateFile(i));
        }
        
        // Act
        var actual = await _driveService.UploadFilesAsync(files.ToList(), "12sXAZ1EA7ocpon5Bx-fbtU7jxJgItJGi");

        // Assert
        Assert.True(actual.Result.All(x => !string.IsNullOrEmpty(x)));
    }

    [Fact]
    public async Task GivenFolderId_WhenCompressTheFolder_ThenTheFolderCompressedAsZipFile()
    {
        // Arrange
        string folderId = "12sXAZ1EA7ocpon5Bx-fbtU7jxJgItJGi";

        // Act
        var actual = await _driveService.CompressFolderAsync(folderId);

        // Assert
        Assert.False(string.IsNullOrEmpty(actual.Result));
    }

    [Fact]
    public void GivenDriveFolderId_WhenDownloadAllFiles_ThenFilesAreDownlodedWithoutFolders()
    {
        var targetPath = @".\";
        var name = Guid.NewGuid().ToString();
        _driveService.DownloadAllFiles("12sXAZ1EA7ocpon5Bx-fbtU7jxJgItJGi", targetPath, name);

        Assert.True(File.Exists($"{targetPath}{name}.zip"));
    }

    [Fact]
    public void GivenDriveFolderId_WhenDownloadAllFilesWithDepth_ThenAllFilesAreDownlodedWithoutFolders()
    {
        var targetPath = @".\";
        var name = Guid.NewGuid().ToString();
        _driveService.DownloadAllFiles("12sXAZ1EA7ocpon5Bx-fbtU7jxJgItJGi", targetPath, name, 1);

        Assert.True(File.Exists($"{targetPath}{name}.zip"));
    }

    [Fact]
    public void GivenDriveFolderId_WhenDownloadFolder_ThenAllFoldersAndFilesAreDownloded()
    {
        var targetPath = @".\";
        var name = Guid.NewGuid().ToString();
        _driveService.DownloadFolder("12sXAZ1EA7ocpon5Bx-fbtU7jxJgItJGi", targetPath, name);

        Assert.True(File.Exists($"{targetPath}{name}.zip"));
    }

    [Fact]
    public void GivenDriveFolderId_WhenDownloadFolderWithDepth_ThenFoldersAndFilesAreDownloded()
    {
        var targetPath = @".\";
        var name = Guid.NewGuid().ToString();
        _driveService.DownloadFolder("12sXAZ1EA7ocpon5Bx-fbtU7jxJgItJGi", targetPath, name, 1);

        Assert.True(File.Exists($"{targetPath}{name}.zip"));
    }

}
