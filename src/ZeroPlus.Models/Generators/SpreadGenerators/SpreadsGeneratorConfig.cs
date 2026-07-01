using System;
using System.Collections.Generic;
using ZeroPlus.Models.Generators.SpreadGenerators.Settings;

namespace ZeroPlus.Models.Generators.SpreadGenerators
{
    public class SpreadsGeneratorConfig
    {
        public bool CallsEnabled { get; set; }
        public bool PutsEnabled { get; set; }
        public bool WholeStrikes { get; set; }
        public bool DecimalStrikes { get; set; }
        public bool MinStrikeEnabled { get; set; }
        public bool MaxStrikeEnabled { get; set; }
        public double MinStrike { get; set; }
        public double MaxStrike { get; set; }
        public bool MinStrikeOccurrenceEnabled { get; set; }
        public bool MaxStrikeOccurrenceEnabled { get; set; }
        public int MinStrikeOccurrence { get; set; }
        public int MaxStrikeOccurrence { get; set; }
        public bool StrikeDistanceFromLastPercentEnabled { get; set; }
        public double StrikeDistanceFromLastPercent { get; set; }
        public bool StrikeIncludeAsAdditionFromTopAndBottom { get; set; }
        public bool StrikeIncludeFromTopAndBottomEnabled { get; set; }
        public int StrikeIncludeFromTopAndBottomCount { get; set; }
        public bool MinExpEnabled { get; set; }
        public bool MaxExpEnabled { get; set; }
        public DateTime MinExp { get; set; }
        public DateTime MaxExp { get; set; }
        public bool Regulars { get; set; }
        public bool NonRegulars { get; set; }
        public bool Quarterlies { get; set; }
        public bool FreshSpreads { get; set; }
        public bool AttemptedSpreads { get; set; }
        public bool DteRangeEnabled { get; set; }
        public bool ApplyDteToExpSpacingMap { get; set; }
        public int MinDteRange { get; set; }
        public int MaxDteRange { get; set; }
        public bool MaxCountEnabled { get; set; }
        public bool Leg1LockEnabled { get; set; }
        public bool Leg2LockEnabled { get; set; }
        public bool Leg3LockEnabled { get; set; }
        public bool Leg4LockEnabled { get; set; }
        public string? Leg1LockOptions { get; set; }
        public string? Leg2LockOptions { get; set; }
        public string? Leg3LockOptions { get; set; }
        public string? Leg4LockOptions { get; set; }
        public bool ApproximateMissingQuotes { get; set; }
        public bool ApproximateMissingGreeks { get; set; }
        public bool ApproximateMissingHanweck { get; set; }
        public bool TopPercentageSelectionEnabled { get; set; }
        public double TopPercentageSelectionCount { get; set; }
        public OutputSortingMode TopPercentageSelectionSortingMode { get; set; }
        public int MaxCount { get; set; }
        public bool EvenlyDistribute { get; set; }
        public bool SingleLegEnabled { get; set; }
        public bool VerticalEnabled { get; set; }
        public bool OneByTwoRatioEnabled { get; set; }
        public bool OneByThreeRatioEnabled { get; set; }
        public bool RatioEnabled { get; set; }
        public bool ButterflyEnabled { get; set; }
        public bool SkewedButterflyEnabled { get; set; }
        public bool TreeEnabled { get; set; }
        public bool CalendarButterflyEnabled { get; set; }
        public bool IronButterflyEnabled { get; set; }
        public bool IronGutFlyEnabled { get; set; }
        public bool CalendarEnabled { get; set; }
        public bool DiagonalEnabled { get; set; }
        public bool CondorEnabled { get; set; }
        public bool IronCondorEnabled { get; set; }
        public bool OneThreeThreeOneEnabled { get; set; }
        public bool OneThreeTwoEnabled { get; set; }
        public bool TwoThreeOneEnabled { get; set; }
        public bool BoxEnabled { get; set; }
        public bool ExportToFile { get; set; }
        public bool OpenInBasket { get; set; }
        public string? UnderlyingQuery { get; set; }
        public RunnerOption RunnerOption { get; set; }
        public int ParsedOutputCount { get; set; }
        public int ParsedBuildCount { get; set; }
        public bool ParsedOutputEnabled { get; set; }
        public List<DateTime> ExcludedExpirations { get; set; } = new List<DateTime>();
        public ISpreadGeneratorIntFilter? Leg1OpenInterestFilter { get; set; }
        public ISpreadGeneratorIntFilter? Leg2OpenInterestFilter { get; set; }
        public ISpreadGeneratorIntFilter? Leg3OpenInterestFilter { get; set; }
        public ISpreadGeneratorIntFilter? Leg4OpenInterestFilter { get; set; }
        public ISpreadGeneratorIntFilter? Leg1VolumeFilter { get; set; }
        public ISpreadGeneratorIntFilter? Leg2VolumeFilter { get; set; }
        public ISpreadGeneratorIntFilter? Leg3VolumeFilter { get; set; }
        public ISpreadGeneratorIntFilter? Leg4VolumeFilter { get; set; }
        public ISingleLegSpreadsGeneratorSettings? SingleLegSpreadsSettings { get; set; }
        public IVerticalSpreadsGeneratorSettings? VerticalSpreadsSettings { get; set; }
        public IRatioSpreadsGeneratorSettings? OneByTwoRatioSpreadsSettings { get; set; }
        public IRatioSpreadsGeneratorSettings? OneByThreeRatioSpreadsSettings { get; set; }
        public IRatioSpreadsGeneratorSettings? RatioSpreadsSettings { get; set; }
        public ICalendarSpreadsGeneratorSettings? CalendarSpreadsSettings { get; set; }
        public IDiagonalSpreadsGeneratorSettings? DiagonalSpreadsSettings { get; set; }
        public IButterflySpreadsGeneratorSettings? ButterflySpreadsSettings { get; set; }
        public ISkewedButterflySpreadsGeneratorSettings? SkewedButterflySpreadsSettings { get; set; }
        public ITreeSpreadsGeneratorSettings? TreeSpreadsSettings { get; set; }
        public ICalendarButterflySpreadsGeneratorSettings? CalendarButterflySpreadsSettings { get; set; }
        public IIronButterflySpreadsGeneratorSettings? IronButterflySpreadsSettings { get; set; }
        public IIronGutFlySpreadsGeneratorSettings? IronGutFlySpreadsSettings { get; set; }
        public ICondorSpreadsGeneratorSettings? CondorSpreadsSettings { get; set; }
        public IOneThreeThreeOneSpreadsGeneratorSettings? OneThreeThreeOneSpreadsSettings { get; set; }
        public IIronCondorSpreadsGeneratorSettings? IronCondorSpreadsSettings { get; set; }
        public IOneThreeTwoSpreadsGeneratorSettings? OneThreeTwoSpreadsSettings { get; set; }
        public IOneThreeTwoSpreadsGeneratorSettings? TwoThreeOneSpreadsSettings { get; set; }
        public IBoxSpreadsGeneratorSettings? BoxSpreadsSettings { get; set; }
    }
}
