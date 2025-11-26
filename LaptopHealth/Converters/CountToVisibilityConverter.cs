using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LaptopHealth.Converters
{
    /// <summary>
    /// Converts a count (int) to Visibility. Returns Visible when count is 0, Collapsed otherwise.
    /// Used to show "No items" messages when a collection is empty.
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
