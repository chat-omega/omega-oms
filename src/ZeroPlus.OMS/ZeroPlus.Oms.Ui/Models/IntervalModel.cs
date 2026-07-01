using DevExpress.Mvvm;
using Newtonsoft.Json;

namespace ZeroPlus.Oms.Ui.Models
{
    public class IntervalModel : BindableBase
    {
        private bool _active = true;
        private double _attemptedEdge = .10;
        private double _interval = 110;
        private int _resubmitCount;
        private double _minDelta;
        private int _minSize;
        private double _maxDelta = 1;
        private string _route;
        private bool _disableRounding;

        [JsonProperty]
        public bool Active { get => _active; set => SetValue(ref _active, value); }
        [JsonProperty]
        public double MinDelta { get => _minDelta; set => SetValue(ref _minDelta, value); }
        [JsonProperty]
        public double MaxDelta { get => _maxDelta; set => SetValue(ref _maxDelta, value); }
        [JsonProperty]
        public double AttemptedEdge { get => _attemptedEdge; set => SetValue(ref _attemptedEdge, value); }
        [JsonProperty]
        public double Interval { get => _interval; set => SetValue(ref _interval, value); }
        [JsonProperty]
        public int MinSize { get => _minSize; set => SetValue(ref _minSize, value); }
        [JsonProperty]
        public int ResubmitCount { get => _resubmitCount; set => SetValue(ref _resubmitCount, value); }
        [JsonProperty]
        public string Route { get => _route; set => SetValue(ref _route, value); }
        [JsonProperty]
        public bool DisableRounding { get => _disableRounding; set => SetValue(ref _disableRounding, value); }


        internal IntervalModel Clone()
        {
            return new IntervalModel
            {
                Active = Active,
                AttemptedEdge = AttemptedEdge,
                MinDelta = MinDelta,
                MaxDelta = MaxDelta,
                Interval = Interval,
                ResubmitCount = ResubmitCount,
                Route = Route,
                DisableRounding = DisableRounding,
            };
        }

        internal ZeroPlus.Models.Data.Update.IntervalModel GetConfig()
        {
            return new ZeroPlus.Models.Data.Update.IntervalModel
            {
                Active = Active,
                AttemptedEdge = AttemptedEdge,
                MinDelta = MinDelta,
                MaxDelta = MaxDelta,
                Interval = Interval,
                ResubmitCount = ResubmitCount,
                Route = Route,
                DisableRounding = DisableRounding,
            };
        }
    }
}
