using System;
using System.Globalization;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(DateTime), typeof(string))]
    public sealed class DateTimeToDisplayTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DateTime time)
            {
                return "";
            }

            if (time == default)
            {
                return "Any";
            }
            else
            {
                return time.ToString("MMM dd yy");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
