using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(PairsType), typeof(string))]
    [ValueConversion(typeof(TriggerMethod), typeof(string))]
    public sealed class TriggerMethodToDisplayTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TriggerMethod pricingMode)
            {
                return pricingMode switch
                {
                    TriggerMethod.RBS => "RATIO BS",
                    TriggerMethod.RSB => "RATIO SB",
                    TriggerMethod.SBS => "SPREAD BS",
                    TriggerMethod.SSB => "SPREAD SB",
                    _ => "",
                };
            }
            else if (value is PairsType pairsType)
            {
                return pairsType switch
                {
                    PairsType.Ratio => "RATIO",
                    PairsType.Spread => "SPREAD",
                    _ => "",
                };
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
