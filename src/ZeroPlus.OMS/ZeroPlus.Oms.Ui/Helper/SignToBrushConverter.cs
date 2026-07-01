using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ZeroPlus.Oms.Enums;

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
    [ValueConversion(typeof(Generated.QuoteChangeType), typeof(Brush))]
    [ValueConversion(typeof(ValueCompare), typeof(Brush))]
    public class SignToBrushConverter : IValueConverter
    {
        private static readonly Brush DefaultNegativeBrush = new SolidColorBrush(Colors.Red);
        private static readonly Brush DefaultPositiveBrush = new SolidColorBrush(Colors.Green);
        private static readonly Brush DefaultZeroBrush = new SolidColorBrush(Colors.White);

        static SignToBrushConverter()
        {
            DefaultNegativeBrush.Freeze();
            DefaultPositiveBrush.Freeze();
            DefaultZeroBrush.Freeze();
        }

        public Brush NegativeBrush { get; set; }
        public Brush PositiveBrush { get; set; }
        public Brush ZeroBrush { get; set; }
        public Brush InvalidBrush { get; set; }

        public double Limit { get; set; } = 0d;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (!IsSupportedType(value))
                {
                    return DependencyProperty.UnsetValue;
                }

                if (value is Generated.QuoteChangeType quoteChangeType)
                {
                    switch (quoteChangeType)
                    {
                        case Generated.QuoteChangeType.Up:
                            return PositiveBrush;
                        case Generated.QuoteChangeType.None:
                            return ZeroBrush;
                        case Generated.QuoteChangeType.Down:
                            return NegativeBrush;
                        case Generated.QuoteChangeType.NULL_VALUE:
                            return ZeroBrush;
                    }
                }

                if (value is ValueCompare valueCompare)
                {
                    switch (valueCompare)
                    {
                        case ValueCompare.Above:
                            return PositiveBrush;
                        case ValueCompare.Equal:
                            return ZeroBrush;
                        case ValueCompare.Below:
                            return NegativeBrush;
                        case ValueCompare.Invalid:
                            return InvalidBrush;
                    }
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

                if (doubleValue < Limit)
                {
                    return NegativeBrush ?? DefaultNegativeBrush;
                }

                if (doubleValue > Limit)
                {
                    return PositiveBrush ?? DefaultPositiveBrush;
                }

                return ZeroBrush ?? DefaultZeroBrush;
            }
            catch (Exception)
            {
                return DependencyProperty.UnsetValue;
            }
        }

        private static bool IsSupportedType(object value)
        {
            return value is int
                         or double
                         or byte
                         or long
                         or float
                         or uint
                         or short
                         or sbyte
                         or ushort
                         or ulong
                         or decimal
                         or string
                         or Generated.QuoteChangeType
                         or ValueCompare;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
