using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Helper
{
    public class KeyGestureToStringConverter : IValueConverter
    {
        static readonly KeyGestureConverter converter = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is string code ? converter.ConvertFromString(code) : null;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is KeyGesture gesture ? gesture.GetDisplayStringForCulture(culture) : null;
    }
}
