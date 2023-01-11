using System;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Microsoft.Extensions.Options;
using Google.Apis.Drive.v3;
using Tests.Configures;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using EasyDriveFilesManager;
using static Google.Apis.Drive.v3.DriveService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using System.Text;

namespace Tests;

public class UnitTest
{
    private readonly DriveService _driveService;
    public UnitTest()
    {
        string json = System.IO.File.ReadAllText(@"C:\Users\osama\source\repos\EasyDriveFilesManager\Tests\Configures\settings.json");
        var settings = JsonConvert.DeserializeObject<MyDriveSettings>(json);

        _driveService = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = new UserCredential(
                    new GoogleAuthorizationCodeFlow(
                        new GoogleAuthorizationCodeFlow.Initializer
                        {
                            ClientSecrets = new ClientSecrets
                            {
                                ClientId = settings.ClientId,
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

    [Fact]
    public async Task WhenUploadFileToDrive_TheTheFileIsCreated()
    {
        var ms = new MemoryStream();
        var writer = new StreamWriter(ms);
        writer.Write("Test file content");
        writer.Flush();
        ms.Position = 0;

        var file = new FormFile(ms, 0, ms.Length, "Data", "test1.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };
        var actual = await _driveService.UploadFile(file, "", "12sXAZ1EA7ocpon5Bx-fbtU7jxJgItJGi");
        Assert.True(!string.IsNullOrEmpty(actual));
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
