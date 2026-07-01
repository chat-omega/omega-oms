using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(PairOperator), typeof(string))]
    public sealed class PairOperatorToDisplayTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not PairOperator pricingMode)
            {
                return "";
            }

            return pricingMode switch
            {
                PairOperator.GT => ">",
                PairOperator.GTE => ">=",
                PairOperator.EQ => "=",
                PairOperator.LT => "<",
                PairOperator.LTE => "<=",
                _ => "",
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
