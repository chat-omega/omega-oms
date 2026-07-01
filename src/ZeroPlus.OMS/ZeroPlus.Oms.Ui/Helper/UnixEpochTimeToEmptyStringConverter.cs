using System;
using System.Globalization;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    public class UnixEpochTimeToEmptyStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime ts && (ts == DateTime.UnixEpoch || ts == default))
            {
                return string.Empty;
            }

            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
