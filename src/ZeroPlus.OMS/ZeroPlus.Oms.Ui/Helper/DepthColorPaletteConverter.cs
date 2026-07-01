using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(int), typeof(Color))]
    public class DepthSideToColorConverter : IValueConverter
    {
        private const string RED_COLOR = "#832121";
        private const string GREEN_COLOR = "#1D673F";
        private const string WHITE_COLOR = "#FFFFFF";
        private const string MOUSE_OVER_COLOR = "#6480e8";
        private const string RED_FOCUSED_COLOR = "#A45E5E";
        private const string GREEN_FOCUSED_COLOR = "#2D7F59";

        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        private static Color[] backColorPalette = BlueBackColorPalette;
        private static Color[] foreColorPalette = BlueForeColorPalette;

        public static Color[] BlueBackColorPalette { [MethodImpl(MethodImplOptions.NoInlining)] get; } = new Color[6]
        {
            Color.FromArgb(41, 80, 225),
            Color.FromArgb(27, 58, 171),
            Color.FromArgb(18, 39, 121),
            Color.FromArgb(11, 27, 87),
            Color.FromArgb(7, 18, 60),
            Color.FromArgb(5, 9, 24)
        };

        public static Color[] BlueForeColorPalette { [MethodImpl(MethodImplOptions.NoInlining)] get; } = new Color[6]
        {
            Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue),
            Color.FromArgb(225, 225, 225),
            Color.FromArgb(225, 225, 225),
            Color.FromArgb(225, 225, 225),
            Color.FromArgb(225, 225, 225),
            Color.FromArgb(225, 225, 225)
        };

        public static Color[] RainbowBackColorPalette { [MethodImpl(MethodImplOptions.NoInlining)] get; } = new Color[6]
       {
           Color.FromArgb(247, 246, 24),
           Color.FromArgb(247, 152, 14),
           Color.FromArgb(44, 176, 207),
           Color.FromArgb(47, 102, 152),
           Color.FromArgb(16, 51, 102),
           Color.FromArgb(5, 9, 24)
       };

        public static Color[] RainbowForeColorPalette { [MethodImpl(MethodImplOptions.NoInlining)] get; } = new Color[6]
        {
            Color.FromArgb(36, 36, 36),
            Color.FromArgb(36, 36, 36),
            Color.FromArgb(36, 36, 36),
            Color.FromArgb(36, 36, 36),
            Color.FromArgb(225, 225, 225),
            Color.FromArgb(225, 225, 225)
        };


        static DepthSideToColorConverter()
        {
            OmsCore.Config.ConfigChangedEvent -= ConfigChangedEvent;
            OmsCore.Config.ConfigChangedEvent += ConfigChangedEvent;
            ConfigChangedEvent(OmsCore.Config, false);
        }

        public DepthSideToColorConverter()
        {
            OmsCore.Config.ConfigChangedEvent -= ConfigChangedEvent;
            OmsCore.Config.ConfigChangedEvent += ConfigChangedEvent;
            ConfigChangedEvent(OmsCore.Config, false);
        }

        private static void ConfigChangedEvent(Config.OmsConfig config, bool requiresRestart)
        {
            switch (config.DepthColorType)
            {
                case Oms.Enums.DepthColorType.UNIFORM:
                    backColorPalette = BlueBackColorPalette;
                    foreColorPalette = BlueForeColorPalette;
                    break;
                case Oms.Enums.DepthColorType.DISTINCT:
                    backColorPalette = RainbowBackColorPalette;
                    foreColorPalette = RainbowForeColorPalette;
                    break;
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level && parameter is string para)
            {
                switch (para.ToUpper())
                {
                    case "BACK":
                        if (level == -1)
                        {
                            return GREEN_COLOR;
                        }
                        else if (level == -2)
                        {
                            return RED_COLOR;
                        }
                        else
                        {
                            return HexConverter(GetBackColor(level));
                        }
                    case "FORE":
                        if (level < 0)
                        {
                            return WHITE_COLOR;
                        }
                        else
                        {
                            return HexConverter(GetForeColor(level));
                        }
                    case "MOVR":
                        if (level == -1)
                        {
                            return GREEN_FOCUSED_COLOR;
                        }
                        else if (level == -2)
                        {
                            return RED_FOCUSED_COLOR;
                        }
                        else
                        {
                            return MOUSE_OVER_COLOR;
                        }
                }
            }
            return Color.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

        public static Color GetBackColor(int level)
        {
            return level < 6 ? backColorPalette[level] : backColorPalette[5];
        }

        public static Color GetForeColor(int level)
        {
            return level < 6 ? foreColorPalette[level] : foreColorPalette[5];
        }

        private static string HexConverter(Color c)
        {
            string hex = "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
            return hex;
        }
    }
}
