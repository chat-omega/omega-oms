using System;
using DevExpress.Mvvm;
using MathNet.Numerics;
using Newtonsoft.Json;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class AutoPermConfigModel : BindableBase
    {
        private double _edge = 0.10;
        private double _edgeToEma = 0.05;
        private double _edgeToTheo = double.NaN;
        private double _widthPercentEdgeToTheo = double.NaN;
        private double _targetEdge;
        private double _edgePerContract = 0.025;
        private double _backupEdge;
        private int _initialSizeForPerms = 1;
        private int _hardSizeForPerms = 1;
        private double _minDelta;
        private double _maxDelta = 1;
        private double _maxDeltaAddition = 1;
        private double _maxLegDeltaDiff = .05;
        private int _minDte;
        private int _maxDte;
        private double _maxWeightedVegaDiff = 1;

        [JsonProperty]
        [Bindable(Default = true)]
        public partial bool Enabled { get; set; }
        [JsonProperty]
        [Bindable]
        public partial SideOperation SideOperation { get; set; }
        [JsonProperty]
        public double Edge
        {
            get => _edge;
            set => SetValue(ref _edge, value.Round(2));
        }
        [JsonProperty]
        [Bindable(Default = true)]
        public partial bool MatchTargetEdge { get; set; }
        [JsonProperty]
        [Bindable(Default = true)]
        public partial bool AttemptBothSides { get; set; }
        [JsonProperty]
        public double EdgeToEma
        {
            get => _edgeToEma;
            set => SetValue(ref _edgeToEma, value.Round(2));
        }
        [JsonProperty]
        public double EdgeToTheo
        {
            get => _edgeToTheo;
            set => SetValue(ref _edgeToTheo, value.Round(2));
        }
        [JsonProperty]
        public double WidthPercentEdgeToTheo
        {
            get => _widthPercentEdgeToTheo;
            set => SetValue(ref _widthPercentEdgeToTheo, value.Round(2));
        }
        [JsonProperty]
        [Bindable(Default = true)]
        public partial bool EdgeToEmaEnabled { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool EdgeToTheoEnabled { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool WidthPercentEdgeToTheoEnabled { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool UseBasketEdge { get; set; }
        [JsonProperty]
        public double TargetEdge
        {
            get => _targetEdge;
            set => SetValue(ref _targetEdge, value.Round(2));
        }
        [JsonProperty]
        public double EdgePerAddedLeg
        {
            get => _edgePerContract;
            set => SetValue(ref _edgePerContract, value.Round(3));
        }
        [JsonProperty]
        public double BackupEdge
        {
            get => _backupEdge;
            set => SetValue(ref _backupEdge, value.Round(2));
        }
        [JsonProperty]
        public double MinDelta
        {
            get => _minDelta;
            set => SetValue(ref _minDelta, value.Round(4));
        }
        [JsonProperty]
        public double MaxDelta
        {
            get => _maxDelta;
            set => SetValue(ref _maxDelta, value.Round(4));
        }
        [JsonProperty]
        public double MaxDeltaAddition
        {
            get => _maxDeltaAddition;
            set => SetValue(ref _maxDeltaAddition, value.Round(4));
        }
        [JsonProperty]
        public double MaxLegDeltaDiff
        {
            get => _maxLegDeltaDiff;
            set => SetValue(ref _maxLegDeltaDiff, value.Round(4));
        }
        [JsonProperty]
        public int MinDte
        {
            get => _minDte;
            set => SetValue(ref _minDte, value.Round(4));
        }
        [JsonProperty]
        public int MaxDte
        {
            get => _maxDte;
            set => SetValue(ref _maxDte, value.Round(4));
        }
        [JsonProperty]
        public double MaxWeightedVegaDiff
        {
            get => _maxWeightedVegaDiff;
            set => SetValue(ref _maxWeightedVegaDiff, value.Round(4));
        }
        [JsonProperty]
        public int InitialSizeForAutoPerms
        {
            get => _initialSizeForPerms;
            set => SetValue(ref _initialSizeForPerms, value);
        }
        [JsonProperty]
        public int HardSideInitialSizeForAutoPerms
        {
            get => _hardSizeForPerms;
            set => SetValue(ref _hardSizeForPerms, value);
        }
        [JsonProperty]
        [Bindable(Default = 1)]
        public partial int MaxGenForPerms { get; set; }
        [JsonProperty]
        [Bindable(Default = 20)]
        public partial int MaxNumberOfPerms { get; set; }
        [JsonProperty]
        [Bindable(Default = 1)]
        public partial int MaxOpenPerms { get; set; }
        [JsonProperty]
        [Bindable(Default = 2)]
        public partial int MaxResub { get; set; }
        [JsonProperty]
        [Bindable(Default = 2)]
        public partial int LevelResub { get; set; }
        [JsonProperty]
        [Bindable]
        public partial PermOperationModel AutoPermTemplate { get; set; }
        [JsonProperty]
        [Bindable(Default = true)]
        public partial bool SizeDownOnFill { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool StopOnFill { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool ReverseOnFill { get; set; }

        internal bool IsMatch(double lastEdge, int dte)
        {
            if (!Enabled)
            {
                return false;
            }

            if (MaxDte > 0 && (dte < MinDte || dte > MaxDte))
            {
                return false;
            }

            return SideOperation switch
            {
                SideOperation.Equal => Math.Abs(lastEdge.Round(2) - Edge) < .01,
                SideOperation.Greater => lastEdge.Round(2) > Edge,
                SideOperation.GreaterOrEqual => lastEdge.Round(2) >= Edge,
                _ => false,
            };
        }
    }
}
