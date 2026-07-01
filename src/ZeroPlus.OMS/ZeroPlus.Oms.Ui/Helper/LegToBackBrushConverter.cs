using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(double), typeof(string))]
    public sealed class LegToBackBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is TicketLegModel model)
                {
                    if (parameter == null || parameter is not string key)
                    {
                        return "#FF0000";
                    }

                    switch (key.ToUpper())
                    {
                        case "EXP":
                            if (model.IsExpirationValid)
                            {
                                if (model.Side == Side.Buy)
                                {
                                    return "#1D673F";
                                }
                                else
                                {
                                    return "#832121";
                                }
                            }
                            else
                            {
                                return "#FF0000";
                            }
                        case "STRIKE":
                            if (model.IsStrikeValid)
                            {
                                if (model.Side == Side.Buy)
                                {
                                    return "#1D673F";
                                }
                                else
                                {
                                    return "#832121";
                                }
                            }
                            else
                            {
                                return "#FF0000";
                            }
                    }
                }
                return "#FF0000";
            }
            catch (Exception)
            {
                return "#FF0000";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
