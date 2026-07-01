using DevExpress.Mvvm;
using Newtonsoft.Json;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class SizeupConfigModel : BindableBase
    {


        [JsonProperty]
        [Bindable(Default = true)]
        public partial bool Enabled { get; set; }
        [JsonProperty]
        [Bindable(Default = 0.10)]
        public partial double Edge { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double AdditionalEdgePerContract { get; set; }
        [JsonProperty]
        [Bindable(Default = 1)]
        public partial double MaxAbsDelta { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double MaxUnderWidth { get; set; }
        [JsonProperty]
        [Bindable(Default = 1)]
        public partial int Size { get; set; }
        [JsonProperty]
        [Bindable]
        public partial ResubmitSizeOption ResubmitSizeOption { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int RequiredLoop { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int ResubmitCount { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int MatchSignalQtyLimit { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool MinEdgeToEmaEnabled { get; set; }
        [JsonProperty]
        [Bindable(Default = .15)]
        public partial double MinEdgeToEma { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool MinIncrementEnabled { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double MinIncrement { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool MaxEmaBidPercentEnabled { get; set; }
        [JsonProperty]
        [Bindable(Default = .37)]
        public partial double MaxEmaBidPercent { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool MinEmaWidthPercentEdgeToTheoCheckEnabled { get; set; }
        [JsonProperty]
        [Bindable(Default = .20)]
        public partial double MinEmaWidthPercentEdgeToTheoCheckEdge { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool SizeScaleEnabled { get; set; }
        [JsonProperty]
        [Bindable(Default = 10)]
        public partial double SizeScaleUnderMin { get; set; }
        [JsonProperty]
        [Bindable(Default = 500)]
        public partial double SizeScaleUnderMax { get; set; }
        [JsonProperty]
        [Bindable(Default = 5)]
        public partial double SizeScaleFactor { get; set; }
        [JsonProperty]
        [Bindable(Default = 60)]
        public partial int SizeScaleMax { get; set; }
        [JsonProperty]
        [Bindable(Default = true)]
        public partial bool UnhedgeableEquityCheckEnabled { get; set; }
        [JsonProperty]
        [Bindable(Default = .50)]
        public partial double UnhedgeableEquitySizePercentage { get; set; }
        [JsonProperty]
        [Bindable(Default = 12)]
        public partial int UnhedgeableEquityMaxSize { get; set; }

        [JsonConstructor]
        public SizeupConfigModel() { }

        public SizeupConfigModel(SizeupConfigModel item)
        {
            Enabled = item.Enabled;
            Edge = item.Edge;
            AdditionalEdgePerContract = item.AdditionalEdgePerContract;
            MaxAbsDelta = item.MaxAbsDelta;
            MaxUnderWidth = item.MaxUnderWidth;
            Size = item.Size;
            ResubmitSizeOption = item.ResubmitSizeOption;
            RequiredLoop = item.RequiredLoop;
            ResubmitCount = item.ResubmitCount;
            MatchSignalQtyLimit = item.MatchSignalQtyLimit;
            MinEdgeToEmaEnabled = item.MinEdgeToEmaEnabled;
            MinEdgeToEma = item.MinEdgeToEma;
            MaxEmaBidPercentEnabled = item.MaxEmaBidPercentEnabled;
            MaxEmaBidPercent = item.MaxEmaBidPercent;
            MinEmaWidthPercentEdgeToTheoCheckEnabled = item.MinEmaWidthPercentEdgeToTheoCheckEnabled;
            MinEmaWidthPercentEdgeToTheoCheckEdge = item.MinEmaWidthPercentEdgeToTheoCheckEdge;
            SizeScaleEnabled = item.SizeScaleEnabled;
            SizeScaleUnderMin = item.SizeScaleUnderMin;
            SizeScaleUnderMax = item.SizeScaleUnderMax;
            SizeScaleFactor = item.SizeScaleFactor;
            SizeScaleMax = item.SizeScaleMax;
            UnhedgeableEquityCheckEnabled = item.UnhedgeableEquityCheckEnabled;
            UnhedgeableEquitySizePercentage = item.UnhedgeableEquitySizePercentage;
            UnhedgeableEquityMaxSize = item.UnhedgeableEquityMaxSize;
            MinIncrementEnabled = item.MinIncrementEnabled;
            MinIncrement = item.MinIncrement;
        }

        internal ZeroPlus.Models.Data.Update.SizeupConfigModel GetConfig()
        {
            return new ZeroPlus.Models.Data.Update.SizeupConfigModel()
            {
                Enabled = Enabled,
                Edge = Edge,
                AdditionalEdgePerContract = AdditionalEdgePerContract,
                MaxAbsDelta = MaxAbsDelta,
                MaxUnderWidth = MaxUnderWidth,
                Size = Size,
                ResubmitSizeOption = ResubmitSizeOption,
                RequiredLoop = RequiredLoop,
                ResubmitCount = ResubmitCount,
                MatchSignalQtyLimit = MatchSignalQtyLimit,
            };
        }
    }
}
