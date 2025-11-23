using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LaptopHealth.Converters
{
    /// <summary>
    /// Converts a boolean value to Visibility, with optional inverse behavior
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool boolValue = value is bool b && b;
            bool inverse = parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase);

            if (inverse)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool inverse = parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
            
            if (value is Visibility visibility)
            {
                bool isVisible = visibility == Visibility.Visible;
                return inverse ? !isVisible : isVisible;
            }

            return false;
        }
    }
}
