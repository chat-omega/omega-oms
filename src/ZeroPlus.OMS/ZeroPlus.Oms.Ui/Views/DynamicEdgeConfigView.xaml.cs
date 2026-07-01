using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System;
using System.Windows.Input;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for DynamicEdgeConfigView.xaml
    /// </summary>
    public partial class DynamicEdgeConfigView : ThemedWindow
    {
        public DynamicEdgeConfigView()
        {
            InitializeComponent();
        }

        private void SelectAll(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is BaseEdit baseEdit)
                {
                    baseEdit.SelectAll();
                }
            }
            catch (Exception)
            {
            }
        }

        private void ResetEmaLimitSpinEdit(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            foreach (object selectedRow in DteGrid.SelectedItems)
            {
                if (selectedRow is DaysToExpirationEdgeModel model)
                {
                    model.MaxAllowedAboveEma = double.NaN;
                }
            }
        }

        private void ResetTheoLimitSpinEdit(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            foreach (object selectedRow in DteGrid.SelectedItems)
            {
                if (selectedRow is DaysToExpirationEdgeModel model)
                {
                    model.MaxAllowedAboveTheo = double.NaN;
                }
            }
        }


        private void ResetVolaLimitSpinEdit(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            foreach (object selectedRow in DteGrid.SelectedItems)
            {
                if (selectedRow is DaysToExpirationEdgeModel model)
                {
                    model.MaxAllowedAboveVola= double.NaN;
                }
            }
        }

        private void ResetMaxThroughTradePxLimitSpinEdit(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            foreach (object selectedRow in DteGrid.SelectedItems)
            {
                if (selectedRow is DaysToExpirationEdgeModel model)
                {
                    model.MaxThroughTradePx = double.NaN;
                }
            }
        }

        private void ResetMinMarketWidthLimitSpinEdit(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            foreach (object selectedRow in DteGrid.SelectedItems)
            {
                if (selectedRow is DaysToExpirationEdgeModel model)
                {
                    model.MinMarketWidth = double.NaN;
                }
            }
        }

        private void ResetMinMarketCrossLimitSpinEdit(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            foreach (object selectedRow in DteGrid.SelectedItems)
            {
                if (selectedRow is DaysToExpirationEdgeModel model)
                {
                    model.MinMarketCross = double.NaN;
                }
            }
        }

        private void ResetBidPercentLimitSpinEdit(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            foreach (object selectedRow in DteGrid.SelectedItems)
            {
                if (selectedRow is DaysToExpirationEdgeModel model)
                {
                    model.MaxAllowedPercentBid = double.NaN;
                }
            }
        }

        private void ResetDynamicBidPercentLimitSpinEdit(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            foreach (object selectedRow in DteGrid.SelectedItems)
            {
                if (selectedRow is DaysToExpirationEdgeModel model)
                {
                    model.DynamicMaxAllowedPercentBid = double.NaN;
                    model.DynamicMaxAllowedPercentBidAddition = double.NaN;
                }
            }
        }

        private void ResetDynamicEmaLimitSpinEdit(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            foreach (object selectedRow in DteGrid.SelectedItems)
            {
                if (selectedRow is DaysToExpirationEdgeModel model)
                {
                    model.DynamicMaxAllowedAboveEma = double.NaN;
                    model.DynamicMaxAllowedAboveEmaAddition = double.NaN;
                }
            }
        }

        private void ResetDynamicTheoLimitSpinEdit(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            foreach (object selectedRow in DteGrid.SelectedItems)
            {
                if (selectedRow is DaysToExpirationEdgeModel model)
                {
                    model.DynamicMaxAllowedAboveTheo = double.NaN;
                    model.DynamicMaxAllowedAboveTheoAddition = double.NaN;
                }
            }
        }

        private void ResetDynamicVolaLimitSpinEdit(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            foreach (object selectedRow in DteGrid.SelectedItems)
            {
                if (selectedRow is DaysToExpirationEdgeModel model)
                {
                    model.DynamicMaxAllowedAboveVola= double.NaN;
                    model.DynamicMaxAllowedAboveVolaAddition = double.NaN;
                }
            }
        }

        private void ResetDynamicWidthLimitSpinEdit(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            foreach (object selectedRow in DteGrid.SelectedItems)
            {
                if (selectedRow is DaysToExpirationEdgeModel model)
                {
                    model.DynamicMinMarketWidth = double.NaN;
                    model.DynamicMinMarketWidthAddition = double.NaN;
                }
            }
        }
    }
}
