using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClientExample;

public class BooleansToVisibilityConverter : IMultiValueConverter
{
    public Visibility InvisibilityValue { get; init; } = Visibility.Collapsed;

    public object Convert(
        object[] values,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        var condition = parameter is bool ? (bool)parameter : true;
        return values.All(v => v.Equals(condition))
            ? Visibility.Visible
            : InvisibilityValue;
    }

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
