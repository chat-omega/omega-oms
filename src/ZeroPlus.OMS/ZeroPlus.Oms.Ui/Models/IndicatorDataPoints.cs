using DevExpress.Mvvm;
using Newtonsoft.Json;
using System;

namespace ZeroPlus.Oms.Ui.Models
{
    public class IndicatorDataPoints : BindableBase
    {
        private double _bid;
        private double _mid;
        private double _ask;
        private double _macd;
        private double _fastEma;
        private double _slowEma;
        private double _signal;
        private double _bar;
        private double _bidEma;
        private double _midEma;
        private double _midEma2;
        private double _midEma3;
        private double _askEma;
        private double _highestBid;
        private double _lowestAsk;

        [JsonProperty]
        public DateTime Timestamp { get; set; }
        [JsonProperty]
        public bool Updated { get; set; }
        [JsonProperty]
        public DateTime HighestBidUpdateTime { get; set; }
        [JsonProperty]
        public DateTime LowestAskUpdateTime { get; set; }

        [JsonProperty]
        public double Bid
        {
            get => _bid;
            set
            {
                double update = Math.Round(value, 2);
                if (_bid != update)
                {
                    SetValue(ref _bid, update);
                    Updated = true;
                }
            }
        }
        [JsonProperty]
        public double Mid
        {
            get => _mid;
            set
            {
                double update = Math.Round(value, 2);
                if (_mid != update)
                {
                    SetValue(ref _mid, update);
                    Updated = true;
                }
            }
        }
        [JsonProperty]
        public double Ask
        {
            get => _ask;
            set
            {
                double update = Math.Round(value, 2);
                if (_ask != update)
                {
                    SetValue(ref _ask, update);
                    Updated = true;
                }
            }
        }
        [JsonProperty]
        public double Macd
        {
            get => _macd;
            set
            {
                double update = value;
                if (_macd != update)
                {
                    SetValue(ref _macd, update);
                    Updated = true;
                }
            }
        }
        [JsonProperty]
        public double FastEma
        {
            get => _fastEma;
            set
            {
                double update = value;
                if (_fastEma != update)
                {
                    SetValue(ref _fastEma, update);
                    Updated = true;
                }
            }
        }
        [JsonProperty]
        public double SlowEma
        {
            get => _slowEma;
            set
            {
                double update = value;
                if (_slowEma != update)
                {
                    SetValue(ref _slowEma, update);
                    Updated = true;
                }
            }
        }
        [JsonProperty]
        public double Signal
        {
            get => _signal;
            set
            {
                double update = value;
                if (_signal != update)
                {
                    SetValue(ref _signal, update);
                    Updated = true;
                }
            }
        }
        [JsonProperty]
        public double Bar
        {
            get => _bar;
            set
            {
                double update = value;
                if (_bar != update)
                {
                    SetValue(ref _bar, update);
                    Updated = true;
                }
            }
        }
        [JsonProperty]
        public double BidEma
        {
            get => _bidEma;
            set
            {
                double update = Math.Round(value, 2);
                if (_bidEma != update)
                {
                    SetValue(ref _bidEma, update);
                    Updated = true;
                }
            }
        }
        [JsonProperty]
        public double MidEma
        {
            get => _midEma;
            set
            {
                double update = Math.Round(value, 2);
                if (_midEma != update)
                {
                    SetValue(ref _midEma, update);
                    Updated = true;
                }
            }
        }
        [JsonProperty]
        public double MidEma2
        {
            get => _midEma2;
            set
            {
                double update = Math.Round(value, 2);
                if (_midEma2 != update)
                {
                    SetValue(ref _midEma2, update);
                    Updated = true;
                }
            }
        }
        [JsonProperty]
        public double MidEma3
        {
            get => _midEma3;
            set
            {
                double update = Math.Round(value, 2);
                if (_midEma3 != update)
                {
                    SetValue(ref _midEma3, update);
                    Updated = true;
                }
            }
        }
        [JsonProperty]
        public double AskEma
        {
            get => _askEma;
            set
            {
                double update = Math.Round(value, 2);
                if (_askEma != update)
                {
                    SetValue(ref _askEma, update);
                    Updated = true;
                }
            }
        }
        [JsonProperty]
        public double HighestBid
        {
            get => _highestBid;
            set
            {
                double update = Math.Round(value, 2);
                if (_highestBid != update)
                {
                    SetValue(ref _highestBid, update);
                    Updated = true;
                }
            }
        }
        [JsonProperty]
        public double LowestAsk
        {
            get => _lowestAsk;
            set
            {
                double update = Math.Round(value, 2);
                if (_lowestAsk != update)
                {
                    SetValue(ref _lowestAsk, update);
                    Updated = true;
                }
            }
        }

        [JsonConstructor]
        public IndicatorDataPoints()
        {
            Timestamp = default;
            Bid = double.NaN;
            Mid = double.NaN;
            Ask = double.NaN;
            Macd = double.NaN;
            FastEma = double.NaN;
            SlowEma = double.NaN;
            Signal = double.NaN;
            Bar = double.NaN;
            BidEma = double.NaN;
            MidEma = double.NaN;
            MidEma2 = double.NaN;
            MidEma3 = double.NaN;
            AskEma = double.NaN;
            HighestBid = double.NaN;
            LowestAsk = double.NaN;
        }

        public IndicatorDataPoints(DateTime timeStamp)
        {
            Timestamp = timeStamp;
            Bid = double.NaN;
            Mid = double.NaN;
            Ask = double.NaN;
            Macd = double.NaN;
            FastEma = double.NaN;
            SlowEma = double.NaN;
            Signal = double.NaN;
            Bar = double.NaN;
            BidEma = double.NaN;
            MidEma = double.NaN;
            MidEma2 = double.NaN;
            MidEma3 = double.NaN;
            AskEma = double.NaN;
            HighestBid = double.NaN;
            LowestAsk = double.NaN;
        }

        public IndicatorDataPoints Clone(DateTime timeStamp)
        {
            return new IndicatorDataPoints(timeStamp)
            {
                Bid = Bid,
                Mid = Mid,
                Ask = Ask,
                Macd = Macd,
                FastEma = FastEma,
                SlowEma = SlowEma,
                Signal = Signal,
                Bar = Bar,
                BidEma = BidEma,
                MidEma = MidEma,
                MidEma2 = MidEma2,
                MidEma3 = MidEma3,
                AskEma = AskEma,
                HighestBid = HighestBid,
                LowestAsk = LowestAsk,
            };
        }
    }
}
