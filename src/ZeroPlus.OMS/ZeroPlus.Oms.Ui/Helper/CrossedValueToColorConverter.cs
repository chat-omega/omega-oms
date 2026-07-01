using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Oms.Ui.Enums;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(OptionChainMktMkrMode), typeof(string))]
    public sealed class CrossedValueToColorConverter : IValueConverter
    {
        private const string REGULAR = "#f1f1f1";
        private const string CROSSED_MARKET = "#619fd7";
        private const string ABOVE_THEO_BID = "#b8d7a3";
        private const string ABOVE_THEO_ASK = "#d69d85";
        private const string CROSSED_ABOVE_THEO_BID = "#4ec9b0";
        private const string CROSSED_ABOVE_THEO_ASK = "#d8a0df";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is OptionChainMktMkrMode mode && parameter is string bidAsk)
                {
                    switch (mode)
                    {
                        case OptionChainMktMkrMode.Normal:
                            return REGULAR;
                        case OptionChainMktMkrMode.CrossedMarket:
                            return CROSSED_MARKET;
                        case OptionChainMktMkrMode.AboveTheo:
                            switch (bidAsk)
                            {
                                case "BID":
                                    return ABOVE_THEO_BID;
                                case "ASK":
                                    return ABOVE_THEO_ASK;
                            }
                            break;
                        case OptionChainMktMkrMode.CrossedAndAboveTheo:
                            switch (bidAsk)
                            {
                                case "BID":
                                    return CROSSED_ABOVE_THEO_BID;
                                case "ASK":
                                    return CROSSED_ABOVE_THEO_ASK;
                            }
                            break;
                    }
                }
                return REGULAR;
            }
            catch (Exception)
            {
                return REGULAR;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
