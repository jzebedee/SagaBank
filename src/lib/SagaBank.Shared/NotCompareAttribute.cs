using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace SagaBank.Shared;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class NotCompareAttribute : ValidationAttribute
{
    [RequiresUnreferencedCode("The property referenced by 'otherProperty' may be trimmed. Ensure it is preserved.")]
    public NotCompareAttribute(string otherProperty) : base("'{0}' and '{1}' match.")
    {
        ArgumentNullException.ThrowIfNull(otherProperty);

        OtherProperty = otherProperty;
    }

    public string OtherProperty { get; }

    public string? OtherPropertyDisplayName { get; internal set; }

    public override bool RequiresValidationContext => true;

    public override string FormatErrorMessage(string name) =>
        string.Format(
            CultureInfo.CurrentCulture, ErrorMessageString, name, OtherPropertyDisplayName ?? OtherProperty);

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2072:UnrecognizedReflectionPattern",
        Justification = "The ctor is marked with RequiresUnreferencedCode informing the caller to preserve the other property.")]
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var otherPropertyInfo = validationContext.ObjectType.GetRuntimeProperty(OtherProperty);
        if (otherPropertyInfo == null)
        {
            return new ValidationResult(string.Format("Could not find a property named {0}.", OtherProperty));
        }
        if (otherPropertyInfo.GetIndexParameters().Length > 0)
        {
            throw new ArgumentException(string.Format("The property {0}.{1} could not be found.", validationContext.ObjectType.FullName, OtherProperty));
        }

        object? otherPropertyValue = otherPropertyInfo.GetValue(validationContext.ObjectInstance, null);
        if (Equals(value, otherPropertyValue))
        {
            OtherPropertyDisplayName ??= GetDisplayNameForProperty(otherPropertyInfo);

            string[]? memberNames = validationContext.MemberName != null
               ? new[] { validationContext.MemberName }
               : null;
            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName), memberNames);
        }

        return null;
    }

    private string? GetDisplayNameForProperty(PropertyInfo property)
    {
        IEnumerable<Attribute> attributes = CustomAttributeExtensions.GetCustomAttributes(property, true);
        foreach (Attribute attribute in attributes)
        {
            if (attribute is DisplayAttribute display)
            {
                return display.GetName();
            }
        }

        return OtherProperty;
    }
}
