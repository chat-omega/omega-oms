using DevExpress.Mvvm;
using System;
using System.Windows.Threading;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Config;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class LatencyIndicatorViewModel : ViewModelBase
    {
        private readonly OmsCore _omsCore;
        private readonly DispatcherTimer _timer;

        private double _ServerCreepMs = double.NaN;
        private double _DatabentoServerLatencyMs = double.NaN;

        public OmsConfig Config => OmsCore.Config;

        public double ServerCreepMs
        {
            get => _ServerCreepMs;
            private set => SetValue(ref _ServerCreepMs, value);
        }

        public double DatabentoServerLatencyMs
        {
            get => _DatabentoServerLatencyMs;
            private set => SetValue(ref _DatabentoServerLatencyMs, value);
        }

        public LatencyIndicatorViewModel(OmsCore omsCore)
        {
            _omsCore = omsCore;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void OnTick(object sender, EventArgs e)
        {
            ServerCreepMs = _omsCore.QuoteClient.IsConnected
                ? Math.Round(_omsCore.QuoteClient.ServerCreepMs, 1)
                : double.NaN;

            DatabentoServerLatencyMs = _omsCore.DatabentoClient.IsConnected
                ? Math.Round(_omsCore.UpdateManager.DatabentoLatencyMs, 1)
                : double.NaN;
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
        }
    }
}
