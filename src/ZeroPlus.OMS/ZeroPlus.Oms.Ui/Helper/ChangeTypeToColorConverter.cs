using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(ChangeTypes), typeof(string))]
    public sealed class ChangeTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is not ChangeTypes)
                {
                    return "Transparent";
                }
                ChangeTypes statusMode = (ChangeTypes)value;

                return statusMode switch
                {
                    ChangeTypes.NEW => "#1D673F",
                    ChangeTypes.ENH => "#ffcf4a",
                    ChangeTypes.BUG => "#b30b00",
                    _ => "Transparent",
                };
            }
            catch (Exception)
            {
                return "Transparent";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
