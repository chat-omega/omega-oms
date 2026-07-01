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
    public class CashPercentageToGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked)
            {
                var resourceUri = isChecked ? 
                    AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/Spreadsheet/PercentStyle.svg") : 
                    AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/Icon Builder/Business_Dollar.svg");
                return WpfSvgRenderer.CreateImageSource(resourceUri);
            }

            return WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/Icon Builder/Business_Dollar.svg"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
