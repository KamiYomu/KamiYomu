using KamiYomu.Web.Extensions;

namespace KamiYomu.Web.Tests.Extensions;

public class UriExtensionsTests
{
    [Theory]
    [InlineData("https://example.com/cover.jpg", true)]
    [InlineData("https://example.com/cover.txt", false)]
    [InlineData("/images/cover.jpg", false)]
    public void IsValidImageUri_ReturnsExpectedResult(string uriValue, bool expected)
    {
        Uri uri = uriValue.StartsWith('/')
            ? new Uri(uriValue, UriKind.Relative)
            : new Uri(uriValue, UriKind.Absolute);

        bool result = uri.IsValidImageUri();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsValidImageUri_ReturnsFalseForNullUri()
    {
        Assert.False(UriExtensions.IsValidImageUri(null!));
    }

    [Fact]
    public void GetFileNameFromUri_ReturnsFileName()
    {
        Uri uri = new("https://example.com/images/cover.png");

        string result = uri.GetFileNameFromUri();

        Assert.Equal("cover.png", result);
    }

    [Theory]
    [InlineData("https://example.com/cover.jpg", "image/jpeg")]
    [InlineData("https://example.com/cover.svg", "image/svg+xml")]
    [InlineData("https://example.com/archive.bin", "application/octet-stream")]
    public void GetContentType_ReturnsExpectedContentType(string uriValue, string expectedContentType)
    {
        Uri uri = new(uriValue);

        string result = uri.GetContentType();

        Assert.Equal(expectedContentType, result);
    }

    [Theory]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".png", "image/png")]
    [InlineData(".unknown", "application/octet-stream")]
    public void ExtensionToContentType_ReturnsExpectedContentType(string extension, string expectedContentType)
    {
        string result = UriExtensions.ExtensionToContentType(extension);

        Assert.Equal(expectedContentType, result);
    }

    [Fact]
    public void ToEncodedString_EncodesEntireUri()
    {
        Uri uri = new("https://example.com/image one.png?x=1&y=2");

        string result = uri.ToEncodedString();

        Assert.Equal("https%3A%2F%2Fexample.com%2Fimage+one.png%3Fx%3D1%26y%3D2", result);
    }

    [Fact]
    public void ToInternalImageUrl_ReturnsInternalRelativeUrlForValidImage()
    {
        Uri uri = new("https://example.com/cover.jpg");

        Uri result = uri.ToInternalImageUrl();

        Assert.False(result.IsAbsoluteUri);
        Assert.Equal("/Libraries/Collection/Index?handler=Image&uri=https%3A%2F%2Fexample.com%2Fcover.jpg", result.OriginalString);
    }

    [Fact]
    public void ToInternalImageUrl_ReturnsOriginalUriForNonImage()
    {
        Uri uri = new("https://example.com/index.html");

        Uri result = uri.ToInternalImageUrl();

        Assert.Same(uri, result);
    }
}
