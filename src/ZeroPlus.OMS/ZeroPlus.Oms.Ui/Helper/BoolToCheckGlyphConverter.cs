using DevExpress.Drawing;
using DevExpress.Images;
using DevExpress.Utils;
using DevExpress.Xpf.Core.Native;
using System;
using System.Globalization;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(bool), typeof(DXImage))]
    public sealed class BoolToCheckGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool boolValue && boolValue
                ? WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/Business Objects/BO_Validation.svg"))
                : WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/Icon Builder/Security_WarningCircled2.svg"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
