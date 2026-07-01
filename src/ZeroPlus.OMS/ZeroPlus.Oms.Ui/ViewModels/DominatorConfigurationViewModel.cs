using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using DevExpress.XtraRichEdit.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows.Input;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Models.Data.Enums;
using Newtonsoft.Json;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    [JsonObject]
    public partial class DominatorConfigurationViewModel : CustomizableTableViewModelBase
    {
        [JsonIgnore]
        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        private static readonly IEnumerable<DomSecurityType> domSecurityTypes = ((DomSecurityType[])Enum.GetValues(typeof(DomSecurityType))).ToArray();

        public DominatorConfigurationViewModel()
        {
            Title = Guid.NewGuid().ToString();
        }
        [JsonIgnore]
        public IEnumerable<DomSecurityType> SecurityTypes => domSecurityTypes;
        
        public ValueTask<double> DomStyleEdge(IComplexOrder order)
        {
            if (order?.Legs == null || !order.Legs.Any()) return ValueTask.FromResult(0.0);

            double edge = minEdgePerContract;

            edge += Math.Abs(order.TotalDelta) * edgeExpansionPerDelta;

            var underMultiplier = UnderMultiplierTable
                .FirstOrDefault(x => x.Under == order.UnderlyingSymbol)
                ?.Multiplier ?? 1.0;
            
            edge *= underMultiplier;

            var spreadMultiplier = SpreadTypeMultiplierTable
                .FirstOrDefault(x => x.SpreadType == order.BaseStrategy)
                ?.Multiplier ?? 1.0;

            edge *= spreadMultiplier;

            return ValueTask.FromResult(Math.Max(edge, minEdgePerContract));
        }

        /// <summary>
        /// Calculates the edge requirement for a complex order based on multiple factors
        /// Final edge is guaranteed to be at least minEdgePerContract.
        /// </summary>
        public double DomStyleEdge(OrderTicket order)
        {
            double edge = minEdgePerContract;

            // Delta expansion using spread's total delta
            edge += Math.Abs(order.TotalDelta) * EdgeExpansionPerDelta;

            /* DTE (Days to Expiration) Multiplier
            - References DteEdgeMultiplierTable for tiered multipliers
            - Selects largest applicable tier where DTE <= order.DaysToExpiration
            - Applies selected multiplier to current edge value
            - Default: 1.0 if no applicable tier found */
            double dteMultiplier = DteEdgeMultiplierTable
                .Where(x => x.Dte <= order.DaysToExpiration)
                .OrderByDescending(x => x.Dte)
                .FirstOrDefault()?.Multiplier ?? 1.0;

            edge *= dteMultiplier;

            /* Underlying-Specific Multiplier
            - References UnderMultiplierTable for symbol-specific multipliers
            - Matches on exact underlying symbol
            - Applies matched multiplier to current edge value
            - Default: 1.0 if no match found */
            var underMultiplier = UnderMultiplierTable
                .FirstOrDefault(x => x.Under == order.UnderlyingSymbol)
                ?.Multiplier ?? 1.0;

            edge *= underMultiplier;

            /* Strategy/spread type multiplier
            - References SpreadTypeMultiplierTable for strategy-specific multipliers
            - Matches on order's BaseStrategy enum
            - Applies matched multiplier to current edge value
            - Default: 1.0 if no match found */
            var spreadMultiplier = SpreadTypeMultiplierTable
                .FirstOrDefault(x => x.SpreadType == order.BaseStrategy)
                ?.Multiplier ?? 1.0;

            edge *= spreadMultiplier;

            /* Expiration Gap Multiplier (Multi-leg orders only)
            - Calculates gap between earliest and latest expiration dates
            - References ExpGapMultiplierTable for gap-based multipliers
            - Selects largest applicable tier where ExpGap <= calculated gap
            - Applies selected multiplier to current edge value
            - Default: 1.0 if no applicable tier found
            - Skipped for single-leg orders */
            bool isSingleLeg = order.Legs.Count == 1;
            if (!isSingleLeg)
            {
                var dates = order.Legs
                    .Select(leg => leg.ExpirationInfo.Expiration.Subtract(DateTime.Today).Days)
                    .OrderBy(dte => dte)
                    .ToList();

                if (dates.Count > 1)
                {
                    int expGap = dates.Last() - dates.First();
                    var gapMultiplier = ExpGapMultiplierTable
                        .Where(x => x.ExpGap <= expGap)
                        .OrderByDescending(x => x.ExpGap)
                        .FirstOrDefault()?.Multiplier ?? 1.0;

                    edge *= gapMultiplier;
                }
            }
            

            /*Leg Delta Multipliers
            - References LegDeltaGapMultiplierTable for delta threshold tiers
            - For each leg:
            - Evaluates absolute delta against all tiers
            - Applies multiplier for EVERY threshold that leg's |delta| exceeds
            - Multipliers stack multiplicatively
            - No default multiplier (skips if no thresholds exceeded)*/
            if (isSingleLeg)
            {
                foreach (var tier in LegDeltaGapMultiplierTable)
                {
                    if (Math.Abs(order.TotalDelta) >= tier.LegDelta)
                        edge *= tier.Multiplier;
                }
            }
            else 
            {
                foreach (var leg in order.Legs)
                {
                    foreach (var tier in LegDeltaGapMultiplierTable)
                    {
                        if (Math.Abs(leg.Delta) >= tier.LegDelta) edge *= tier.Multiplier;
                    }
                }
            }
            
            return Math.Max(edge, minEdgePerContract);
        }
        private static readonly IEnumerable<BaseStrategy> spreadTypes = ((BaseStrategy[])Enum.GetValues(typeof(BaseStrategy))).ToArray();
        [JsonIgnore]
        public static IEnumerable<BaseStrategy> SpreadTypes => spreadTypes;

        [Bindable]
        public partial DomSecurityType SecurityType { get; set; }

        [Bindable]
        public partial string Title { get; set; }

        [Bindable]
        public partial DateTime LastUpdateTime { get; set; }

        private bool? allowIfDayTraded;

        public bool? AllowIfDayTraded { get => allowIfDayTraded; set => SetValue(ref allowIfDayTraded, value); }

        private bool? useBestQuoteForBid;

        public bool? UseBestQuoteForBid { get => useBestQuoteForBid; set => SetValue(ref useBestQuoteForBid, value); }

        private int maxDte = int.MaxValue;

        public int MaxDte { get => maxDte; set => SetValue(ref maxDte, value); }

        private int minDte = 0;

        public int MinDte { get => minDte; set => SetValue(ref minDte, value); }

        private double maxSpreadDelta;

        public double MaxSpreadDelta { get => maxSpreadDelta; set => SetValue(ref maxSpreadDelta, value); }

        private double minEdgePerContract;

        public double MinEdgePerContract { get => minEdgePerContract; set => SetValue(ref minEdgePerContract, value); }

        private double edgeExpansionPerDelta;

        public double EdgeExpansionPerDelta { get => edgeExpansionPerDelta; set => SetValue(ref edgeExpansionPerDelta, value); }

        private bool? restartWhenDone;

        public bool? RestartWhenDone { get => restartWhenDone; set => SetValue(ref restartWhenDone, value); }

        private int initialFishQty;

        public int InitialFishQty { get => initialFishQty; set => SetValue(ref initialFishQty, value); }

        private ObservableCollection<DTEEdgeExpansion> dteEdgeMultiplierTable = new();

        public ObservableCollection<DTEEdgeExpansion> DteEdgeMultiplierTable { get => dteEdgeMultiplierTable; set => SetValue(ref dteEdgeMultiplierTable, value); }
        public class DTEEdgeExpansion
        {
            public int Dte { get; set; } = 0;
            public double Multiplier { get; set; } = 1;
        }
   
        private ObservableCollection<UnderlyingEdgeExpansion> underMultiplierTable = new();

        public ObservableCollection<UnderlyingEdgeExpansion> UnderMultiplierTable { get => underMultiplierTable; set => SetValue(ref underMultiplierTable, value); }        
        
        public class UnderlyingEdgeExpansion
        {
            private static readonly ISecurityBook securityBook;
            static UnderlyingEdgeExpansion()
            {
                securityBook = new ZeroPlus.Models.Data.Securities.SecurityBook();
            }

            private ZeroPlus.Models.Data.Securities.Security security;
            public string Under
            {
                get => security?.ToString();
                set => security = securityBook.GetSecurity(value);
            }
            public double Multiplier { get; set; } = 1;
        }

        private ObservableCollection<SpreadTypeExpansion> spreadTypeMultiplierTable = new();

        public ObservableCollection<SpreadTypeExpansion> SpreadTypeMultiplierTable { get => spreadTypeMultiplierTable; set => SetValue(ref spreadTypeMultiplierTable, value); }
        public class SpreadTypeExpansion
        {
            public BaseStrategy SpreadType { get; set; } = BaseStrategy.INVALID;
            public double Multiplier { get; set; } = 1;
        }

        private ObservableCollection<ExpGapExpansion> expGapMultiplierTable = new();

        public ObservableCollection<ExpGapExpansion> ExpGapMultiplierTable { get => expGapMultiplierTable; set => SetValue(ref expGapMultiplierTable, value); }

        public class ExpGapExpansion
        {
            public int ExpGap { get; set; } = 0;
            public double Multiplier { get; set; } = 1;
        }

        private ObservableCollection<LegDeltaGap> legDeltaGapMultiplierTable = new();

        public ObservableCollection<LegDeltaGap> LegDeltaGapMultiplierTable { get => legDeltaGapMultiplierTable; set => SetValue(ref legDeltaGapMultiplierTable, value); }
        public class LegDeltaGap
        {
            public double LegDelta { get; set; } = 0.0;
            public double Multiplier { get; set; } = 1;
        }
        public void AddLegDeltaGap() => LegDeltaGapMultiplierTable.Add(new());
        public void AddExpGapExpansion() => ExpGapMultiplierTable.Add(new());
        public void AddSpreadTypeExpansion() => SpreadTypeMultiplierTable.Add(new());
        public void AddUnderlyingEdgeExpansion() => UnderMultiplierTable.Add(new());
        public void AddDTEEdgeExpansion() => DteEdgeMultiplierTable.Add(new());
    }
}
