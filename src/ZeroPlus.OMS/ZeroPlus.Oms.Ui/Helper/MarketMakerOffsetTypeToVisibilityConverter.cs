using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(MarketMakerOffsetType), typeof(Visibility))]
    public sealed class MarketMakerOffsetTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not MarketMakerOffsetType type)
            {
                return Visibility.Collapsed;
            }

            if ((type == MarketMakerOffsetType.PercentageOffset && (string)parameter == "Percentage") ||
                (type == MarketMakerOffsetType.PxOffset && (string)parameter == "Price"))
            {
                return Visibility.Visible;
            }
            else
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
