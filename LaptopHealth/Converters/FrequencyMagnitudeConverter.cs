using System.Globalization;
using System.Windows.Data;

namespace LaptopHealth.Converters
{
    /// <summary>
    /// Converts frequency magnitude (0-10) to visual bar height (0-250 pixels)
    /// </summary>
    public class FrequencyMagnitudeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is float magnitude)
            {
                // Map 0-10 range to 0-250 pixels
                float minHeight = 2;
                float maxHeight = 250;
                float mappedHeight = minHeight + (magnitude / 10f) * (maxHeight - minHeight);
                return Math.Max(0, Math.Min(maxHeight, mappedHeight));
            }

            return 0d;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
