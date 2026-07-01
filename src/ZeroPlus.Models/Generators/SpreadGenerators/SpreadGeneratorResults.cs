using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Newtonsoft.Json;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;

namespace ZeroPlus.Models.Generators.SpreadGenerators
{
    [Serializable]
    public class SpreadLeg
    {
        [JsonProperty]
        public int Ratio { get; set; }
        [JsonProperty]
        public Side Side { get; set; }
        [JsonProperty]
        public Option? Option { get; set; }

        public SpreadLeg()
        {
            Ratio = 1;
            Side = Side.Buy;
        }

        public SpreadLeg(Option option) : this()
        {
            Option = option;
        }

        public SpreadLeg(Option option, Side side) : this()
        {
            Side = side;
            Option = option;
        }

        public SpreadLeg(Option option, Side side, int ratio)
        {
            Ratio = ratio;
            Side = side;
            Option = option;
        }
    }

    [Serializable]
    public class Spread
    {
        [JsonProperty]
        public List<SpreadLeg> Legs { get; set; } = new();
        [JsonIgnore]
        public int[] Ratios => Legs.Select(x => x.Ratio).ToArray();
        [JsonProperty]
        public string? Symbol { get; set; }
        [JsonProperty]
        public double EdgeOverride { get; set; } = double.NaN;
        [JsonProperty]
        public double HighestLegDelta { get; set; } = double.NaN;
        [JsonProperty]
        public double LowestLegDelta { get; set; } = double.NaN;
        [JsonProperty]
        public string? Side { get; set; }
        [JsonProperty]
        public double Distance { get; set; } = double.MaxValue;
        [JsonProperty]
        public double Width { get; set; }

        [JsonConstructor]
        public Spread() : this(string.Empty)
        {

        }

        public Spread(string tos) : this(tos, double.NaN)
        {
        }

        public Spread(string tos, double width)
        {
            Symbol = tos;
            Width = width;
        }

        public DateTime GetMinExpiration()
        {
            HashSet<DateTime> expirations = GetExpirations();
            return expirations.MinBy(x => x);
        }

        public string GetExpirationString()
        {
            HashSet<DateTime> expirations = GetExpirations();
            return string.Join(" - ", expirations.Select(x => x.ToString("MMM dd yy")));
        }

        private HashSet<DateTime> GetExpirations()
        {
            HashSet<DateTime> expirations = new(Legs.Count);
            if (Legs.Count > 0)
            {
                var leg = Legs[0].Option;
                if (leg != null)
                {
                    expirations.Add(leg.Expiration);
                }
            }
            if (Legs.Count > 1)
            {
                var leg = Legs[1].Option;
                if (leg != null)
                {
                    expirations.Add(leg.Expiration);
                }
            }
            if (Legs.Count > 2)
            {
                var leg = Legs[2].Option;
                if (leg != null)
                {
                    expirations.Add(leg.Expiration);
                }
            }
            if (Legs.Count > 3)
            {
                var leg = Legs[3].Option;
                if (leg != null)
                {
                    expirations.Add(leg.Expiration);
                }
            }

            return expirations;
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (obj is Spread spread)
            {
                return spread.Symbol == Symbol;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return Symbol?.GetHashCode() ?? 0;
        }
    }

    [Serializable]
    public class SpreadGeneratorResults : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private double _frontMonthExpirationsPercent = 1;
        private double _backMonthExpirationsPercent = 1;
        private int _minCount;
        private int _totalCount;

        [JsonProperty]
        public string Title { get; set; }
        [JsonProperty]
        public HashSet<string> Errors { get; set; }
        [JsonProperty]
        public string? Underlying { get; set; }
        [JsonProperty]
        public PutCall? Type { get; set; }
        [JsonProperty]
        public Strategy Strategy { get; set; }
        [JsonIgnore]
        public ObservableCollection<ExpirationInfo> Expirations { get; set; }
        [JsonProperty]
        public HashSet<Spread> Spreads { get; set; }
        [JsonProperty]
        public double FrontMonthExpirationsPercent
        {
            get => _frontMonthExpirationsPercent;
            set
            {
                _frontMonthExpirationsPercent = value;
                NotifyPropertyChanged();
            }
        }
        [JsonProperty]
        public double BackMonthExpirationsPercent
        {
            get => _backMonthExpirationsPercent;

            set
            {
                _backMonthExpirationsPercent = value;
                NotifyPropertyChanged();
            }
        }
        [JsonProperty]
        public int MinCount
        {
            get => _minCount;

            set
            {
                _minCount = value;
                NotifyPropertyChanged();
            }
        }
        [JsonProperty]
        public int TotalCount
        {
            get => _totalCount;

            set
            {
                _totalCount = value;
                NotifyPropertyChanged();
            }
        }

        [JsonConstructor]
        public SpreadGeneratorResults()
        {
            Underlying = string.Empty;
            Type = null;
            Title = string.Empty;
            Spreads = new HashSet<Spread>();
            Errors = new HashSet<string>();
            Expirations = new ObservableCollection<ExpirationInfo>();
        }

