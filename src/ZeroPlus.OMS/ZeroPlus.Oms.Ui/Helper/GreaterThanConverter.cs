using System;
using System.Globalization;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper;

[ValueConversion(typeof(int), typeof(bool))]
public class GreaterThanConverter : IValueConverter
{
    public int Limit { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue > Limit;
        }

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}