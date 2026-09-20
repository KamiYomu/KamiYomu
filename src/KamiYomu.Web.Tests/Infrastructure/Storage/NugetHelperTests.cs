using System.IO.Compression;

using KamiYomu.Web.Infrastructure.Storage;

namespace KamiYomu.Web.Tests.Infrastructure.Storage;

public class NugetHelperTests
{
    [Fact]
    public void IsNugetPackage_ReturnsFalse_WhenFileDoesNotExist()
    {
        // Arrange
        string filePath = Path.Combine(AppContext.BaseDirectory, "nuget-helper-tests", Guid.NewGuid().ToString("N"), "missing.zip");

        // Act
        bool result = NugetHelper.IsNugetPackage(filePath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsNugetPackage_ReturnsTrue_WhenArchiveContainsNuspecFile()
    {
        using TestArtifacts artifacts = new();
        string filePath = artifacts.CreateZipArchive(
            "package-with-nuspec.zip",
            ("package/example.nuspec", "<package />"),
            ("lib/net8.0/example.dll", "binary-placeholder"));

        // Act
        bool result = NugetHelper.IsNugetPackage(filePath);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsNugetPackage_ReturnsFalse_WhenArchiveDoesNotContainNuspecFile()
    {
        using TestArtifacts artifacts = new();
        string filePath = artifacts.CreateZipArchive(
            "package-without-nuspec.zip",
            ("lib/net8.0/example.dll", "binary-placeholder"),
            ("README.txt", "not-a-package"));

        // Act
        bool result = NugetHelper.IsNugetPackage(filePath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsNugetPackage_ReturnsFalse_WhenFileIsNotAValidZipArchive()
    {
        using TestArtifacts artifacts = new();
        string filePath = artifacts.CreateTextFile("invalid.zip", "plain text content");

        // Act
        bool result = NugetHelper.IsNugetPackage(filePath);

        // Assert
        Assert.False(result);
    }

    private sealed class TestArtifacts : IDisposable
    {
        private readonly string _rootDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "nuget-helper-tests",
            Guid.NewGuid().ToString("N"));

        public TestArtifacts()
        {
            _ = Directory.CreateDirectory(_rootDirectory);
        }

        public string CreateTextFile(string fileName, string content)
        {
            string path = Path.Combine(_rootDirectory, fileName);
            File.WriteAllText(path, content);
            return path;
        }

        public string CreateZipArchive(string fileName, params (string EntryName, string Content)[] entries)
        {
            string path = Path.Combine(_rootDirectory, fileName);

            using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach ((string entryName, string content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(entryName);
                using StreamWriter writer = new(entry.Open());
                writer.Write(content);
            }

            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, recursive: true);
            }
        }
    }
}
