using DevExpress.Xpf.Editors;
using System;
using System.Windows.Controls;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for PriceTrackBarView.xaml
    /// </summary>
    public partial class PriceTrackBarView : UserControl
    {
        private PriceTrackBarViewModel _viewModel;
        private bool _notifyPriceChange;
        private bool _keepInvalidValue;

        public PriceTrackBarView()
        {
            InitializeComponent();
            SetPriceRange(0, 0);
        }

        internal void SetPriceRange(double low, double high)
        {
            if (double.IsNaN(low) || double.IsInfinity(low))
            {
                low = 0;
            }
            if (double.IsNaN(high) || double.IsInfinity(high))
            {
                high = 0;
            }
            low *= 100.0;
            high *= 100.0;
            PriceTrackBar.Minimum = Math.Min(low, high);
            PriceTrackBar.Maximum = Math.Max(low, high);
        }

        internal void SetPrice(double price)
        {
            _notifyPriceChange = false;
            PriceTrackBar.Value = price * 100;
        }

        private void PriceTrackBar_EditValueChanging(object sender, EditValueChangingEventArgs e)
        {
            TrackBarEdit trackbar = sender as TrackBarEdit;

            if (e.OldValue != null)
            {
                if (((double)e.OldValue > trackbar.Maximum || (double)e.OldValue < trackbar.Minimum) && _keepInvalidValue)
                {
                    e.IsCancel = true;
                    e.Handled = true;
                    if ((double)e.NewValue > trackbar.Minimum && (double)e.NewValue < trackbar.Maximum)
                    {
                        _keepInvalidValue = false;
                    }
                }
                else
                {
                    _keepInvalidValue = true;
                }
            }
        }

        private void PriceTrackBar_EditValueChanged(object sender, EditValueChangedEventArgs e)
        {
            try
            {
                _viewModel ??= (PriceTrackBarViewModel)DataContext;
                if (_notifyPriceChange)
                {
                    _viewModel.PriceChanged((decimal)(double)e.NewValue / 100M);
                }
                else
                {
                    _notifyPriceChange = true;
                }
            }
            catch (Exception)
            {
            }
        }

        private void Grid_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (!PriceTrackBar.IsMouseOver)
            {
                if (e.Delta > 0)
                {
                    PriceTrackBar.Increment(PriceTrackBar.SmallStep);
                }
                else if (e.Delta < 0)
                {
                    PriceTrackBar.Decrement(PriceTrackBar.SmallStep);
                }
            }
        }
    }
}