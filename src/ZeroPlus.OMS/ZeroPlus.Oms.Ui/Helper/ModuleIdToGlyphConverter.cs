using DevExpress.Drawing;
using DevExpress.Images;
using DevExpress.Utils;
using DevExpress.Xpf.Core.Native;
using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Helper
{

    [ValueConversion(typeof(int), typeof(DXImage))]
    public class ModuleIdToGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is not int intVal)
                {
                    return null;
                }

                return (Module)intVal switch
                {
                    Module.OrderBook or Module.OrderBookLayout or Module.CustomOrderBook or Module.CustomOrderBookLayout => WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/Icon Builder/Actions_Book.svg")),
                    Module.ComplexOrderTicket or Module.ComplexOrderTicketLayout or Module.CombinedOrderTicket or Module.CombinedOrderTicketLayout or Module.BasketTrader or Module.BasketTraderLayout or Module.LockTrader or Module.LockTraderLayout => WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/RichEdit/RichEditBookmark.svg")),
                    Module.Trades or Module.TradesLayout => WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/Outlook Inspired/EmployeeTaskList.svg")),
                    Module.SpreadsGenerator or Module.SpreadsGeneratorLayout or Module.SpreadTemplate or Module.SpreadTemplateLayout => WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/XAF/ModelEditor_GenerateContent.svg")),
                    Module.DominatorsManager or Module.DominatorsManagerLayout or Module.BasketManager or Module.BasketManagerLayout => WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/Spreadsheet/ManageRelations.svg")),
                    Module.Heatmap or Module.HeatmapLayout => WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/RichEdit/SplitTableCells.svg")),
                    Module.Dashboard or Module.DashboardLayout => WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/Chart/ChartType_SideBySideBar3DStacked.svg")),
                    Module.OptionChain or Module.OptionChainLayout => WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/RichEdit/FloatingObjectLayoutOptions.svg")),
                    Module.Portfolio or Module.PortfolioLayout or Module.PositionManager => WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/Outlook Inspired/EmployeeTaskList.svg")),
                    Module.PnlReport or Module.PnlReportLayout => WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/Reports/SparklineWinLoss.svg")),
                    _ => null,
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
