using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(bool), typeof(GridLength))]
    public sealed class MultiBooleanToGridVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
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
                foreach (object value in values)
                {
                    if (value is bool val && val ^ inverse)
                    {
                        return new GridLength(1, GridUnitType.Star);
                    }
                }
                return GridLength.Auto;
            }
            catch (Exception)
            {
                return GridLength.Auto;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
