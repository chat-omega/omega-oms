using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Models.Data.Enums;
namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(PermMode), typeof(bool))]
    public sealed class PermModeToSettingsStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not PermMode type)
            {
                return true;
            }

            return type switch
            {
                PermMode.Highlight => false,
                _ => (object)true,
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
