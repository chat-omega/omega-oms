using System;
using System.Collections.Generic;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Oms.Data.Models;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Automation
{
    public class PxCalculator
    {
        private readonly bool _isSingleLeg;
        public double Bid { get; set; } = double.NaN;
        public double Ask { get; set; } = double.NaN;
        public double NetTheo { get; set; } = double.NaN;
        public double NetHwTheo { get; set; } = double.NaN;
        public double TotalDelta { get; set; } = double.NaN;
        public int Lcd { get; set; } = 1;
        public OpsOrderModel Order { get; set; }
        public List<TicketLegModel> Legs { get; set; }
        public DataLoadedNotifier QuoteLoadedNotifier { get; set; }
        public DataLoadedNotifier TheoLoadedNotifier { get; set; }
        public DataLoadedNotifier GreeksLoadedNotifier { get; set; }

        public PxCalculator(int lcd, List<TicketLegModel> legs, OpsOrderModel mainOrder)
        {
            _isSingleLeg = legs.Count == 1;
            Lcd = lcd;
            Legs = legs;
            Order = mainOrder;
            QuoteLoadedNotifier = new();
            TheoLoadedNotifier = new();
            GreeksLoadedNotifier = new();
            SubscribeToData();
        }

        private void SubscribeToData()
        {
            foreach (var leg in Legs)
            {
                leg.LegUpdatedEvent += LegUpdatedEvent;
                leg.SubscribeToDataFeed();
            }
        }

        private void LegUpdatedEvent()
        {
            double bid = 0;
            double ask = 0;
            double netHwTheo = 0;
            double netTheo = 0;
            double totalDelta = 0;

            foreach (var leg in Legs)
            {
                double qtyAbs = _isSingleLeg ? 1 : Math.Abs(leg.Quantity);
                int side = leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy || _isSingleLeg ? 1 : -1;

                if (side == 1)
                {
                    bid += side * qtyAbs * leg.Bid;
                    ask += side * qtyAbs * leg.Ask;
                }
                else
                {
                    bid += side * qtyAbs * leg.Ask;
                    ask += side * qtyAbs * leg.Bid;
                }
#if DEBUG
                netTheo += side * (qtyAbs * leg.Theo);
#else
                netTheo += side * (qtyAbs * leg.DeltaAdjTheo);
#endif
                netHwTheo += side * (qtyAbs * leg.HanweckTV);
                totalDelta += side * leg.Delta * leg.Ratio;
            }

            var lcd = _isSingleLeg ? 1 : Lcd;

            Bid = bid / lcd;
            Ask = ask / lcd;
            NetHwTheo = netHwTheo / lcd;
            NetTheo = netTheo / lcd;
            TotalDelta = totalDelta;

            if (!double.IsNaN(Bid) && !double.IsNaN(Ask))
            {
                QuoteLoadedNotifier.Set();
            }

            if (!double.IsNaN(NetTheo))
            {
                TheoLoadedNotifier.Set();
            }

            if (!double.IsNaN(TotalDelta))
            {
                GreeksLoadedNotifier.Set();
            }
        }

        public void Dispose()
        {
            foreach (var leg in Legs)
            {
                leg.LegUpdatedEvent -= LegUpdatedEvent;
                leg.Dispose();
            }
        }
    }
}
