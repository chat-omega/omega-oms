using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(double), typeof(string))]
    public sealed class ChartConstantLineModeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is not LineMode)
                {
                    return "#e74032";
                }
                LineMode lineMode = (LineMode)value;

                return lineMode switch
                {
                    LineMode.Primary => "#e74032",
                    LineMode.Secondary => "#48cb8e",
                    _ => "#e74032",
                };
            }
            catch (Exception)
            {
                return "#e74032";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
