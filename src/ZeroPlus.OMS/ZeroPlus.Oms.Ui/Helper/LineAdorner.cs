using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.Helper
{
    public class LineAdorner : Adorner
    {
        private readonly EdgeScanFeedView _edgeScanFeedView;
        private readonly BasketTraderView _basketTraderView;
        private readonly EdgeScanFeedViewModel _edgeScanFeedViewModel;

        public LineAdorner(EdgeScanFeedView window1, BasketTraderView window2) : base(window1)
        {
            _edgeScanFeedView = window1;
            _basketTraderView = window2;

            if (_edgeScanFeedView.DataContext is EdgeScanFeedViewModel viewModel)
            {
                _edgeScanFeedViewModel = viewModel;

                _edgeScanFeedView.LocationChanged += OnEdgeScanFeedMove;
                _basketTraderView.LocationChanged += OnBasketWindowMove;
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            Pen pen = new(_edgeScanFeedViewModel.BorderBrush, 2);
            Point startPoint = new(_edgeScanFeedView.Left + (_edgeScanFeedView.Width / 2), _edgeScanFeedView.Top);
            Point endPoint = new(_basketTraderView.Left + (_basketTraderView.Width / 2), _basketTraderView.Top);

            drawingContext.DrawLine(pen, startPoint, endPoint);
        }

        private void OnEdgeScanFeedMove(object sender, EventArgs e)
        {
            InvalidateVisual();
        }

        private void OnBasketWindowMove(object sender, EventArgs e)
        {
            InvalidateVisual();
        }
    }
}
