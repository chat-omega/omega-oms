using DevExpress.Mvvm;
using NLog;
using System;

namespace ZeroPlus.Oms.Ui.Api
{
    public partial class RestApi : BindableBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private ApiHttpServer _httpServer;
        public bool _IsRunning;

        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        [Bindable]
        public partial bool IsRunning { get; set; }

        public void Start()
        {
            try
            {
                Stop();
                _httpServer = new ApiHttpServer(OmsCore.Config.RestApiAddress, OmsCore.Config.RestApiPort);
                _httpServer.ConnectionStatusChanged += ServerStatusChanged;
                _httpServer.Start();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Start));
            }
        }

        public void Stop()
        {
            try
            {
                if (_httpServer != null)
                {
                    _httpServer.ConnectionStatusChanged -= ServerStatusChanged;
                    _httpServer.Stop();
                    _httpServer.Dispose();
                }
                ServerStatusChanged(false);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Stop));
            }
        }

        private void ServerStatusChanged(bool connected)
        {
            IsRunning = connected;
        }
    }
}
