using System;
using System.Globalization;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(DateTime?), typeof(string))]
    public sealed class ExDividendToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is not DateTime exDividend)
                {
                    return "Transparent";
                }

                double timeSpan = (exDividend.Date - DateTime.Today).TotalDays;

                if (timeSpan < 7)
                {
                    return "#ff0000";
                }
                else if (timeSpan < 14)
                {
                    return "#ffa700";
                }
                else if (timeSpan < 21)
                {
                    return "#a3ff00";
                }
                else
                {
                    return "#2cba00";
                }
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
