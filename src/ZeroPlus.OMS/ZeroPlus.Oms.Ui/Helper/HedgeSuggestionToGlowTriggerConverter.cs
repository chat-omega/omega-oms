using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Oms.Ui.Enums;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(HedgeSuggestion), typeof(bool))]
    public sealed class HedgeSuggestionToGlowTriggerConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is not HedgeSuggestion)
                {
                    return false;
                }
                HedgeSuggestion hedgeSuggestion = (HedgeSuggestion)value;

                return hedgeSuggestion switch
                {
                    HedgeSuggestion.SuggestHedge => true,
                    _ => (object)false,
                };
            }
            catch (Exception)
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
