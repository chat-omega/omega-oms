using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(double), typeof(Visibility))]
    public sealed class DoubleToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double valueDouble)
            {
                return Visibility.Visible;
            }

            if (double.IsNaN(valueDouble) || double.IsInfinity(valueDouble))
            {
                return Visibility.Collapsed;
            }
            else
            {
                return Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
