using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(AutoPermSelectionMode), typeof(string))]
    public sealed class AutoPermSelectionModeToDisplayTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not AutoPermSelectionMode type)
            {
                return "";
            }

            return type switch
            {
                AutoPermSelectionMode.Closest => "Closest Edge",
                AutoPermSelectionMode.Highest => "Highest Edge",
                _ => "",
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
