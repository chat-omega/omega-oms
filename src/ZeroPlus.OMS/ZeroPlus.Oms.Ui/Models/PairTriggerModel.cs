using DevExpress.Mvvm;
using Newtonsoft.Json;
using System;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Extensions;

namespace ZeroPlus.Oms.Ui.Models
{
    public class PairTriggerModel : BindableBase
    {
        private int _level;
        private int _qty;
        private double _base;
        private double _changeToEma;
        private double _profitTarget;
        private double _target;
        private double _closeTarget;
        private double _targetDiff;
        private bool _sent;
        private bool _disposed;
        private PairOrderModel _order;
        private PairOrderModel _closingOrder;
        private DateTime _sendTime;
        private readonly OpenCloseCounterModel _openCloseCounter = new(0, 0);
        private double _openingPx = double.NaN;
        private double _closingPx = double.NaN;
        private DateTime _openingTime;
        private DateTime _closingTime;
        private double _closeTimeSpan = double.NaN;
        private Side _side;
        private string _state;
        private double _totalUnitPnl;
        private double _avgUnitPnl;
        private int _totalFills;

        [JsonProperty]
        public Side Side
        {
            get { return _side; }
            set { SetValue(ref _side, value); }
        }

        [JsonProperty]
        public int Level
        {
            get { return _level; }
            set { SetValue(ref _level, value); }
        }

        [JsonProperty]
        public int Qty
        {
            get { return _qty; }
            set { SetValue(ref _qty, value); }
        }

        [JsonProperty]
        public double Base
        {
            get { return _base; }
            set { SetValue(ref _base, value); }
        }

        [JsonProperty]
        public double ChangeToEma
        {
            get { return _changeToEma; }
            set { SetValue(ref _changeToEma, value); }
        }

        [JsonProperty]
        public double ProfitTarget
        {
            get { return _profitTarget; }
            set { SetValue(ref _profitTarget, value); }
        }

        [JsonProperty]
        public double TargetDiff
        {
            get { return _targetDiff; }
            set { SetValue(ref _targetDiff, value); }
        }

        [JsonProperty]
        public double Target
        {
            get { return _target; }
            set { SetValue(ref _target, value); }
        }

        [JsonProperty]
        public double CloseTarget
        {
            get { return _closeTarget; }
            set { SetValue(ref _closeTarget, value); }
        }

        [JsonIgnore]
        public OpenCloseCounterModel OpenCloseCounter
        {
            get { return _openCloseCounter; }
        }

        [JsonIgnore]
        public bool Sent
        {
            get { return _sent; }
            set { SetValue(ref _sent, value); }
        }

        [JsonIgnore]
        public string State
        {
            get { return _state; }
            set { SetValue(ref _state, value); }
        }

        [JsonIgnore]
        public bool Disposed
        {
            get { return _disposed; }
            set { SetValue(ref _disposed, value); }
        }

        [JsonIgnore]
        public PairOrderModel Order
        {
            get { return _order; }
            set
            {
                SetValue(ref _order, value);
            }
        }

        [JsonIgnore]
        public PairOrderModel ClosingOrder
        {
            get { return _closingOrder; }
            set { SetValue(ref _closingOrder, value); }
        }

        [JsonIgnore]
        public DateTime SendTime
        {
            get { return _sendTime; }
            set { SetValue(ref _sendTime, value); }
        }

        [JsonIgnore]
        public double OpeningPx
        {
            get { return _openingPx; }
            set { SetValue(ref _openingPx, value); }
        }

        [JsonIgnore]
        public double ClosingPx
        {
            get { return _closingPx; }
            set { SetValue(ref _closingPx, value); }
        }

        [JsonIgnore]
        public double TotalUnitPnl
        {
            get { return _totalUnitPnl; }
            set { SetValue(ref _totalUnitPnl, value); }
        }

        [JsonIgnore]
        internal int TotalUnitFills
        {
            get { return _totalFills; }
            set { SetValue(ref _totalFills, value); }
        }

        [JsonIgnore]
        public double AvgUnitPnl
        {
            get { return _avgUnitPnl; }
            set { SetValue(ref _avgUnitPnl, value); }
        }

        [JsonIgnore]
        public DateTime OpeningTime
        {
            get { return _openingTime; }
            set { SetValue(ref _openingTime, value); }
        }

        [JsonIgnore]
        public DateTime ClosingTime
        {
            get { return _closingTime; }
            set { SetValue(ref _closingTime, value); }
        }

        [JsonIgnore]
        public double CloseTimeSpan
        {
            get { return _closeTimeSpan; }
            set { SetValue(ref _closeTimeSpan, value); }
        }

        [JsonIgnore]
        public bool IsClosing => ClosingOrder != null && !ClosingOrder.OrderStatus.IsClosed();


        [JsonConstructor]
        public PairTriggerModel()
        {
        }

        public PairTriggerModel(PairTriggerModel other, Side side)
        {
            Side = side;
            Level = other.Level;
            Qty = other.Qty;
            Base = other.Base;
            ChangeToEma = other.ChangeToEma;
            ProfitTarget = other.ProfitTarget;
        }

        internal void Reset()
        {
            Order = null;
            ClosingOrder = null;
            Sent = false;
            State = "Ready";
            OpeningPx = double.NaN;
            ClosingPx = double.NaN;
            CloseTimeSpan = double.NaN;
            OpeningTime = default;
            ClosingTime = default;
        }
    }
}
