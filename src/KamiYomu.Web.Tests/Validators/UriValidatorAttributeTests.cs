using System.ComponentModel.DataAnnotations;

using KamiYomu.Web.Validators;

namespace KamiYomu.Web.Tests.Validators;

public class UriValidatorAttributeTests
{
    [Fact]
    public void GetValidationResult_ReturnsSuccessForAbsoluteUri()
    {
        UriValidatorAttribute attribute = new();

        ValidationResult? result = attribute.GetValidationResult(new Uri("https://example.com"), new ValidationContext(new object()));

        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void GetValidationResult_ReturnsErrorForRelativeUri()
    {
        UriValidatorAttribute attribute = new();

        ValidationResult? result = attribute.GetValidationResult(new Uri("/relative", UriKind.Relative), new ValidationContext(new object()));

        Assert.NotNull(result);
        Assert.Equal("The URL must be absolute.", result.ErrorMessage);
    }

    [Fact]
    public void GetValidationResult_ReturnsSuccessForAbsoluteStringUri()
    {
        UriValidatorAttribute attribute = new();

        ValidationResult? result = attribute.GetValidationResult("https://example.com", new ValidationContext(new object()));

        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void GetValidationResult_ReturnsCustomMessageForInvalidValue()
    {
        UriValidatorAttribute attribute = new()
        {
            ErrorMessage = "Custom invalid url message."
        };

        ValidationResult? result = attribute.GetValidationResult("not-a-valid-uri", new ValidationContext(new object()));

        Assert.NotNull(result);
        Assert.Equal("Custom invalid url message.", result.ErrorMessage);
    }

    [Fact]
    public void GetValidationResult_ReturnsDefaultMessageForNullValue()
    {
        UriValidatorAttribute attribute = new();

        ValidationResult? result = attribute.GetValidationResult(null, new ValidationContext(new object()));

        Assert.NotNull(result);
        Assert.Equal("Invalid URL format.", result.ErrorMessage);
    }
}
