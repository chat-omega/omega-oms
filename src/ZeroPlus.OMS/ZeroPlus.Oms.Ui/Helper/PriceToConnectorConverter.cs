using System;
using System.Globalization;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(double), typeof(string))]
    public sealed class PriceToConnectorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is double valueDouble)
                {
                    if (parameter == null || parameter is not string key)
                    {
                        return "";
                    }

                    switch (key.ToUpper())
                    {
                        case "BUTTON":
                            if (valueDouble == 0)
                            {
                                return "EVEN";
                            }
                            else if (valueDouble < 0)
                            {
                                return "CR";
                            }
                            else if (valueDouble > 0)
                            {
                                return "DR";
                            }
                            break;
                        case "NET":
                            if (valueDouble == 0)
                            {
                                return "";
                            }
                            else if (valueDouble < 0)
                            {
                                return "CREDIT";
                            }
                            else if (valueDouble > 0)
                            {
                                return "DEBIT";
                            }
                            break;
                        case "CON":
                            if (valueDouble < 0)
                            {
                                return "@";
                            }
                            else if (valueDouble >= 0)
                            {
                                return "for";
                            }
                            break;
                    }
                }
                else if (value is bool valueBool)
                {
                    if (parameter == null || parameter is not string key)
                    {
                        return "";
                    }

                    switch (key.ToUpper())
                    {
                        case "BUTTON":
                            if (valueBool)
                            {
                                return "CR";
                            }
                            else if (!valueBool)
                            {
                                return "DR";
                            }
                            break;
                        case "NET":
                            if (valueBool)
                            {
                                return "CREDIT";
                            }
                            else if (!valueBool)
                            {
                                return "DEBIT";
                            }
                            break;
                        case "BS":
                            if (valueBool)
                            {
                                return "SELL";
                            }
                            else if (!valueBool)
                            {
                                return "BUY";
                            }
                            break;
                        case "BSLB":
                            if (valueBool)
                            {
                                return "SELL Lock BID";
                            }
                            else if (!valueBool)
                            {
                                return "BUY Lock BID";
                            }
                            break;
                        case "BSLA":
                            if (valueBool)
                            {
                                return "SELL Lock ASK";
                            }
                            else if (!valueBool)
                            {
                                return "BUY Lock ASK";
                            }
                            break;
                        case "BSLM":
                            if (valueBool)
                            {
                                return "SELL Lock MID";
                            }
                            else if (!valueBool)
                            {
                                return "BUY Lock MID";
                            }
                            break;
                        case "BA":
                            if (valueBool)
                            {
                                return "ASK";
                            }
                            else if (!valueBool)
                            {
                                return "BID";
                            }
                            break;
                        case "BS3":
                            if (valueBool)
                            {
                                return "SELL - 3 WAY SIDE";
                            }
                            else if (!valueBool)
                            {
                                return "BUY - 3 WAY SIDE";
                            }
                            break;
                        case "BSQ":
                            if (valueBool)
                            {
                                return "SELL QTY";
                            }
                            else if (!valueBool)
                            {
                                return "BUY QTY";
                            }
                            break;
                        case "CON":
                            if (valueBool)
                            {
                                return "@";
                            }
                            else if (!valueBool)
                            {
                                return "for";
                            }
                            break;
                    }
                }

                return "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
