using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Oms.Ui.Enums;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(HedgeSuggestion), typeof(string))]
    public sealed class HedgeSuggestionToBorderColorConverter : IValueConverter
    {
        public string GreenColor { get; set; } = "#aaf707";
        public string RedColor { get; set; } = "#ff5c5b";
        public string DefaultColor { get; set; } = "#3f3f46";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is not HedgeSuggestion)
                {
                    return DefaultColor;
                }
                HedgeSuggestion hedgeSuggestion = (HedgeSuggestion)value;

                return hedgeSuggestion switch
                {
                    HedgeSuggestion.SuggestHedge => GreenColor,
                    HedgeSuggestion.DoNotSuggestHedge => RedColor,
                    _ => DefaultColor,
                };
            }
            catch (Exception)
            {
                return DefaultColor;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
