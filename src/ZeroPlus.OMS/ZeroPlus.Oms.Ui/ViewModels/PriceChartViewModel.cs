using DevExpress.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class PriceChartViewModel(IOptionPricingService pricingService) : ViewModelBase
    {
        private readonly IOptionPricingService _pricingService = pricingService ?? throw new ArgumentNullException(nameof(pricingService));


        private double spotPrice;
        private double strike;
        private double timeToExpiry;
        private double riskFreeRate;
        private double volatility;
        private double quantity;
        private string optionType;

        public double SpotPrice
        {
            get => spotPrice;
            set => SetValue(ref spotPrice, value, () => UpdatePnLData());
        }

        public double Strike
        {
            get => strike;
            set => SetValue(ref strike, value, () => UpdatePnLData());
        }

        public double TimeToExpiry
        {
            get => timeToExpiry;
            set => SetValue(ref timeToExpiry, value, () => UpdatePnLData());
        }

        public double RiskFreeRate
        {
            get => riskFreeRate;
            set => SetValue(ref riskFreeRate, value, () => UpdatePnLData());
        }

        public double Volatility
        {
            get => volatility;
            set => SetValue(ref volatility, value, () => UpdatePnLData());
        }

        public double Quantity
        {
            get => quantity;
            set => SetValue(ref quantity, value, () => UpdatePnLData());
        }

        public string OptionType
        {
            get => optionType;
            set => SetValue(ref optionType, value, () => UpdatePnLData());
        }

        public ObservableCollection<PnLDataPoint> PnLData { get; } = new ObservableCollection<PnLDataPoint>();

        private void UpdatePnLData()
        {
            try
            {
                PnLData.Clear();

                var initialParams = new OptionParameters(
                    SpotPrice, Strike, TimeToExpiry,
                    RiskFreeRate / 100, Volatility / 100,
                    OptionType == "Call"
                );

                // Generate price points with more precise stepping
                double minPrice = spotPrice * 0.5;
                double maxPrice = spotPrice * 1.5;
                int points = 100;
                double step = (maxPrice - minPrice) / (points - 1);

                for (int i = 0; i < points; i++)
                {
                    double currentSpot = minPrice + step * i;

                    var currentParams = initialParams with { SpotPrice = currentSpot };
                    var pnlParams = new PnLParameters(currentParams, initialParams, (int)Quantity);

                    double pnl = _pricingService.CalculatePnL(pnlParams);
                    PnLData.Add(new PnLDataPoint { SpotPrice = currentSpot, PnL = pnl });
                }
            }
            catch (Exception)
            {
            }
        }

        public class PnLDataPoint
        {
            public double SpotPrice { get; set; }
            public double PnL { get; set; }
        }

    }
}