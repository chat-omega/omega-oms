using System;
using System.Globalization;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(object), typeof(bool))]
    public sealed class NullToBooleanConverter : IValueConverter
    {
        public bool Inverted { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Inverted ? value != null : value == null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
