using System.Globalization;
using System.Windows.Data;

namespace SageRage.UI.Converters;

public class PercentageToWidthConverter : IValueConverter
{
    public double MaxWidth { get; set; } = 200.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
            return Math.Max(4, d * MaxWidth);
        return 4.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
