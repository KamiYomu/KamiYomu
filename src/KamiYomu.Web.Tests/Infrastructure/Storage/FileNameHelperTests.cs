using KamiYomu.Web.Infrastructure.Storage;

namespace KamiYomu.Web.Tests.Infrastructure.Storage;

public class FileNameHelperTests
{
    [Fact]
    public void SanitizeFileName_ReturnsEmpty_WhenInputIsNullOrWhitespace()
    {
        // Arrange
        string? nullInput = null;

        // Act
        string nullResult = FileNameHelper.SanitizeFileName(nullInput!);
        string emptyResult = FileNameHelper.SanitizeFileName(string.Empty);
        string whitespaceResult = FileNameHelper.SanitizeFileName("   ");

        // Assert
        Assert.Equal(string.Empty, nullResult);
        Assert.Equal(string.Empty, emptyResult);
        Assert.Equal(string.Empty, whitespaceResult);
    }

    [Fact]
    public void SanitizeFileName_RemovesAccents_ReplacesInvalidCharacters_AndCollapsesReplacementRuns()
    {
        // Arrange
        const string input = "  Café///Au  ";

        // Act
        string result = FileNameHelper.SanitizeFileName(input, "-");

        // Assert
        Assert.Equal("Cafe-Au", result);
    }

    [Fact]
    public void SanitizeFileName_UsesUnderscore_WhenReplacementIsEmpty()
    {
        // Arrange
        const string input = "/One//Piece/";

        // Act
        string result = FileNameHelper.SanitizeFileName(input, string.Empty);

        // Assert
        Assert.Equal("One_Piece", result);
    }

    [Fact]
    public void NormalizeSystemPath_ReturnsInput_WhenValueIsNullOrWhitespace()
    {
        // Arrange
        string? nullInput = null;

        // Act
        string? nullResult = FileNameHelper.NormalizeSystemPath(nullInput!);
        string emptyResult = FileNameHelper.NormalizeSystemPath(string.Empty);
        string whitespaceResult = FileNameHelper.NormalizeSystemPath("   ");

        // Assert
        Assert.Null(nullResult);
        Assert.Equal(string.Empty, emptyResult);
        Assert.Equal("   ", whitespaceResult);
    }

    [Theory]
    [InlineData("/db", "db")]
    [InlineData("db", "db")]
    public void NormalizeSystemPath_CombinesExpectedBaseDirectory(string raw, string relativePath)
    {
        // Arrange
        string baseDir = FileNameHelper.IsRunningInDocker()
            ? "/"
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KamiYomu");
        string expected = Path.Combine(baseDir, relativePath);

        // Act
        string result = FileNameHelper.NormalizeSystemPath(raw);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsRunningInDocker_MatchesDockerenvPresence()
    {
        // Arrange
        bool expected = File.Exists("/.dockerenv");

        // Act
        bool result = FileNameHelper.IsRunningInDocker();

        // Assert
        Assert.Equal(expected, result);
    }
}
