using System.Globalization;
using System.Windows.Data;

namespace ClientExample;

internal class ValueToBooleanConverter : IValueConverter
{
    public bool IsNotNull { get; init; } = true;

    public object Convert(object value, Type targetType,
                          object parameter, CultureInfo culture)
    {
        return IsNotNull ? value != null : value == null;
    }

    public object ConvertBack(object value, Type targetType,
                              object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
