using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ZeroPlus.Oms.Ui.Enums;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(OptionChainMktMkrMode), typeof(FontWeight))]
    public sealed class CrossedValueToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is OptionChainMktMkrMode mode && parameter is string bidAsk)
                {
                    switch (mode)
                    {
                        case OptionChainMktMkrMode.Normal:
                        case OptionChainMktMkrMode.AboveTheo:
                            return FontWeights.Normal;
                        case OptionChainMktMkrMode.CrossedMarket:
                            return FontWeights.DemiBold;
                        case OptionChainMktMkrMode.CrossedAndAboveTheo:
                            return FontWeights.Bold;
                    }
                }
                return FontWeights.Normal;
            }
            catch (Exception)
            {
                return FontWeights.Normal;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
