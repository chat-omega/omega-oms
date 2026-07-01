using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(Side), typeof(Brush))]
    public class WarnSideToWarnColorConverter : IValueConverter
    {
        private readonly Brush DefaultBuyBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#426349"));
        private readonly Brush DefaultSellBrursh = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#b7363d"));
        private readonly Brush DefaultBrush = new SolidColorBrush(Colors.White);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Side side)
            {
                switch (side)
                {
                    case ZeroPlus.Models.Data.Enums.Side.Buy:
                    case ZeroPlus.Models.Data.Enums.Side.BuyToCover:
                        return DefaultBuyBrush;
                    case ZeroPlus.Models.Data.Enums.Side.Sell:
                    case ZeroPlus.Models.Data.Enums.Side.SellShort:
                        return DefaultSellBrursh;
                }
            }
            return DefaultBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
