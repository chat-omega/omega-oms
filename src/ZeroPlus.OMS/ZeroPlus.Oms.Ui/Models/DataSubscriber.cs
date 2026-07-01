using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Updates;

namespace ZeroPlus.Oms.Ui.Models
{
    public delegate void DataSubscriberUpdatedHandler(DataSubscriber dataSubscriber, SubscriptionFieldType updatedField);
    public class DataSubscriber : IOmsDataSubscriber
    {
        public event DataSubscriberUpdatedHandler DataSubscriberUpdated;
        public OmsCore OmsCore;
        public bool IsDisposed { get; set; }

        public string Symbol { get; set; }
        public Side Side { get; set; }
        public int Ratio { get; set; }
        public List<SubscriptionFieldType> SubscriptionFields { get; set; }

        public double Bid { get; set; } = double.NaN;
        public double Ask { get; set; } = double.NaN;
        public double Ema { get; set; } = double.NaN;
        public double Delta { get; set; } = double.NaN;
        public double Vega { get; set; } = double.NaN;
        public double Theo { get; set; } = double.NaN;
        public double Gamma { get; set; } = double.NaN;
        public double Theta { get; set; } = double.NaN;
        public double Implied { get; set; } = double.NaN;
        public double Rho { get; set; } = double.NaN;
        public double AdjTheo { get; set; } = double.NaN;

        public DataSubscriber(OmsCore omsCore)
        {
            OmsCore = omsCore;
            SubscriptionFields = new();
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            SubscriptionFieldType field = key.Type;

            switch (field)
            {
                case SubscriptionFieldType.Bid when value is double bid:
                    Bid = bid;
                    break;
                case SubscriptionFieldType.Ask when value is double ask:
                    Ask = ask;
                    break;
                case SubscriptionFieldType.Ema when value is double ema:
                    Ema = ema;
                    break;
                case SubscriptionFieldType.FullEma when value is EmaUpdateModel emaUpdate:
                    Ema = emaUpdate.MidPeriodEmaAdj;
                    break;
                case SubscriptionFieldType.Greeks when value is GreekUpdate greekUpdate:
                    Delta = greekUpdate.Delta;
                    Vega = greekUpdate.Vega;
                    Theo = greekUpdate.Theo;
                    Gamma = greekUpdate.Gamma;
                    Theta = greekUpdate.Theta;
                    Implied = greekUpdate.Implied;
                    Rho = greekUpdate.Rho;
                    break;
                case SubscriptionFieldType.DeltaAdjTheo when value is DeltaAdjTheo adjTheoUpdate:
                    AdjTheo = adjTheoUpdate.DeltaAdjustedTheo;
                    break;
            }

            NotifyListeners(field);
        }

        protected void NotifyListeners(SubscriptionFieldType field)
        {
            DataSubscriberUpdated?.Invoke(this, field);
        }

        public void Subscribe(string symbol, List<SubscriptionFieldType> fields)
        {
            Symbol = symbol;
            SubscriptionFields = fields;
            foreach (var field in SubscriptionFields)
            {
                switch (field)
                {
                    case SubscriptionFieldType.Bid:
                    case SubscriptionFieldType.Ask:
                        OmsCore.QuoteClient.Subscribe(Symbol, field, this);
                        break;
                    case SubscriptionFieldType.Greek:
                        OmsCore.GreekClient.Subscribe(Symbol, field, this);
                        break;
                    case SubscriptionFieldType.Ema:
                    case SubscriptionFieldType.FullEma:
                    case SubscriptionFieldType.DeltaAdjTheo:
                        OmsCore.UpdateManager.Subscribe(Symbol, field, this);
                        break;
                }
            }
        }

        public void Unsubscribe()
        {
            foreach (var field in SubscriptionFields)
            {
                switch (field)
                {
                    case SubscriptionFieldType.Bid:
                    case SubscriptionFieldType.Ask:
                        OmsCore.QuoteClient.Unsubscribe(Symbol, field, this);
                        break;
                    case SubscriptionFieldType.Greek:
                        OmsCore.GreekClient.Unsubscribe(Symbol, field, this);
                        break;
                    case SubscriptionFieldType.Ema:
                    case SubscriptionFieldType.FullEma:
                    case SubscriptionFieldType.DeltaAdjTheo:
                        OmsCore.UpdateManager.Unsubscribe(Symbol, field, this);
                        break;
                }
            }
        }
    }
}
