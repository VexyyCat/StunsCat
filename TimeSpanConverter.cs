using System;
using System.Globalization;
using System.Windows.Data;

namespace StunsCat
{
    public class TimeSpanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan timeSpan)
            {
                return timeSpan.ToString(@"mm\:ss");
            }

            return "00:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string timeString && TimeSpan.TryParseExact(timeString, @"mm\:ss", null, out TimeSpan result))
            {
                return result;
            }

            return TimeSpan.Zero;
        }
    }
}