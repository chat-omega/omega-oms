using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Extensions;

namespace ZeroPlus.Models.Data.EdgeScanner
{
    public class EdgeScanFeedTradeFilterRowModel
    {
        private readonly byte MAX_EDGE = 15;

        private double _maxEdge;
        private double _maxDeltaAdjEdge;
        private List<object> _selectedTradeConditionCodes;

        [Newtonsoft.Json.JsonIgnore]
        internal HashSet<string> BlockedUnderlyingsMap = new();
        [Newtonsoft.Json.JsonIgnore]
        internal HashSet<string> AllowUnderlyingsMap = new();
        [Newtonsoft.Json.JsonIgnore]
        internal HashSet<string> BlockedExchangeMap = new();
        [Newtonsoft.Json.JsonIgnore]
        internal HashSet<string> AllowExchangeMap = new();
        [Newtonsoft.Json.JsonIgnore]
        internal HashSet<string> AllowStrategies = new();
        [Newtonsoft.Json.JsonIgnore]
        public Dictionary<string, StrategyModel>? StrategyToModelMap { get; private set; } = new Dictionary<string, StrategyModel>();
        [Newtonsoft.Json.JsonIgnore]
        private HashSet<BaseStrategy> BaseStrategies { get; }
        [Newtonsoft.Json.JsonIgnore]
        public string StrategiesSummary { get; set; } = "None";
        [Newtonsoft.Json.JsonIgnore]
        public List<LegTypes> LegTypes { get; } = ((LegTypes[])Enum.GetValues(typeof(LegTypes))).ToList();
        [Newtonsoft.Json.JsonIgnore]
        public List<EdgeScannerType> EdgeFeedScanners { get; } = ((EdgeScannerType[])Enum.GetValues(typeof(EdgeScannerType))).ToList();
        [Newtonsoft.Json.JsonIgnore]
        public string BlockedExpirationsSummary { get; set; } = "None";
        [Newtonsoft.Json.JsonIgnore]
        public DateTime BlockedExpirationInput { get; set; }
        [Newtonsoft.Json.JsonIgnore]
        public HashSet<DateTime> BlockedExpirationsSet { get; set; } = new HashSet<DateTime>();
        [Newtonsoft.Json.JsonIgnore]
        public List<object> SelectedEdgeFeedScanners { get; set; } = ((EdgeScannerType[])Enum.GetValues(typeof(EdgeScannerType))).Select(x => (object)x).ToList();
        [Newtonsoft.Json.JsonProperty]
        public string Header { get; set; } = "";
        [Newtonsoft.Json.JsonProperty]
        public HashSet<StrategyModel> Strategies { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public List<DateTime> BlockedExpirations { get; set; } = new List<DateTime>();
        [Newtonsoft.Json.JsonProperty]
        public List<EdgeScannerType> SelectedEdgeFeedScannersExport { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public LegTypes SelectedLegTypes { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public string BlockedUnderlyings { get; set; } = "";
        [Newtonsoft.Json.JsonProperty]
        public string AllowUnderlyings { get; set; } = "";
        [Newtonsoft.Json.JsonProperty]
        public string BlockedExchange { get; set; } = "";
        [Newtonsoft.Json.JsonProperty]
        public string AllowExchange { get; set; } = "";
        [Newtonsoft.Json.JsonProperty]
        public bool AllowUncertain { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public bool AllowQtyMismatch { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public int MinDte { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public int MaxDte { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public int MinBidAskSize { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public int MinQty { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public int MaxQty { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public bool MinEdgeToTheoEnabled { get; set; } = true;
        [Newtonsoft.Json.JsonProperty]
        public double MinEdgeToTheo { get; set; } = .20;
        [Newtonsoft.Json.JsonProperty]
        public bool MinBidPercentEnabled { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double MinBidPercent { get; set; } = 0.10;
        [Newtonsoft.Json.JsonProperty(PropertyName = "MinPercentBidEnabled")]
        public bool MaxBidPercentEnabled { get; set; }
        [Newtonsoft.Json.JsonProperty(PropertyName = "MinPercentBid")]
        public double MaxBidPercent { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public bool MinBidEnabled { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double MinBid { get; set; } = 0.05;
        [Newtonsoft.Json.JsonProperty]
        public bool MinNotionalEnabled { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double MinNotional { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public int LoopInterval { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public int LoopTimeSpan { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public int MinLoopCount { get; set; } = 5;
        [Newtonsoft.Json.JsonProperty]
        public double MinPrice { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double MaxPrice { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double MinMarketWidth { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double MaxMarketWidth { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public bool EdgeRangeEnabled { get; set; } = true;
        [Newtonsoft.Json.JsonProperty]
        public double MinEdge { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public bool DeltaAdjEdgeRangeEnabled { get; set; } = true;
        [Newtonsoft.Json.JsonProperty]
        public double MinDeltaAdjEdge { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double MinUnderlying { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double MaxUnderlying { get; set; } = 9999;
        [Newtonsoft.Json.JsonProperty]
        public double UnderlyingWidth { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double MaxTimeDelay { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double MinDelta { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double MaxDelta { get; set; } = 1;
        [Newtonsoft.Json.JsonProperty]
        public double LowLegMinDelta { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double LowLegMaxDelta { get; set; } = 1;
        [Newtonsoft.Json.JsonProperty]
        public double HighLegMinDelta { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double HighLegMaxDelta { get; set; } = 1;
        [Newtonsoft.Json.JsonProperty]
        public DateTime MinNearExpirationFilter { get; set; } = DateTime.Today;
        [Newtonsoft.Json.JsonProperty]
        public DateTime MaxNearExpirationFilter { get; set; } = DateTime.Today;
        [Newtonsoft.Json.JsonProperty]
        public DateTime MinFarExpirationFilter { get; set; } = DateTime.Today;
        [Newtonsoft.Json.JsonProperty]
        public DateTime MaxFarExpirationFilter { get; set; } = DateTime.Today;
        [Newtonsoft.Json.JsonIgnore]
        public List<object> TradeConditionCodes { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public string Description { get; set; } = "";
        [Newtonsoft.Json.JsonProperty]
        public bool MaxChangeInUnderlyingEnabled { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double MaxChangeInUnderlying { get; set; } = .5;
        [Newtonsoft.Json.JsonProperty]
        public bool MinLegDeltaEnabled { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double MinLegDelta { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public bool MaxSpreadWeightedVegaEnabled { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double MaxSpreadWeightedVega { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double MaxEdge
        {
            get => _maxEdge;
            set => _maxEdge = Math.Max(value, MAX_EDGE);
        }
        [Newtonsoft.Json.JsonProperty]
        public double MaxDeltaAdjEdge
        {
            get => _maxDeltaAdjEdge;
            set => _maxDeltaAdjEdge = Math.Max(value, MAX_EDGE);
        }
        [Newtonsoft.Json.JsonProperty]
        public List<object> SelectedTradeConditionCodes
        {
            get => _selectedTradeConditionCodes;
            set => _selectedTradeConditionCodes = SanitizeConditionCodes(value);
        }
        [Newtonsoft.Json.JsonProperty]
        public double PriceChainDeviationMaxLookBackTime { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double PriceChainDeviationMaxChangeInUnder { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double PriceChainDeviationMinDeviation { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double PriceChainDeviationMinMarketWidthAtTrade { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public double PriceChainDeviationMaxMarketWidthAtViolation { get; set; }

        public void UpdateMap()
        {
            try
            {
                string[] blockList = BlockedUnderlyings.Split(",");
                IEnumerable<string> indexBlock = blockList.Where(x => !string.IsNullOrWhiteSpace(x)).Where(x => x.IsIndex()).Select(x => "$" + x.Trim());
                IEnumerable<string> stockBlock = blockList.Where(x => !string.IsNullOrWhiteSpace(x)).Where(x => !x.IsIndex()).Select(x => x.Trim());

                string[] allowList = AllowUnderlyings.Split(",");
                IEnumerable<string> indexAllow = allowList.Where(x => !string.IsNullOrWhiteSpace(x)).Where(x => x.IsIndex()).Select(x => "$" + x.Trim());
                IEnumerable<string> stockAllow = allowList.Where(x => !string.IsNullOrWhiteSpace(x)).Where(x => !x.IsIndex()).Select(x => x.Trim());

                BlockedUnderlyingsMap = indexBlock.Union(stockBlock).ToHashSet();
                AllowUnderlyingsMap = indexAllow.Union(stockAllow).ToHashSet();

                BlockedExchangeMap = BlockedExchange.Split(",").Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToHashSet();
                AllowExchangeMap = AllowExchange.Split(",").Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToHashSet();

                foreach (var scanner in SelectedEdgeFeedScannersExport.ToList())
                {
                    int index = (int)scanner;
                    if (index < (int)EdgeScannerType.LoopFinder)
                    {
                        SelectedEdgeFeedScannersExport.Remove((EdgeScannerType)index);
                        index += (int)EdgeScannerType.LoopFinder;
                        SelectedEdgeFeedScannersExport.Add((EdgeScannerType)index);
                    }
                }

                BlockedExpirations ??= new List<DateTime>();
                BlockedExpirations = BlockedExpirations.DistinctBy(x => x.Date).ToList();
                BlockedExpirationsSet = BlockedExpirations.Select(x => x.Date).ToHashSet();
                BlockedExpirationsSummary = BlockedExpirationsSet.Count == 0 ? "None" : BlockedExpirationsSet.Distinct().Count() + " Exp";
                SelectedTradeConditionCodes = SanitizeConditionCodes(SelectedTradeConditionCodes);
                if (Strategies == null || Strategies.Count == 0)
                {
                    InitializeAllStrategies();
                    StrategiesSummary = "All";
                }
                else if (Strategies.Where(x => x.IsChecked).GroupBy(x => x.Strategy).Select(x => x.Key).Count() == BaseStrategies.Count)
                {
                    StrategiesSummary = "All";
                }
                else
                {
                    StrategiesSummary = Strategies.Count(x => x.IsChecked) + " Selected";
                }
                var grouped = Strategies?.GroupBy(x => x.Strategy);
                StrategyToModelMap = new Dictionary<string, StrategyModel>();
                if (grouped != null)
                {
                    foreach (var group in grouped)
                    {
                        StrategyModel? strategyModel = group.FirstOrDefault();
                        if (strategyModel != null)
                        {
                            StrategyToModelMap[Utils.OptionStrategy2.ConvertToString(group.Key)] = strategyModel;
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        private static List<object> SanitizeConditionCodes(List<object> selectedTradeConditionCodes)
        {
            List<object> chars = new();
            try
            {
                if (selectedTradeConditionCodes != null)
                {
                    foreach (var item in selectedTradeConditionCodes)
                    {
                        var code = Convert.ToChar(item);
                        if (code != '*')
                        {
                            chars.Add(code);
                        }
                    }
                }

                return chars;
            }
            catch (Exception)
            {
                return chars;
            }
        }

        public void SaveCopy()
        {
            try
            {
                SelectedEdgeFeedScannersExport = SelectedEdgeFeedScanners.Select(x => (EdgeScannerType)x).ToList();
            }
            catch (Exception) { }
        }

        public void LoadCopy()
        {
            try
            {
                if (SelectedEdgeFeedScannersExport != null)
                {
                    SelectedEdgeFeedScanners = SelectedEdgeFeedScannersExport.Select(x => (object)x).ToList();
                }
                else
                {
                    SelectedEdgeFeedScanners = new List<object>();
                }
            }
            catch (Exception) { }
        }

        public bool BlockedUnderlyingsMapContains(string underSymbol)
        {
            try
            {
                if (BlockedUnderlyingsMap.Count == 0)
                {
                    return false;
                }
                return BlockedUnderlyingsMap.Contains(underSymbol);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool AllowUnderlyingsMapContains(string underSymbol)
        {
            try
            {
                if (AllowUnderlyingsMap.Count == 0)
                {
                    return true;
                }

                return AllowUnderlyingsMap.Contains(underSymbol);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool BlockedExchangeMapContains(string underSymbol)
        {
            try
            {
                if (BlockedExchangeMap.Count == 0)
                {
                    return false;
                }
                return BlockedExchangeMap.Contains(underSymbol);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool AllowExchangeMapContains(string underSymbol)
        {
            try
            {
                if (AllowExchangeMap.Count == 0)
                {
                    return true;
                }

                return AllowExchangeMap.Contains(underSymbol);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public EdgeScanFeedTradeFilterRowModel? Clone()
        {
            try
            {
                string export = Newtonsoft.Json.JsonConvert.SerializeObject(this);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<EdgeScanFeedTradeFilterRowModel>(export);
            }
            catch (Exception)
            {
                return null;
            }
        }

        [Newtonsoft.Json.JsonConstructor]
        public EdgeScanFeedTradeFilterRowModel()
        {
            _selectedTradeConditionCodes = new List<object>();
            Strategies = new HashSet<StrategyModel>();
            SelectedEdgeFeedScannersExport = new List<EdgeScannerType>();
            TradeConditionCodes = new List<object>
            {
                new TradeConditionCodeModel('*', " -- Single Leg -- "),
                new TradeConditionCodeModel('a', "a\tSLAN\tSingle Leg Auction Non ISO"),
                new TradeConditionCodeModel('b', "b\tSLAI\tSingle Leg Auction ISO"),
                new TradeConditionCodeModel('c', "c\tSLCN\tSingle Leg Cross Non ISO"),
                new TradeConditionCodeModel('d', "d\tSCLI\tSingle Leg Cross ISO"),
                new TradeConditionCodeModel('I', "I\tAUTO\tAuto-electronic Exec"),
                new TradeConditionCodeModel('S', "S\tISOI\tSingle Leg Cross ISO"),

                new TradeConditionCodeModel('*', " -- Multi Leg -- "),
                new TradeConditionCodeModel('f', "f\tMLET\tMulti Leg auto-electronic trade"),
                new TradeConditionCodeModel('g', "g\tMLAT\tMulti Leg Auction"),
                new TradeConditionCodeModel('h', "h\tMLCT\tMulti Leg Cross"),
                new TradeConditionCodeModel('k', "k\tTLAT\tStock Options Auction"),
                new TradeConditionCodeModel('n', "n\tTLET\tStock Options auto-electronic trade"),
                new TradeConditionCodeModel('o', "o\tTLCT\tStock Options Cross"),
                new TradeConditionCodeModel('p', "p\tTLFT\tStock Options floor trade"),

                new TradeConditionCodeModel('*', " -- Multi Leg Against Single Leg(s) -- "),
                new TradeConditionCodeModel('j', "j\tMESL\tMulti Leg auto-electronic trade against single leg(s)"),
                new TradeConditionCodeModel('l', "l\tMASL\tMulti Leg Auction against single leg(s)"),
                new TradeConditionCodeModel('q', "q\tTESL\tStock Options auto-electronic trade against single leg(s)"),
                new TradeConditionCodeModel('r', "r\tTASL\tStock Options Auction against single leg(s)"),
                new TradeConditionCodeModel('u', "u\tMCTP\tMultilateral Compression Trade of Proprietary Products"),

                new TradeConditionCodeModel('*', " -- Others -- "),
                new TradeConditionCodeModel('A', "A\tCANC\tNow busted"),
                new TradeConditionCodeModel('B', "B\tOSEQ\tOut of sequence"),
                new TradeConditionCodeModel('C', "C\tCNCL\tTransaction is the last reported but now canceled"),
                new TradeConditionCodeModel('D', "D\tLATE\tTransaction is being reported late"),
                new TradeConditionCodeModel('E', "E\tCNCO\tOpen report, now busted"),
                new TradeConditionCodeModel('F', "F\tOPEN\tOpen but late report"),
                new TradeConditionCodeModel('G', "G\tCNOL\tOnly report but now busted"),
                new TradeConditionCodeModel('H', "H\tOPNL\tOpening report, late"),
                new TradeConditionCodeModel('J', "J\tREOP\tReopening report"),
                new TradeConditionCodeModel('v', "v\tEXHT\tExtended Hours Trade"),
            };

            BaseStrategies = Enum.GetValues(typeof(BaseStrategy)).Cast<BaseStrategy>().ToHashSet();
        }

        public void InitializeAllStrategies()
        {
            Strategies = BaseStrategies.Select(x => new StrategyModel(x)).ToHashSet();
        }
    }
}