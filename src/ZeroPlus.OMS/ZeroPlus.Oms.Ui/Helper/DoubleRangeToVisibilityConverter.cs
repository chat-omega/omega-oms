using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(double), typeof(Visibility))]
    public sealed class DoubleRangeToVisibilityConverter : IValueConverter
    {
        public double Limit { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double valueDouble || double.IsNaN(valueDouble) || double.IsInfinity(valueDouble) || valueDouble <= Limit)
            {
                return Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}