using DevExpress.Mvvm;
using ZeroPlus.Oms.Ui.Services;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class PriceTrackBarViewModel : ViewModelBase
    {
        public delegate void PriceUpdatedEventHandler(decimal price);

        public event PriceUpdatedEventHandler PriceUpdatedEvent;

        public ISetPriceRangeService ISetPriceRangeService => GetService<ISetPriceRangeService>();

        [Bindable]
        public partial double Low { get; set; }

        [Bindable]
        public partial double High { get; set; }

        [Bindable]
        public partial double MinimumRange { get; set; }

        [Bindable]
        public partial double MaximumRange { get; set; }

        [Bindable]
        public partial decimal PriceIncrement { get; set; }

        public void SetIncrement(decimal increment)
        {
            PriceIncrement = increment * 100M;
        }

        public void SetLowHigh(double low, double high)
        {
            Low = low;
            High = high;

            ISetPriceRangeService.SetPriceRange(low, high);
        }

        public void PriceChanged(decimal price)
        {
            PriceUpdatedEvent?.Invoke(price);
        }

        internal void SetPrice(double price)
        {
            ISetPriceRangeService.SetPrice(price);
        }
    }
}
