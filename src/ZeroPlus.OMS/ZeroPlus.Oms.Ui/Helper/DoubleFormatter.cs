using System;
using System.Globalization;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    public sealed class DoubleFormatter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!TryExtractDouble(value, out double val))
                return value;

            if (IsInvalidDouble(val))
                return targetType == typeof(string) ? string.Empty : null;

            DoubleFormat format = ResolveFormat(parameter);

            return targetType == typeof(string)
                ? FormatAsString(val, format, culture)
                : GetNumericValue(val, format);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

        private static bool TryExtractDouble(object value, out double result)
        {
            if (value is double d) { result = d; return true; }
            if (value is int i) { result = i; return true; }
            result = 0d;
            return false;
        }

        private static bool IsInvalidDouble(double value)
        {
            return double.IsNaN(value)
                || double.IsInfinity(value)
                || value == double.MinValue
                || value == double.MaxValue;
        }

        private static DoubleFormat ResolveFormat(object parameter)
        {
            if (parameter is DoubleFormat format)
                return format;

            if (parameter is not string key || string.IsNullOrEmpty(key))
                return DoubleFormat.Default;

            return key.ToUpperInvariant() switch
            {
                "NONZERO" => DoubleFormat.NonZero,
                "C0" => DoubleFormat.C0,
                "C" or "C2" => DoubleFormat.C2,
                "N0" => DoubleFormat.N0,
                "N2" => DoubleFormat.N2,
                "N3" => DoubleFormat.N3,
                "N4" => DoubleFormat.N4,
                "N6" => DoubleFormat.N6,
                "P" => DoubleFormat.P,
                "P2" => DoubleFormat.P2,
                "%" => DoubleFormat.Percent,
                "SIGFIG_PCT" => DoubleFormat.SigFigPct,
                "G3_PCT" => DoubleFormat.G3Pct,
                "RAW" => DoubleFormat.Raw,
                "GREEK" => DoubleFormat.Greek,
                "VAL" => DoubleFormat.Val,
                "TIMESPAN" => DoubleFormat.TimeSpan,
                _ => DoubleFormat.Default
            };
        }

        private static object GetNumericValue(double value, DoubleFormat format)
        {
            switch (format)
            {
                case DoubleFormat.NonZero:
                    return value == 0d ? null : value;

                case DoubleFormat.C0:
                case DoubleFormat.N0:
                    return Math.Round(value, 0);

                case DoubleFormat.C2:
                case DoubleFormat.N2:
                case DoubleFormat.Default:
                    return Math.Round(value, 2);

                case DoubleFormat.N3:
                    return Math.Round(value, 3);

                case DoubleFormat.N4:
                    return Math.Round(value, 4);

                case DoubleFormat.N6:
                    return Math.Round(value, 6);

                case DoubleFormat.P:
                case DoubleFormat.P2:
                case DoubleFormat.Percent:
                    return Math.Round(value * 100d, 2);

                case DoubleFormat.SigFigPct:
                    return value == 0d ? 0d : RoundToSignificantFigures(value * 100d, 3);

                case DoubleFormat.G3Pct:
                    return value == 0d ? 0d : RoundToSignificantFigures(value, 3);

                case DoubleFormat.Greek:
                    return Math.Round(value, OmsCore.Config.DecimalPlacesForGreeks, MidpointRounding.AwayFromZero);

                case DoubleFormat.TimeSpan:
                    return Math.Round(value, 4);

                case DoubleFormat.Val:
                case DoubleFormat.Raw:
                    return value;

                case DoubleFormat.I0:
                    return (int)Math.Round(value, 0);

                default:
                    return Math.Round(value, 2);
            }
        }

        private static object FormatAsString(double value, DoubleFormat format, CultureInfo culture)
        {
            switch (format)
            {
                case DoubleFormat.NonZero:
                    return value == 0d ? string.Empty : value.ToString(culture);

                case DoubleFormat.C0:
                    return value.ToString("C0", culture);

                case DoubleFormat.C2:
                    return value.ToString("C2", culture);

                case DoubleFormat.N0:
                    return value.ToString("N0", culture);

                case DoubleFormat.N2:
                    return value.ToString("N2", culture);

                case DoubleFormat.N3:
                    return value.ToString("N3", culture);

                case DoubleFormat.N4:
                    return value.ToString("N4", culture);

                case DoubleFormat.N6:
                    return value.ToString("N6", culture);

                case DoubleFormat.P2:
                    return value.ToString("P2", culture);

                case DoubleFormat.P:
                case DoubleFormat.Percent:
                    return value.ToString("P", culture);

                case DoubleFormat.SigFigPct:
                    return value == 0d ? "0" : (value * 100d).ToString("G3", culture) + "%";

                case DoubleFormat.G3Pct:
                    return value == 0d ? "0%" : value.ToString("G3", culture) + "%";

                case DoubleFormat.Raw:
                case DoubleFormat.Val:
                    return value.ToString(culture);

                case DoubleFormat.Greek:
                    return Math.Round(value, OmsCore.Config.DecimalPlacesForGreeks, MidpointRounding.AwayFromZero).ToString(culture);

                case DoubleFormat.TimeSpan:
                    return Math.Round(value, 4).ToString(culture);

                default:
                    return Math.Round(value, 2).ToString(culture);
            }
        }

        private static double RoundToSignificantFigures(double value, int significantFigures)
        {
            if (value == 0d) return 0d;
            double scale = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(value))) + 1 - significantFigures);
            return Math.Round(value / scale) * scale;
        }
    }
}
