using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Ui.Enums;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(object), typeof(string))]
    [ValueConversion(typeof(LoLaSignalTtl), typeof(string))]
    public sealed class EnumToDisplayTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return "";
            }
            if (value is LoLaSignalTtl loLaSignalTtl)
            {
                switch (loLaSignalTtl)
                {
                    case LoLaSignalTtl.Blank:
                        return "";
                    case LoLaSignalTtl.Time1614:
                        return "16:14";
                    case LoLaSignalTtl.Time1559:
                        return "15:59";
                    case LoLaSignalTtl.Min1:
                        return "1 Min";
                    case LoLaSignalTtl.Min15:
                        return "15 Min";
                    case LoLaSignalTtl.Hour1:
                        return "1 Hour";
                    case LoLaSignalTtl.Hour2:
                        return "2 Hour";
                    case LoLaSignalTtl.EOD:
                        return "EOD";
                    case LoLaSignalTtl.EOD_ETH:
                        return "EOD (ETH)";
                    default:
                        return loLaSignalTtl.ToString().FromCamelCase();
                }
            }

            var toString = value.ToString();

            if (toString != null && toString == toString.ToUpper() && toString.Contains("_"))
            {
                return toString.Replace("_", " ");
            }

            return toString.ToSpaced();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
