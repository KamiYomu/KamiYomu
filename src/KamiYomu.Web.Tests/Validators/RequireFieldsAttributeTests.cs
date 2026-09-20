using System.ComponentModel.DataAnnotations;

using KamiYomu.Web.Validators;

namespace KamiYomu.Web.Tests.Validators;

public class RequireFieldsAttributeTests
{
    [Fact]
    public void Constructor_ThrowsWhenNoPropertyNamesAreProvided()
    {
        _ = Assert.Throws<ArgumentException>(() => new RequireFieldsAttribute());
    }

    [Fact]
    public void GetValidationResult_ReturnsSuccessWhenValueIsNull()
    {
        RequireFieldsAttribute attribute = new("Name");

        ValidationResult? result = attribute.GetValidationResult(null, new ValidationContext(new object()));

        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void GetValidationResult_ReturnsSuccessWhenAnyConfiguredPropertyIsFilled()
    {
        RequireFieldsAttribute attribute = new("Name", "Email")
        {
            ErrorMessage = "At least one field is required."
        };
        ContactModel model = new()
        {
            Name = "Marco",
            Email = " "
        };

        ValidationResult? result = attribute.GetValidationResult(model, new ValidationContext(model));

        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void GetValidationResult_ReturnsErrorWhenAllConfiguredPropertiesAreEmpty()
    {
        RequireFieldsAttribute attribute = new("Name", "Email")
        {
            ErrorMessage = "At least one field is required."
        };
        ContactModel model = new()
        {
            Name = " ",
            Email = null
        };

        ValidationResult? result = attribute.GetValidationResult(model, new ValidationContext(model));

        Assert.NotNull(result);
        Assert.Equal("At least one field is required.", result.ErrorMessage);
    }

    [Fact]
    public void GetValidationResult_ThrowsWhenConfiguredPropertyDoesNotExist()
    {
        RequireFieldsAttribute attribute = new("MissingProperty");
        ContactModel model = new();

        _ = Assert.Throws<InvalidOperationException>(() => attribute.GetValidationResult(model, new ValidationContext(model)));
    }

    private sealed class ContactModel
    {
        public string? Name { get; init; }

        public string? Email { get; init; }
    }
}
