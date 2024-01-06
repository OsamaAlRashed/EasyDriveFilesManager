using Microsoft.AspNetCore.Http;

internal static class UnitTestsHelpers
{
    internal static IFormFile CreateFile(int i)
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
}