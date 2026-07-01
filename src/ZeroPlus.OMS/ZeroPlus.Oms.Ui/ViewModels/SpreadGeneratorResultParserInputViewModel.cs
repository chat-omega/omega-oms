using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Generators.SpreadGenerators;
using ZeroPlus.Oms.Subscription;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public enum LegType
    {
        Lower,
        Higher,
    }

    public partial class SpreadGeneratorResultParserInputViewModel : ViewModelBase
    {


        public LegType[] LegTypes { get; } = (LegType[])Enum.GetValues(typeof(LegType));
        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        public ISpreadsGenerator SpreadsGenerator { get; internal set; }

        [Bindable]
        public partial SpreadGeneratorResults SpreadGeneratorResults { get; set; }

        [Bindable]
        public partial bool ShowProgressBar { get; set; }

        [Bindable]
        public partial double HighestLegDeltaMinValue { get; set; }

        [Bindable]
        public partial double HighestLegDeltaMaxValue { get; set; }

        [Bindable]
        public partial double RetainedPercentageRangeMin { get; set; }

        [Bindable]
        public partial double RetainedPercentageRangeMax { get; set; }

        [Bindable]
        public partial LegType LegType { get; set; }

        public SpreadGeneratorResultParserInputViewModel()
        {
            HighestLegDeltaMinValue = 0;
            HighestLegDeltaMaxValue = 1;
            RetainedPercentageRangeMin = 1;
            RetainedPercentageRangeMax = 1;
        }

        [Command]
        public async Task ApplyCommand()
        {
            SpreadsGenerator.ShowProgressBar = ShowProgressBar = true;
            SpreadsGenerator.ProgressStatus = "Parsing Spreads By Min Delta Range Filter";
            Stopwatch stopwatch = Stopwatch.StartNew();
            int eliminationCounter = await ApplyDeltaFilter();
            SpreadGeneratorResults.UpdateExpirations();
            stopwatch.Stop();
            int totalCount = SpreadsGenerator.LatestSpreadGeneratorResults.Sum(x => x.Spreads.Count);
            SpreadsGenerator.ShowProgressBar = ShowProgressBar = false;
            SpreadsGenerator.ProgressStatus = $"Done! {totalCount:N0} spreads parsed, {eliminationCounter:N0} spreads eliminated, in {stopwatch.ElapsedMilliseconds}ms.";
            CurrentWindowService?.Close();
        }

        [Command]
        public void CancelCommand()
        {
            CurrentWindowService?.Close();
        }

        private async Task<int> ApplyDeltaFilter()
        {
            return await Task.Run(async () =>
            {
                int eliminationCounter = 0;
                DataStore deltaStore = LoadSpreadsToDeltaStore();
                await IdentifyHighestLegDelta(deltaStore);
                double maxLimit = Math.Max(HighestLegDeltaMinValue, HighestLegDeltaMaxValue);
                IEnumerable<IGrouping<double, Spread>> targetedSpreadsGroupedByDelta = null;
                switch (LegType)
                {
                    case LegType.Lower:
                        IEnumerable<Spread> targetedSpreads = SpreadGeneratorResults.Spreads.Where(x => !double.IsNaN(x.LowestLegDelta) && x.LowestLegDelta <= maxLimit);
                        targetedSpreadsGroupedByDelta = targetedSpreads.GroupBy(x => x.LowestLegDelta);
                        break;
                    case LegType.Higher:
                        targetedSpreads = SpreadGeneratorResults.Spreads.Where(x => !double.IsNaN(x.HighestLegDelta) && x.HighestLegDelta <= maxLimit);
                        targetedSpreadsGroupedByDelta = targetedSpreads.GroupBy(x => x.HighestLegDelta);
                        break;
                }

                if (targetedSpreadsGroupedByDelta == null)
                {
                    return 0;
                }

                foreach (IGrouping<double, Spread> group in targetedSpreadsGroupedByDelta)
                {
                    double delta = Math.Abs(group.Key);
                    if (delta >= HighestLegDeltaMinValue)
                    {
                        int count = group.Count();
                        double co = (delta - HighestLegDeltaMinValue) / Math.Abs(HighestLegDeltaMinValue - HighestLegDeltaMaxValue);
                        double percent = RetainedPercentageRangeMin + (Math.Abs(RetainedPercentageRangeMin - RetainedPercentageRangeMax) * co);
                        double target = Math.Min(count * percent, count);
                        Spread[] groupArray = group.ToArray();
                        for (int i = 0; i < count - target; i++)
                        {
                            Spread item = groupArray[i];
                            SpreadGeneratorResults.Spreads.Remove(item);
                            eliminationCounter++;
                        }
                    }
                    else
                    {
                        SpreadGeneratorResults.Spreads.RemoveWhere(x => group.Contains(x));
                        eliminationCounter += group.Count();
                    }
                }
                return eliminationCounter;
            });
        }

        private DataStore LoadSpreadsToDeltaStore()
        {
            DataStore deltaStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
            var leg1Options = SpreadGeneratorResults.Spreads.Where(x => x.Legs.Count >= 1).Select(x => x.Legs[0].Option);
            var leg2Options = SpreadGeneratorResults.Spreads.Where(x => x.Legs.Count >= 2).Select(x => x.Legs[1].Option);
            var leg3Options = SpreadGeneratorResults.Spreads.Where(x => x.Legs.Count >= 3).Select(x => x.Legs[2].Option);
            var leg4Options = SpreadGeneratorResults.Spreads.Where(x => x.Legs.Count >= 4).Select(x => x.Legs[3].Option);
            var allOptions = leg1Options.Union(leg2Options)
                                        .Union(leg3Options)
                                        .Union(leg4Options)
                                        .DistinctBy(x => x.Symbol)
                                        .ToList();
            deltaStore.GetHanweckDataFor(allOptions, SubscriptionFieldType.Delta);
            return deltaStore;
        }

        private async Task IdentifyHighestLegDelta(DataStore deltaStore)
        {
            List<Task> tasks = new();
            foreach (Spread spread in SpreadGeneratorResults.Spreads)
            {
                tasks.Add(SetHighestLegDeltaAsync(spread, deltaStore));
            }
            await Task.WhenAll(tasks);
        }

        private async Task SetHighestLegDeltaAsync(Spread spread, DataStore deltaStore)
        {
            double highestDelta = Math.Abs(await deltaStore.GetDataAsync(spread.Legs[0].Option.Symbol));
            double lowstDelta = Math.Abs(await deltaStore.GetDataAsync(spread.Legs[0].Option.Symbol));
            if (spread.Legs[1].Option != null)
            {
                double leg2Delta = Math.Abs(await deltaStore.GetDataAsync(spread.Legs[1].Option.Symbol));
                if (leg2Delta > highestDelta)
                {
                    highestDelta = leg2Delta;
                }
                if (leg2Delta < lowstDelta)
                {
                    lowstDelta = leg2Delta;
                }
            }
            if (spread.Legs[2].Option != null)
            {
                double leg3Delta = Math.Abs(await deltaStore.GetDataAsync(spread.Legs[2].Option.Symbol));
                if (leg3Delta > highestDelta)
                {
                    highestDelta = leg3Delta;
                }
                if (leg3Delta < lowstDelta)
                {
                    lowstDelta = leg3Delta;
                }
            }
            if (spread.Legs[3].Option != null)
            {
                double leg4Delta = Math.Abs(await deltaStore.GetDataAsync(spread.Legs[3].Option.Symbol));
                if (leg4Delta > highestDelta)
                {
                    highestDelta = leg4Delta;
                }
                if (leg4Delta < lowstDelta)
                {
                    lowstDelta = leg4Delta;
                }
            }
            spread.HighestLegDelta = Math.Round(highestDelta, 2);
            spread.LowestLegDelta = Math.Round(lowstDelta, 2);
        }
    }
}
