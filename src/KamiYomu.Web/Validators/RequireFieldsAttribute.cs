using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace KamiYomu.Web.Validators;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class RequireFieldsAttribute : ValidationAttribute
{
    private readonly string[] _propertyNames;

    public RequireFieldsAttribute(params string[] propertyNames)
    {
        if (propertyNames == null || propertyNames.Length == 0)
        {
            throw new ArgumentException("At least one property name must be provided.");
        }

        _propertyNames = propertyNames;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        Type type = value.GetType();

        foreach (string propertyName in _propertyNames)
        {
            PropertyInfo? prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance) ?? throw new InvalidOperationException(
                    $"Property '{propertyName}' not found on type '{type.Name}'."
                );
            object? propValue = prop.GetValue(value);

            if (IsFilled(propValue))
            {
                return ValidationResult.Success;
            }
        }

        return new ValidationResult(
            ErrorMessage
        );
    }

    private static bool IsFilled(object? value)
    {
        return value switch
        {
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            _ => true
        };
    }
}
