using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Models.Data.Enums;
namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(OrderType), typeof(bool))]
    public sealed class OrderTypeToLimitHandlingStateEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is OrderType type)
            {
                return type switch
                {
                    OrderType.Market => false,
                    _ => (object)true,
                };
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
