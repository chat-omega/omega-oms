using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(Side), typeof(Brush))]
    public class SideToColorConverter : IValueConverter
    {
        private static readonly Brush DefaultBuyBrush = new SolidColorBrush(Color.FromRgb(29, 103, 63));
        private static readonly Brush DefaultSellBrursh = new SolidColorBrush(Color.FromRgb(131, 33, 33));
        private static readonly Brush DefaultBrush = new SolidColorBrush(Colors.White);

        public bool Inverse { get; set; }

        static SideToColorConverter()
        {
            DefaultBuyBrush.Freeze();
            DefaultSellBrursh.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Side side)
            {
                if (!Inverse)
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
                else
                {
                    switch (side)
                    {
                        case ZeroPlus.Models.Data.Enums.Side.Buy:
                        case ZeroPlus.Models.Data.Enums.Side.BuyToCover:
                            return DefaultSellBrursh;
                        case ZeroPlus.Models.Data.Enums.Side.Sell:
                        case ZeroPlus.Models.Data.Enums.Side.SellShort:
                            return DefaultBuyBrush;
                    }
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
