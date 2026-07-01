using DevExpress.Mvvm;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class AutoCancelSettingsViewModel : ViewModelBase
    {
        public AutoCancelConfig AutoCancelConfig { get; set; } = new();

        private bool resubmitAfterCancel;

        public bool ResubmitAfterCancel { get => resubmitAfterCancel; set => SetValue(ref resubmitAfterCancel, value); }

        private bool useHedgeUnderlyingForAutoCancel;

        public bool UseHedgeUnderlyingForAutoCancel { get => useHedgeUnderlyingForAutoCancel; set => SetValue(ref useHedgeUnderlyingForAutoCancel, value); }

        private bool cancelOnClose;

        public bool CancelOnClose { get => cancelOnClose; set => SetValue(ref cancelOnClose, value); }

        private bool cancelWithEdgeToTheoEnabled;

        public bool CancelWithEdgeToTheoEnabled { get => cancelWithEdgeToTheoEnabled; set => SetValue(ref cancelWithEdgeToTheoEnabled, value); }
    }
    public class AutoCancelConfig
    {
        // Timer-based cancellation
        public bool CancelWithTimerEnabled { get; set; }
        public double CancelWithTimer { get; set; }

        // Theoretical price edge cancellation
        public bool CancelWithEdgeToTheoEnabled { get; set; }
        public double CancelWithTheoEdge { get; set; }
        public bool CancelWithEdgeToAdjTheoEnabled { get; set; }
        public double CancelWithAdjTheoEdge { get; set; }

        // Underlying price cancellation
        public bool CancelWithUnderlyingPxEnabled { get; set; }
        public double CancelWithUnderlyingPx { get; set; }
        public bool CancelWithUnderlyingDeltaPxEnabled { get; set; }
        public double CancelWithUnderlyingDeltaPx { get; set; }

        // Mid price and width cancellation
        public bool CancelWithEdgeToMidEnabled { get; set; }
        public double CancelWithMidEdge { get; set; }
        public bool CancelWithWidthEnabled { get; set; }
        public double CancelWithWidthThreshold { get; set; }
    }
}