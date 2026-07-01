using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Models
{
    public class SpreadDataSubscriber : DataSubscriber
    {
        public List<DataSubscriber> ChildSubscribers { get; set; }

        public SpreadDataSubscriber(OmsCore omsCore) : base(omsCore)
        {
            ChildSubscribers = new List<DataSubscriber>();
        }

        public new void Unsubscribe()
        {
            foreach (DataSubscriber subscriber in ChildSubscribers)
            {
                subscriber.DataSubscriberUpdated -= OnDataSubscriberUpdated;
                subscriber.Unsubscribe();
            }
        }

        internal void OnDataSubscriberUpdated(DataSubscriber dataSubscriber, SubscriptionFieldType updatedField)
        {
            double spreadBid = 0.0;
            double spreadAsk = 0.0;
            double spreadEma = 0.0;
            double spreadDelta = 0.0;
            double spreadVega = 0.0;
            double spreadTheo = 0.0;
            double spreadGamma = 0.0;
            double spreadTheta = 0.0;
            double spreadImplied = 0.0;
            double spreadRho = 0.0;
            double spreadAdjTheo = 0.0;

            foreach (DataSubscriber subscriber in ChildSubscribers)
            {
                double qtyAbs = Math.Abs(subscriber.Ratio);

                double bid = qtyAbs * subscriber.Bid;
                double ask = qtyAbs * subscriber.Ask;

                int side = subscriber.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? 1 : -1;

                switch (subscriber.Side)
                {
                    case ZeroPlus.Models.Data.Enums.Side.Buy:
                        spreadBid += side * bid;
                        spreadAsk += side * ask;
                        break;
                    default:
                        spreadBid += side * ask;
                        spreadAsk += side * bid;
                        break;
                }

                spreadEma += side * qtyAbs * subscriber.Ema;
                spreadDelta += side * qtyAbs * subscriber.Delta;
                spreadGamma += side * qtyAbs * subscriber.Gamma;
                spreadTheta += side * qtyAbs * subscriber.Theta;
                spreadVega += side * qtyAbs * subscriber.Vega;
                spreadRho += side * qtyAbs * subscriber.Rho;
                spreadImplied += side * qtyAbs * subscriber.Implied;
                spreadTheo += side * qtyAbs * subscriber.Theo;
                spreadAdjTheo += side * qtyAbs * subscriber.AdjTheo;
            }

            Bid = spreadBid;
            Ask = spreadAsk;
            Ema = spreadEma;
            Delta = spreadDelta;
            Vega = spreadVega;
            Theo = spreadTheo;
            Gamma = spreadGamma;
            Theta = spreadTheta;
            Implied = spreadImplied;
            Rho = spreadRho;
            AdjTheo = spreadAdjTheo;

            NotifyListeners(updatedField);
        }
    }
}
