using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Printing;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ZeroPlus.Oms.Ui.Helper
{
    public class ColorToHexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is string hex ? (Color)ColorConverter.ConvertFromString(hex) : null;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
            => value is Color color ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : null;
    }
}
