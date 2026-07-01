using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Indicators;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class MacdContext : ViewModelBase
    {
        private readonly MacdCalculator macdCalculator;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public IEnumerable<DataType> DataTypes { get; } = ((DataType[])Enum.GetValues(typeof(DataType))).ToList();

        public MacdContext()
        {
            macdCalculator = new MacdCalculator();

            MacdValue = double.NaN;
            MacdBar = double.NaN;

            SignalEmaPeriods = 9;
            FastEmaPeriods = 12;
            SlowEmaPeriods = 26;
            SignalEmaSmoothing = 2;
            FastEmaSmoothing = 2;
            SlowEmaSmoothing = 2;
            MacdInterval = 60000;
            EmaConfigsChanged();

            MacdAutoTradingEnabled = false;
            EnterTolerance = 0;
            ExitTolerance = 0;

            macdCalculator.MacdUpdated += MacdCalculator_MacdUpdated;
        }


        #region ViewModel Properties

        [Bindable]
        public partial bool ManualMode { get; set; }

        [Bindable]
        public partial double MacdInterval { get; set; }

        [Bindable]
        public partial double FastEmaSmoothing { get; set; }
        [Bindable]
        public partial double FastEmaPeriods { get; set; }
        [Bindable]
        public partial double SlowEmaSmoothing { get; set; }
        [Bindable]
        public partial double SlowEmaPeriods { get; set; }
        [Bindable]
        public partial double SignalEmaSmoothing { get; set; }
        [Bindable]
        public partial double SignalEmaPeriods { get; set; }

        [Bindable]
        public partial double MacdValue { get; set; }

        [Bindable]
        public partial double MacdBar { get; set; }

        [Bindable]
        public partial double FastEMA { get; set; }
        [Bindable]
        public partial double SlowEMA { get; set; }

        bool _macdAutoTradingEnabled;
        public bool MacdAutoTradingEnabled
        {
            get => _macdAutoTradingEnabled;
            set
            {
                LivePositionOrder = null;
                SetValue(ref _macdAutoTradingEnabled, value);
            }
        }

        [Bindable]
        public partial double EnterTolerance { get; set; }

        [Bindable]
        public partial double ExitTolerance { get; set; }

        public event EventHandler MacdEntryLongEvent;
        public event EventHandler MacdExitLongEvent;
        public event EventHandler MacdEntryShortEvent;
        public event EventHandler MacdExitShortEvent;
        #endregion

        [Command]
        public void EmaConfigsChanged()
        {
            macdCalculator.SignalEmaConfig.EmaSmoothing = SignalEmaSmoothing;
            macdCalculator.SignalEmaConfig.EmaPeriods = SignalEmaPeriods;
            macdCalculator.SignalEmaConfig.EmaInterval = MacdInterval;

            macdCalculator.SlowEmaConfig.EmaSmoothing = SlowEmaSmoothing;
            macdCalculator.SlowEmaConfig.EmaPeriods = SlowEmaPeriods;
            macdCalculator.FastEmaConfig.EmaInterval = MacdInterval;

            macdCalculator.FastEmaConfig.EmaSmoothing = FastEmaSmoothing;
            macdCalculator.FastEmaConfig.EmaPeriods = FastEmaPeriods;
            macdCalculator.SlowEmaConfig.EmaInterval = MacdInterval;
        }

        private void MacdCalculator_MacdUpdated(double macd, double sigal, double bar)
        {
            _log.Debug("MacdAutoTradingEnabled={MacdAutoTradingEnabled}", MacdAutoTradingEnabled);
            if (MacdAutoTradingEnabled)
            {
                var IsOrderSent = SendMacdAutoOrder(oldMacdBar: MacdValue, newMacdBar: macd);
                _log.Debug("IsOrderSent={IsOrderSent}", IsOrderSent);
            }

            MacdValue = macd;
            MacdBar = bar;
            FastEMA = macdCalculator.FastEma;
            SlowEMA = macdCalculator.SlowEma;
        }

        public void UpdateMacd(double newValue)
        {
            try
            {
                macdCalculator.AddUpdate(newValue);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error in UpdateMacd({newValue}).", newValue);
            }
        }
        private bool PositionLive => LivePositionOrder is not null;
        public Models.PairOrderModel LivePositionOrder { get; set; }

        private bool SendMacdAutoOrder(double oldMacdBar, double newMacdBar)
        {
            if (double.IsNaN(oldMacdBar) || double.IsNaN(newMacdBar))
            {
                return false;
            }
            if (!PositionLive && EnterSignal(oldMacdBar, newMacdBar, EnterTolerance))
            {
                if (newMacdBar > 0) MacdEntryLongEvent?.Invoke(this, EventArgs.Empty);
                else if (newMacdBar < 0) MacdEntryShortEvent?.Invoke(this, EventArgs.Empty);
                return true;
            }
            else if (PositionLive && ExitSignal(oldMacdBar, newMacdBar, ExitTolerance))
            {
                if (LivePositionOrder?.Type != PositionEffect.Open)
                {
                    _log.Warn("Invalid LivePositionOrder PositionEffect not Open found in Exit Signal Check");
                    return false;
                }
                if (LivePositionOrder?.Side == ZeroPlus.Models.Data.Enums.Side.Buy) MacdExitLongEvent?.Invoke(this, EventArgs.Empty);
                else if (LivePositionOrder?.Side == ZeroPlus.Models.Data.Enums.Side.Sell) MacdExitShortEvent?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }


        /// The color of the bar of the histogram changes from bright to dark if its height is less than the last bar
        /// for green bars it means the value of MacdBar decreases becasue it is positive
        /// for red bars it means the value of MacdBar increases because it is negative
        private static bool ExitSignal(double oldMacdBar, double newMacdBar, double exitTolerance)
        {
            _log.Debug("Exit Signal Check: oldMacdBar={oldMacdBar}, MacdBar={newMacdBar}, ExitTolerance={exitTolerance}",
                oldMacdBar, newMacdBar, exitTolerance);
            if (Math.Sign(newMacdBar) == Math.Sign(oldMacdBar)
               && Math.Abs(newMacdBar) + exitTolerance < Math.Abs(oldMacdBar))
            {
                return true;
            }
            if (Math.Sign(newMacdBar) != Math.Sign(oldMacdBar))
            {
                return true;
            }
            return false;
        }

        /// If the Macd line crosses the signal line 
        private static bool EnterSignal(double oldMacdBar, double newMacdBar, double enterTolerance)
        {
            _log.Debug("Enter Signal Check: oldMacdBar={oldMacdBar}, MacdBar={newMacdBar}, EnterTolerance={enterTolerance}",
                oldMacdBar, newMacdBar, enterTolerance);
            if (Math.Sign(newMacdBar) != Math.Sign(oldMacdBar)
                && Math.Abs(newMacdBar - oldMacdBar) > enterTolerance)
            {
                return true;
            }
            return false;
        }

        internal void Start()
        {
            macdCalculator.Start();
        }

        internal void Stop()
        {
            macdCalculator.Stop();
            macdCalculator.Reset();
            MacdValue = double.NaN;
            MacdBar = double.NaN;
        }
    }
}
