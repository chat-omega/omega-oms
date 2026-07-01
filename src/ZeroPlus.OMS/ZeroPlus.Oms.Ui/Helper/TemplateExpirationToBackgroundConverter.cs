using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(double), typeof(string))]
    public sealed class TemplateExpirationToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is TemplateExpirationModel statusMode && statusMode != null)
                {
                    if (statusMode.IsExpired)
                    {
                        return "#FF6C67";
                    }
                    else if (!statusMode.IsRegular)
                    {
                        return "#59456E";
                    }
                }
                return "#171718";
            }
            catch (Exception)
            {
                return "#171718";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