        public SpreadGeneratorResults(string? underlying, PutCall? type, Strategy strategy)
        {
            Underlying = underlying;
            Type = type;
            Strategy = strategy;
            Title = underlying + " " + type + " " + strategy.ToString();
            Spreads = new HashSet<Spread>();
            Errors = new HashSet<string>();
            Expirations = new ObservableCollection<ExpirationInfo>();
        }

        public SpreadGeneratorResults Select(int count, CancellationToken token)
        {
            Spread[] spreads = Spreads.ToArray();
            double step = count == 1 ? 1 : (spreads.Length - 1) / (double)(count - 1);

            SpreadGeneratorResults selected = new(Underlying, Type, Strategy);

            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                int index = (int)Math.Round(step * i, 0);
                selected.Spreads.Add(spreads[index]);
            }

            return selected;
        }

        public void UpdateExpirations()
        {
            if (Spreads != null)
            {
                IEnumerable<ExpirationInfo> expirations = Spreads.GroupBy(x => x.GetExpirationString()).Select(x => new ExpirationInfo(x.Key, x.First().GetMinExpiration(), x.Count()));
                Expirations = new ObservableCollection<ExpirationInfo>(expirations.OrderBy(x => x.MinExpiration));
                TotalCount = Spreads.Count;
                MinCount = Expirations.Count == 0 ? 0 : Expirations.Min(x => x.Count);
            }
            UpdateExpirationPercentage();
        }

        public void UpdateExpirationPercentage()
        {
            if (Expirations != null && Expirations.Count > 0)
            {
                double increment = Math.Abs(BackMonthExpirationsPercent - FrontMonthExpirationsPercent) / (Expirations.Count - 1);
                ExpirationInfo[] expirations = FrontMonthExpirationsPercent < BackMonthExpirationsPercent ? Expirations.OrderBy(x => x.MinExpiration).ToArray() : Expirations.OrderByDescending(x => x.MinExpiration).ToArray();
                double min = Math.Min(FrontMonthExpirationsPercent, BackMonthExpirationsPercent);
                int quota = TotalCount / Expirations.Count;
                for (int i = 0; i < expirations.Length; i++)
                {
                    ExpirationInfo expiration = expirations[i];
                    expiration.Percent = min + i * increment;
                    expiration.Quota = quota;
                    expiration.UpdateTarget(MinCount);
                }
            }
        }

        public void ParseToTarget(CancellationToken token)
        {
            foreach (ExpirationInfo expirationInfo in Expirations)
            {
                ProcessExpirationInfoByElimination(expirationInfo, token);
            }
        }

        private void ProcessExpirationInfoByElimination(ExpirationInfo expirationInfo, CancellationToken token)
        {
            List<Spread> spreadsList = Spreads.Where(x => x.GetExpirationString() == expirationInfo.Expiration).ToList();
            while (expirationInfo.Count > expirationInfo.Target)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                for (int i = 0; i < spreadsList.Count; i++)
                {
                    Spread spread = spreadsList[i];
                    spread.Distance = double.MaxValue;
                    for (int j = i + 1; j < spreadsList.Count; j++)
                    {
                        Spread comperand = spreadsList[j];
                        double value = 0.0;
                        if (spread.Legs.Count >= 4)
                        {
                            value += Math.Abs(spread.Legs[0].Option!.Strike - comperand.Legs[0].Option!.Strike);
                            value += Math.Abs(spread.Legs[1].Option!.Strike - comperand.Legs[1].Option!.Strike);
                            value += Math.Abs(spread.Legs[2].Option!.Strike - comperand.Legs[2].Option!.Strike);
                            value += Math.Abs(spread.Legs[3].Option!.Strike - comperand.Legs[3].Option!.Strike);
                        }
                        else if (spread.Legs.Count >= 3)
                        {
                            value += Math.Abs(spread.Legs[0].Option!.Strike - comperand.Legs[0].Option!.Strike);
                            value += Math.Abs(spread.Legs[1].Option!.Strike - comperand.Legs[1].Option!.Strike);
                            value += Math.Abs(spread.Legs[2].Option!.Strike - comperand.Legs[2].Option!.Strike);
                        }
                        else if (spread.Legs.Count >= 2)
                        {
                            value += Math.Abs(spread.Legs[0].Option!.Strike - comperand.Legs[0].Option!.Strike);
                            value += Math.Abs(spread.Legs[1].Option!.Strike - comperand.Legs[1].Option!.Strike);
                        }
                        else if (spread.Legs.Count >= 1)
                        {
                            value += Math.Abs(spread.Legs[0].Option!.Strike - comperand.Legs[0].Option!.Strike);
                        }

                        if (value < spread.Distance)
                        {
                            spread.Distance = value;
                        }
                        if (value == 0)
                        {
                            spread.Distance = value;
                            break;
                        }
                    }
                }

                Spread? min = spreadsList.MinBy(x => x.Distance);
                if (min != null)
                {
                    Spreads.Remove(min);
                    spreadsList.Remove(min);
                    expirationInfo.Count--;
                }
                else
                {
                    break;
                }
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
