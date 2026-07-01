using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(bool), typeof(GridLength))]
    public sealed class BooleanToGridColumnVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                bool inverse = false;
                if (parameter is not null and string parameterString)
                {
                    if (parameterString == "Inverse")
                    {
                        inverse = true;
                    }
                }
                if (value is bool val && val ^ inverse)
                {
                    return new GridLength(1, GridUnitType.Star);
                }
                return new GridLength(0);
            }
            catch (Exception)
            {
                return GridLength.Auto;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
