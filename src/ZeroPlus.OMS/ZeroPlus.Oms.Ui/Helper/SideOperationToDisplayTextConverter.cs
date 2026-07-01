using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(SideOperation), typeof(string))]
    public sealed class SideOperationToDisplayTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not SideOperation type)
            {
                return "";
            }

            return type switch
            {
                SideOperation.Equal => "=",
                SideOperation.Greater => ">",
                SideOperation.GreaterOrEqual => "≥",
                _ => "",
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
