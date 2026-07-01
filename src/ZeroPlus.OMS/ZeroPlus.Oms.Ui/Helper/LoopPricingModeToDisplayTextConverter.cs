using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Extensions;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(LoopPricingMode), typeof(string))]
    public sealed class LoopPricingModeToDisplayTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not LoopPricingMode pricingMode)
            {
                return "";
            }

            if (parameter is string s)
            {
                switch (pricingMode)
                {
                    case LoopPricingMode.PriceIncrement:
                        return "Px Inc";
                    case LoopPricingMode.DeltaAdjustedLastFillPrice:
                        return "Delta Adj";
                    case LoopPricingMode.LimitedDeltaAdjustedLastFillPrice:
                        return "Limit Adj";
                    case LoopPricingMode.BadMarketLimitedDeltaAdjLastFillPx:
                        return "Bad Mkt Limit Adj";
                    case LoopPricingMode.AdjustedTheoPeggedLastFillPrice:
                        return "Theo Adj";
                    default:
                        return pricingMode.ToString().FromCamelCase();
                }
            }

            switch (pricingMode)
            {
                case LoopPricingMode.PriceIncrement:
                    return "Px Increment";
                case LoopPricingMode.DeltaAdjustedLastFillPrice:
                    return "Delta Adj Px";
                case LoopPricingMode.LimitedDeltaAdjustedLastFillPrice:
                    return "Limited Delta Adj Px";
                case LoopPricingMode.BadMarketLimitedDeltaAdjLastFillPx:
                    return "Bad Mkt Limited Delta Adj Px";
                case LoopPricingMode.AdjustedTheoPeggedLastFillPrice:
                    return "Adjust With Adj Theo";
                default:
                    return pricingMode.ToString().FromCamelCase();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
