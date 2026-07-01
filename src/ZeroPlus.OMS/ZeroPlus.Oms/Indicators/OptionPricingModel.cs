using System;
using ZeroPlus.Comms.Models.OptionPricing;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;

namespace ZeroPlus.Oms.Indicators
{
    public class OptionPricingModel : IOmsDataSubscriber
    {
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public string Symbol { get; set; }
        public SubscriptionFieldType QuoteType { get; set; }
        public Greeks Greeks { get; set; }
        public double OriginalPrice { get; set; } = double.NaN;
        public double OptionPrice { get; set; } = double.NaN;
        public double Volatility { get; internal set; } = double.NaN;
        public double UnderlyingPrice { get; internal set; } = double.NaN;
        public bool IsDisposed { get; set; }

        public OptionPricingModel(string symbol, SubscriptionFieldType quoteType)
        {
            Symbol = symbol;
            QuoteType = quoteType;
            Greeks = new Greeks();

            OmsCore.GreekClient.Subscribe(symbol, SubscriptionFieldType.Delta, this);
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            if (key.Type == SubscriptionFieldType.Delta && value is double delta)
            {
                Greeks.Delta = delta;
            }
        }
    }
}
