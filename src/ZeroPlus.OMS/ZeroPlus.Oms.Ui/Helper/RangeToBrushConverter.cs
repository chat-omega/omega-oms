using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(int), typeof(Brush))]
    [ValueConversion(typeof(double), typeof(Brush))]
    [ValueConversion(typeof(byte), typeof(Brush))]
    [ValueConversion(typeof(long), typeof(Brush))]
    [ValueConversion(typeof(float), typeof(Brush))]
    [ValueConversion(typeof(uint), typeof(Brush))]
    [ValueConversion(typeof(short), typeof(Brush))]
    [ValueConversion(typeof(sbyte), typeof(Brush))]
    [ValueConversion(typeof(ushort), typeof(Brush))]
    [ValueConversion(typeof(ulong), typeof(Brush))]
    [ValueConversion(typeof(decimal), typeof(Brush))]
    [ValueConversion(typeof(string), typeof(Brush))]
    public class RangeToBrushConverter : IValueConverter
    {
        private static readonly Brush DefaultLowBrush = new SolidColorBrush(Colors.Red);
        private static readonly Brush DefaultHighBrush = new SolidColorBrush(Colors.Green);
        private static readonly Brush DefaultBrush = new SolidColorBrush(Colors.Transparent);

        static RangeToBrushConverter()
        {
            DefaultLowBrush.Freeze();
            DefaultHighBrush.Freeze();
            DefaultBrush.Freeze();
        }

        public Brush AboveRangeBrush { get; set; }
        public Brush InbetweenRangeBrush { get; set; }
        public Brush BelowRangeBrush { get; set; }
        public bool UseAbsoluteValue { get; set; }
        public double RangeFloor { get; set; }
        public double RangeCeil { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (!IsSupportedType(value))
                {
                    return DependencyProperty.UnsetValue;
                }

                double doubleValue = 0.0;
                if (value is string valString)
                {
                    double.TryParse(valString, NumberStyles.Currency | NumberStyles.AllowCurrencySymbol | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out doubleValue);
                }
                else
                {
                    doubleValue = System.Convert.ToDouble(value);
                }

                if (UseAbsoluteValue)
                {
                    doubleValue = Math.Abs(doubleValue);
                }

                if (doubleValue < RangeFloor)
                {
                    return BelowRangeBrush ?? DefaultLowBrush;
                }
                else if (doubleValue >= RangeFloor && doubleValue <= RangeCeil)
                {
                    return InbetweenRangeBrush ?? DefaultHighBrush;
                }
                else if (doubleValue > RangeCeil)
                {
                    return AboveRangeBrush ?? DefaultHighBrush;
                }

                return DefaultBrush;
            }
            catch (Exception)
            {
                return DependencyProperty.UnsetValue;
            }
        }

        private static bool IsSupportedType(object value)
        {
            return value is int or double or byte or long or
                   float or uint or short or sbyte or
                   ushort or ulong or decimal or string;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
