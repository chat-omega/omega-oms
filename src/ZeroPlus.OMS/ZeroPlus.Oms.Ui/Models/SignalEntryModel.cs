using DevExpress.Mvvm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Oms.Ui.Enums;
using static ZeroPlus.Oms.Ui.LowLatency.Ext.MsgRequests;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class SignalEntryModel : BindableBase
    {
        private int _minExpiry;
        private int _maxExpiry;

        [JsonIgnore]
        public static readonly JsonSerializerSettings NoJsonNulls = new JsonSerializerSettings
        { NullValueHandling = NullValueHandling.Ignore };

        [JsonIgnore]
        public IEnumerable<SignalDataType> SignalDataTypes { get; } = ((SignalDataType[])Enum.GetValues(typeof(SignalDataType))).ToList();

        [JsonProperty]
        [Bindable]
        public partial bool Enabled { get; set; }
        [JsonProperty]
        [Bindable]
        public partial string Tag { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int InitialOrderQty { get; set; }
        [JsonProperty]
        public int MinExpiry
        {
            get => _minExpiry;
            set
            {
                SetValue(ref _minExpiry, value);
                MinExpiryDate = DateTime.Today + TimeSpan.FromDays(MinExpiry);
            }
        }
        [JsonProperty]
        public int MaxExpiry
        {

            get => _maxExpiry;
            set
            {
                SetValue(ref _maxExpiry, value);
                MaxExpiryDate = DateTime.Today + TimeSpan.FromDays(MaxExpiry);
            }
        }
        [JsonProperty]
        [Bindable]
        public partial DateTime MinExpiryDate { get; set; }
        [JsonProperty]
        [Bindable]
        public partial DateTime MaxExpiryDate { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int MinDelta { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int MaxDelta { get; set; }
        [JsonProperty]
        [Bindable]
        public partial CallPut CallPut { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int PercentBid { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double EdgeToTheo { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool UseAdjTheo { get; set; }
        [JsonProperty]
        [Bindable]
        public partial LeanModel LeanModel { get; set; }

        public SignalEntryModel(string tag)
        {
            Tag = tag;
            LeanModel = new LeanModel();
        }

        public jsonRequestSignalTradeWatcherInstanceParams JsonRequestSignalTradeWatcherInstanceParams(bool partialUpload, bool excludeOnLoss, bool excludeOnScratch)
        {
            jsonRequestSignalTradeWatcherInstanceItemParams item = null;

            if (!partialUpload || Enabled)
            {
                string callPuts = CallPut switch
                {
                    CallPut.All => "CP",
                    CallPut.Calls => "C",
                    CallPut.Puts => "P",
                    _ => "CP"
                };

                item = new jsonRequestSignalTradeWatcherInstanceItemParams()
                {
                    OrderQty = InitialOrderQty,

                    PctBid = PercentBid,
                    EdgeToTheo = $"{EdgeToTheo:N2}",
                    UseAdjTheo = UseAdjTheo ? 1 : 0,

                    ExcludeOnLoss = excludeOnLoss ? 1 : 0,
                    ExcludeOnScratch = excludeOnScratch ? 1 : 0,

                    Lean = JsonRequestLeanBaseParams,

                    OptionFilter = new jsonRequestOptionFilterParams()
                    {
                        MaxDelta = MaxDelta,
                        MinDelta = MinDelta,
                        CallPuts = callPuts,
                        MinExpiry = MinExpiry,
                        MaxExpiry = MaxExpiry,
                        MinExpiryDate = int.Parse($"{DateTime.Today.AddDays(MinExpiry):yyyyMMdd}"),
                        MaxExpiryDate = int.Parse($"{DateTime.Today.AddDays(MaxExpiry):yyyyMMdd}"),
                    }
                };
            }

            jsonRequestSignalTradeWatcherInstanceParams param = new jsonRequestSignalTradeWatcherInstanceParams
            {
                Enabled = Enabled ? 1 : 0,
                InstanceName = Tag,
                Item = item,
                HashCode = 0,
            };

            string jsonString = $"{JsonConvert.SerializeObject(param, Formatting.None, NoJsonNulls)}";
            ulong hashCode = (ulong)jsonString.GetHashCode();
            param.HashCode = hashCode;

            return param;
        }

        public jsonRequestLeanBaseParams JsonRequestLeanBaseParams => new()
        {
            MinSpreadPrice = $"{LeanModel.MinMarketWidth:N2}",
            MaxSpreadPrice = $"{LeanModel.MaxMarketWidth:N2}",
            MaxSideSpreadPrice = $"{LeanModel.MaxSideSpread:N2}",
            MinNbboPrice = $"{LeanModel.MinNbboPrice:N2}",
            MaxNbboPrice = $"{LeanModel.MaxNbboPrice:N2}",
            MinL1LeanQty = LeanModel.MarketMinL1LeanQty,
            MinL1LeanCnt = LeanModel.MarketMinL1LeanCount,
            MinL2LeanQty = LeanModel.MarketMinL2LeanQty,
            MinL2LeanCnt = LeanModel.MarketMinL2LeanCount,
            MinSideQty = LeanModel.MinSideQty,
            MinDigQty = LeanModel.DigQty,
            MinDigCnt = LeanModel.DigCount,
            UseDig = LeanModel.SignalDataType == SignalDataType.Market ? 0 : 1,
        };
    }
}
