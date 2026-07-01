using DevExpress.Mvvm;
using System;
using ZeroPlus.Comms.Models.Data.MarketData;
using ZeroPlus.Comms.Models.OptionPricing;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class SymbolModel : BindableBase, IOmsDataSubscriber
    {
        public string _Underlying;
        public string _Symbol;
        public Side _Side;
        public int _Quantity;
        public double _Multiplier;
        public DateTime _Expiration;
        public double _Strike;
        public PutCall _PutCall;
        public bool _Loaded;
        public double _Iv;
        public double _Delta;
        public double _Gamma;
        public double _Theta;
        public double _Vega;
        public double _NetDelta;
        public double _NetGamma;
        public double _NetTheta;
        public double _Mid;
        public double _NewMid;

        public OmsCore OmsCore { get; }
        [Bindable]
        public partial string Underlying { get; set; }
        [Bindable]
        public partial string Symbol { get; set; }
        [Bindable]
        public partial Side Side { get; set; }
        [Bindable]
        public partial int Quantity { get; set; }
        [Bindable]
        public partial double Multiplier { get; set; }
        [Bindable]
        public partial DateTime Expiration { get; set; }
        [Bindable]
        public partial double Strike { get; set; }
        [Bindable]
        public partial PutCall PutCall { get; set; }
        [Bindable]
        public partial bool Loaded { get; set; }
        [Bindable]
        public partial double Iv { get; set; }
        [Bindable]
        public partial double Delta { get; set; }
        [Bindable]
        public partial double Gamma { get; set; }
        [Bindable]
        public partial double Theta { get; set; }
        [Bindable]
        public partial double Vega { get; set; }
        [Bindable]
        public partial double NetDelta { get; set; }
        [Bindable]
        public partial double NetGamma { get; set; }
        [Bindable]
        public partial double NetTheta { get; set; }
        [Bindable]
        public partial double Mid { get; set; }
        [Bindable]
        public partial double NewMid { get; set; }
        public bool IsDisposed { get; set; }

        public SymbolModel()
        {
            Iv = double.NaN;
            Delta = double.NaN;
            Gamma = double.NaN;
            Theta = double.NaN;
            Vega = double.NaN;
            NetDelta = double.NaN;
            NetGamma = double.NaN;
            NetTheta = double.NaN;
            Mid = double.NaN;
            OmsCore = ServiceLocator.GetService<OmsCore>();
        }

        internal Greeks UpdateGreeks(MDUnderlying underlyingDetails, double mid, double dteOffset, double ivOffset)
        {
            Greeks greeks = new();

            if (!Loaded)
            {
                greeks = new Greeks()
                {
                    Delta = double.NaN,
                    Gamma = double.NaN,
                    Theta = double.NaN,
                };
                return greeks;
            }

            double totalDays = (Expiration - DateTime.Now).TotalDays - dteOffset;
            if (totalDays < 0)
            {
                totalDays = 0;
            }
            else
            {
                totalDays += 1;
            }

            PricingParameters pricingParameters = new()
            {
                Volatility = 0.0,
                PutCall = PutCall == PutCall.Put ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call,
                Strike = Strike,
                DaysToExpiration = totalDays,
                RiskFreeRate = underlyingDetails.RiskFreeRate,
                StockRate = underlyingDetails.StockRate,
                UnderlyingPrice = mid,
                UnderlyingMultiplier = underlyingDetails.Multiplier,
                ExerciseStyle = Underlying.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American
            };
            pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, mid, underlyingDetails.Dividends, DateTime.Now);

            OptionModel.Binomial.ImpliedVolatility(pricingParameters, Mid, greeks);

            Delta = greeks.Delta;
            Gamma = greeks.Gamma;
            Theta = greeks.Theta;
            Vega = greeks.Vega;

            NetDelta = Delta * Quantity * Multiplier;
            NetGamma = Gamma * Quantity * Multiplier;
            NetTheta = Theta * Quantity * Multiplier;

            pricingParameters.Volatility = Iv + (Iv * ivOffset);
            NewMid = OptionModel.Binomial.PriceOption(pricingParameters, greeks);

            return greeks;
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            string symbol = key.Symbol;
            SubscriptionFieldType subscriptionFieldType = key.Type;

            if (symbol == Symbol)
            {
                switch (subscriptionFieldType)
                {
                    case SubscriptionFieldType.MidPoint:
                        {
                            if (value is double mid)
                            {
                                Mid = mid;
                            }
                            break;
                        }

                    case SubscriptionFieldType.ImpliedVol:
                        {
                            if (value is double iv)
                            {
                                Iv = iv;
                            }
                            break;
                        }
                }
            }
        }

        internal void Subscribe()
        {
            OmsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.MidPoint, this);
            OmsCore.GreekClient.Subscribe(Symbol, SubscriptionFieldType.ImpliedVol, this);
            Loaded = true;
        }

        internal void Unsubscribe()
        {
            OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.MidPoint, this);
            OmsCore.GreekClient.Unsubscribe(Symbol, SubscriptionFieldType.ImpliedVol, this);
            Loaded = true;
            IsDisposed = true;
        }
    }
}
