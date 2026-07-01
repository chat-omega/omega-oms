using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(MarketMakerOffsetType), typeof(string))]
    public sealed class MarketMakerOffsetTypeToDisplayTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not MarketMakerOffsetType type)
            {
                return "";
            }

            return type switch
            {
                MarketMakerOffsetType.PxOffset => "Px Offset",
                MarketMakerOffsetType.PercentageOffset => "% Offset",
                MarketMakerOffsetType.Off => "Offset Off",
                _ => "",
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
