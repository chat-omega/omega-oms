using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(double), typeof(string))]
    public sealed class StatusModeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is not StatusMode)
                {
                    return "#FFFFFF";
                }
                StatusMode statusMode = (StatusMode)value;

                if (parameter == null || parameter is not string property)
                {
                    return "#FFFFFF";
                }

                return statusMode switch
                {
                    StatusMode.Reset => property == "FOREGROUND" ? "#FFFFFF" : "Transparent",
                    StatusMode.Pending => property == "FOREGROUND" ? "#FFFFFF" : "#953BBC",
                    StatusMode.NewBuy => property == "FOREGROUND" ? "Green" : "Transparent",
                    StatusMode.NewSell => property == "FOREGROUND" ? "Red" : "Transparent",
                    StatusMode.FilledBuy => property == "FOREGROUND" ? "#FFFFFF" : "#1D673F",
                    StatusMode.FilledSell => property == "FOREGROUND" ? "#FFFFFF" : "#832121",
                    StatusMode.CancelledBuy => property == "FOREGROUND" ? "DarkGreen" : "#FFFFFF",
                    StatusMode.CancelledSell => property == "FOREGROUND" ? "DarkRed" : "#FFFFFF",
                    StatusMode.RejectedBuy => property == "FOREGROUND" ? "#FFFFFF" : "Transparent",
                    StatusMode.RejectedSell => property == "FOREGROUND" ? "#FFFFFF" : "Transparent",
                    _ => "#FFFFFF",
                };
            }
            catch (Exception)
            {
                return "#FFFFFF";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
