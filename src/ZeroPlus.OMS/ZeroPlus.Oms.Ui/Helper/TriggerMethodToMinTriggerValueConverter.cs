using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(PairsType), typeof(bool))]
    [ValueConversion(typeof(TriggerMethod), typeof(bool))]
    [ValueConversion(typeof(TriggerMethod), typeof(Visibility))]
    public sealed class TriggerMethodToMinTriggerValueConverter : IValueConverter
    {
        public bool Inverse { get; set; }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool returnType = false;
            if (value is TriggerMethod pricingMode)
            {
                returnType = pricingMode switch
                {
                    TriggerMethod.RBS or TriggerMethod.RSB => !Inverse,
                    TriggerMethod.SBS or TriggerMethod.SSB => Inverse,
                    _ => Inverse,
                };
            }
            else if (value is PairsType pairsType)
            {
                switch (pairsType)
                {
                    case PairsType.Ratio:
                        returnType = !Inverse;
                        break;
                    case PairsType.Spread:
                        returnType = Inverse;
                        break;
                }
            }

            if (targetType == typeof(Visibility))
            {
                return returnType ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                return returnType;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
