using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Oms.Enums;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(InstanceMode), typeof(bool))]
    public sealed class BasketAutomationModeToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is InstanceMode mode && parameter is string lType)
            {
                return mode.ToString().ToUpper() == lType.ToUpper();
            }
            else
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
