using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Oms.Enums;
namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(PairTriggerType), typeof(bool))]
    public sealed class PairTriggerTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PairTriggerType pairTriggerType)
            {
                return pairTriggerType == PairTriggerType.Static;
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
