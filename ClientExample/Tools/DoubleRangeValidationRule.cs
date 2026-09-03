using System.Globalization;
using System.Windows.Controls;

namespace ClientExample;

internal class DoubleRangeValidationRule : ValidationRule
{
    public double Minimum { get; set; } = double.MinValue;
    public double Maximum { get; set; } = double.MaxValue;

    public override ValidationResult Validate(
        object value,
        CultureInfo cultureInfo)
    {
        if (value is string text &&
            double.TryParse(text, NumberStyles.Float, cultureInfo, out double number))
        {
            if (number >= Minimum && number <= Maximum)
                return ValidationResult.ValidResult;
        }

        return new ValidationResult(
            false,
            $"Value must be between {Minimum} and {Maximum}.");
    }
}