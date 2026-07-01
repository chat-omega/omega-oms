using System;
using System.Collections.Generic;
using ZeroPlus.Comms.Models.Data.MarketData;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Data
{
    public class DataPointModel
    {
        private double _totalBid;
        private double _totalAsk;
        private double _totalHwTv;
        private double _totalTheo;
        private double _totalVega;
        private double _totalDelta;
        private double _totalImplied;
        private double _totalTheta = double.NaN;
        private double _totalGamma = double.NaN;
        private int _roundingDigit = 2;
        private readonly HashSet<int> _legIds = new();

        public DateTime Timestamp { get; set; }
        public double BidIv { get; set; }
        public double AskIv { get; set; }
        public double Iv { get; set; }
        public double UnderPx { get; set; }
        public double UnderlyingFitted { get; set; }
        public double UnderMid { get; set; }
        public double TradePx { get; set; }
        public double Delta { get; set; }
        public double Vega { get; set; }
        public double Theta { get; set; }
        public double Gamma { get; set; }
        public double Implied { get; set; }

        public void AddResult(int legId, int ratio, OptionSnapshot result)
        {
            _legIds.Add(legId);
            _totalBid += ratio * (ratio > 0 ? result.Bid : result.Ask);
            _totalAsk += ratio * (ratio > 0 ? result.Ask : result.Bid);
            _totalHwTv += ratio * result.HwTV;
            _totalTheo += ratio * result.HwTV;
            _totalDelta += ratio * result.HwDelta;
            _totalVega += ratio * result.HwVega;
            _totalImplied += ratio * result.HwIV;
            _roundingDigit = result.HwVega > 6 ? 5 : result.HwVega > 1 ? 4 : 3;
            UnderPx = result.UnderLast1;
        }

        public void AddResult(int legId, SubscriptionFieldType type, int ratio, double price, OptionSnapshot result)
        {
            _legIds.Add(legId);
            if (ratio > 0)
            {
                switch (type)
                {
                    case SubscriptionFieldType.Bid:
                        _totalBid += ratio * price;
                        break;
                    case SubscriptionFieldType.TheorethicalValue:
                        _totalHwTv += ratio * price;
                        break;
                    case SubscriptionFieldType.Ask:
                        _totalAsk += ratio * price;
                        break;
                }
            }
            else
            {
                switch (type)
                {
                    case SubscriptionFieldType.Bid:
                        _totalAsk += ratio * price;
                        break;
                    case SubscriptionFieldType.TheorethicalValue:
                        _totalHwTv += ratio * price;
                        break;
                    case SubscriptionFieldType.Ask:
                        _totalBid += ratio * price;
                        break;
                }
            }

            _totalDelta += ratio * result.HwDelta;
            _totalVega += ratio * result.HwVega;
            _totalImplied += ratio * result.HwIV;
            _roundingDigit = result.HwVega > 6 ? 5 : result.HwVega > 1 ? 4 : 3;
            UnderPx = result.UnderLast1;
        }

        public void AddResult(int legId, SubscriptionFieldType type, int ratio, double price, double under)
        {
            _legIds.Add(legId);
            if (ratio > 0)
            {
                switch (type)
                {
                    case SubscriptionFieldType.Bid:
                        _totalBid += ratio * price;
                        break;
                    case SubscriptionFieldType.TheorethicalValue:
                        _totalHwTv += ratio * price;
                        break;
                    case SubscriptionFieldType.Ask:
                        _totalAsk += ratio * price;
                        break;
                }
            }
            else
            {
                switch (type)
                {
                    case SubscriptionFieldType.Bid:
                        _totalAsk += ratio * price;
                        break;
                    case SubscriptionFieldType.TheorethicalValue:
                        _totalHwTv += ratio * price;
                        break;
                    case SubscriptionFieldType.Ask:
                        _totalBid += ratio * price;
                        break;
                }
            }

            UnderPx = under;
        }

        public void AddResult(int legId, SubscriptionFieldType type, int ratio, double price, ZeroPlus.Models.Data.Responses.OptionSnapshot result)
        {
            _legIds.Add(legId);
            if (ratio > 0)
            {
                switch (type)
                {
                    case SubscriptionFieldType.Bid:
                        _totalBid += ratio * price;
                        break;
                    case SubscriptionFieldType.TheorethicalValue:
                        _totalHwTv += ratio * price;
                        break;
                    case SubscriptionFieldType.Ask:
                        _totalAsk += ratio * price;
                        break;
                }
            }
            else
            {
                switch (type)
                {
                    case SubscriptionFieldType.Bid:
                        _totalAsk += ratio * price;
                        break;
                    case SubscriptionFieldType.TheorethicalValue:
                        _totalHwTv += ratio * price;
                        break;
                    case SubscriptionFieldType.Ask:
                        _totalBid += ratio * price;
                        break;
                }
            }
            _totalDelta += ratio * result.Delta;
            _totalVega += ratio * result.Vega;
            _totalImplied += ratio * result.Iv;
            _roundingDigit = result.Vega > 6 ? 5 : result.Vega > 1 ? 4 : 3;
            UnderPx = (result.UnderBid + result.UnderAsk) / 2;
        }

        public bool TryCalculate(int totalLegs)
        {
            bool valid = totalLegs == _legIds.Count;
            if (valid)
            {
                Iv = Math.Round(_totalHwTv, _roundingDigit);
                BidIv = Math.Round(_totalHwTv - ((_totalTheo - _totalBid) / _totalVega / 100), _roundingDigit);
                AskIv = Math.Round(_totalHwTv + ((_totalAsk - _totalTheo) / _totalVega / 100), _roundingDigit);
            }

            return valid;
        }

        public bool TryRecalculate(int totalLegs)
        {
            bool valid = totalLegs == _legIds.Count;

            if (valid)
            {
                BidIv = Math.Round(_totalBid, _roundingDigit);
                Iv = Math.Round(_totalHwTv, _roundingDigit);
                AskIv = Math.Round(_totalAsk, _roundingDigit);
                Delta = Math.Round(_totalDelta, _roundingDigit);
                Vega = Math.Round(_totalVega, _roundingDigit);
                Theta = Math.Round(_totalTheta, _roundingDigit);
                Gamma = Math.Round(_totalGamma, _roundingDigit);
                Implied = Math.Round(_totalImplied, _roundingDigit);
            }

            return valid;
        }
    }
}
