using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Helper;

[ValueConversion(typeof(TheoModel), typeof(string))]
[ValueConversion(typeof(EmaModel), typeof(string))]
public class TheoModelToDisplayTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (parameter is string prefix)
            {
                switch (value)
                {
                    case TheoModel theoModel:
                        switch (theoModel)
                        {
                            case TheoModel.Hanw:
                                return prefix + " [HW]";
                            case TheoModel.VolaV0:
                                return prefix + " [V0]";
                            case TheoModel.VolaV1:
                                return prefix + " [V1]";
                            case TheoModel.VolaV2:
                                return prefix + " [V2]";
                            case TheoModel.VolaV3:
                                return prefix + " [V3]";
                            default:
                                return prefix;
                        }
                    case EmaModel emaModel:
                        switch (emaModel)
                        {
                            case EmaModel.AdjDaEma:
                                return prefix + " [Adj DAE]";
                            case EmaModel.AdjEma:
                                return prefix + " [Adj EMA]";
                            case EmaModel.AdjVolaEma:
                                return prefix + " [Adj VOE]";
                            case EmaModel.DaEma:
                                return prefix + " [DAE]";
                            case EmaModel.Ema:
                                return prefix + " [EMA]";
                            case EmaModel.VolaEma:
                                return prefix + " [VOE]";
                            default:
                                return prefix;
                        }
                }
            }

            return value;
        }
        catch (Exception)
        {
            return value;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}