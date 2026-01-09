using System.ComponentModel.DataAnnotations;

namespace KamiYomu.Web.Validators;

public class UriValidatorAttribute : ValidationAttribute
{
    public UriValidatorAttribute()
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is Uri uri)
        {
            return !uri.IsAbsoluteUri ? new ValidationResult(ErrorMessage ?? "The URL must be absolute.") : ValidationResult.Success;
        }

        if (value is string str)
        {
            if (Uri.TryCreate(str, UriKind.Absolute, out _))
            {
                return ValidationResult.Success;
            }
        }

        return new ValidationResult(ErrorMessage ?? "Invalid URL format.");
    }
}
