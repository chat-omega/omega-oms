using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ZeroPlus.Oms.Ui.Enums;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(HedgeSuggestion), typeof(Visibility))]
    public sealed class HedgeSuggestionToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is not HedgeSuggestion)
                {
                    return Visibility.Collapsed;
                }

                bool inverse = false;
                if (parameter is string inverseParameter)
                {
                    inverse = true;
                }
                HedgeSuggestion hedgeSuggestion = (HedgeSuggestion)value;

                return hedgeSuggestion switch
                {
                    HedgeSuggestion.SuggestHedge => inverse ? Visibility.Collapsed : Visibility.Visible,
                    _ => (object)(inverse ? Visibility.Visible : Visibility.Collapsed),
                };
            }
            catch (Exception)
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
