using DevExpress.Mvvm.UI;
using System;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.Services
{
    public class SetPriceRangeService : ServiceBase, ISetPriceRangeService
    {
        private PriceTrackBarView AssociatedBar => AssociatedObject as PriceTrackBarView;

        public void SetPrice(double price)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                AssociatedBar.SetPrice(price);
            }));
        }

        public void SetPriceRange(double low, double high)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                AssociatedBar.SetPriceRange(low, high);
            }));
        }
    }
}
