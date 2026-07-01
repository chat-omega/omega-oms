using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Managers;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class BasketModel : ViewModelBase, IOmsDataSubscriber
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly IBasket _basket;
        private readonly PortfolioManagerModel _portfolioManager;


        public List<string> EdgeTypes => new()
        {
            "Theo",
            "Adj Theo",
            "Mid",
            "Deriv",
            "Theo & Mid",
            "Theo stop Mid",
            "Mid stop Ema",
            "% Bid stop Ema",
            "Bid %",
            "Bid stop Ema",
            "Bid"
        };

        #region public properties
        public string InstanceId { get; private set; }
        public bool IsDisposed { get; set; }

        [Bindable]
        public partial bool Active { get; set; }
        [Bindable]
        public partial string ModuleTitle { get; set; }
        [Bindable]
        public partial string SampleDescription { get; set; }
        [Bindable]
        public partial string Tag { get; set; }
        [Bindable]
        public partial string Trader { get; set; }
        [Bindable]
        public partial string Host { get; set; }
        [Bindable]
        public partial string Setup { get; set; }
        [Bindable]
        public partial string List { get; set; }
        [Bindable]
        public partial double RealizedPnl { get; set; }
        [Bindable]
        public partial double AdjustedPnl { get; set; }
        [Bindable]
        public partial string State { get; set; }
        [Bindable]
        public partial bool IsRunning { get; set; }
        [Bindable]
        public partial int RowCount { get; set; }
        [Bindable]
        public partial int Fills { get; set; }
        [Bindable]
        public partial double Edge { get; set; }
        [Bindable]
        public partial string EdgeType { get; set; }
        [Bindable]
        public partial TimeSpan ResubmitCountDown { get; set; }
        [Bindable]
        public partial int ResubmitIntervalSec { get; set; }
        [Bindable]
        public partial bool ResubmitOnTimer { get; set; }

        partial void OnResubmitOnTimerChanged(bool value) => ResubmitOnTimerChanged();
        [Bindable]
        public partial bool OpenTicket { get; set; }

        partial void OnOpenTicketChanged(bool value) => TicketOpenSettingsChanged();
        [Bindable]
        public partial bool OpenTicketOnManager { get; set; }

        partial void OnOpenTicketOnManagerChanged(bool value) => OpenTicketOnManagerSettingsChanged();
        #endregion

        public BasketModel(IBasket basket, PortfolioManagerModel portfolioManagerModel)
        {
            _basket = basket;
            _portfolioManager = portfolioManagerModel;
        }

        #region ViewModel Commands
        [AsyncCommand]
        public async Task ReverseSidessAsync()
        {
            await Task.Run(() => _basket.ReverseSidesNoCheck());
        }

        [AsyncCommand]
        public async Task FlipCPAsync()
        {
            await Task.Run(() => _basket.FlipCpNoCheck());
        }

        [AsyncCommand]
        public async Task OppCPAsync()
        {
            await Task.Run(() => _basket.OppCpNoCheck());
        }

        [AsyncCommand]
        public async Task CleanInvalidRowsAsync()
        {
            await Task.Run(() => _basket.CleanInvalidRows(false));
        }

        [AsyncCommand]
        public async Task SubmitAllAsync()
        {
            await Task.Run(() => _basket.SubmitAllNoCheckSafe());
        }

        [AsyncCommand]
        public async Task ModifyAllAsync()
        {
            await Task.Run(() => _basket.ModifyAllNoCheck());
        }

        [AsyncCommand]
        public async Task CancelAllAsync()
        {
            await Task.Run(() => _basket.CancelAllNoCheck());
        }

        [AsyncCommand]
        public async Task ResetTimer()
        {
            await Task.Run(() => _basket.ResetTimerNoCheck());
        }

        [AsyncCommand]
        public async Task StopAllLoops()
        {
            await Task.Run(() => _basket.StopAllLoops());
        }

        [AsyncCommand]
        public async Task EdgeChanged()
        {
            await Task.Run(() => _basket.SetEdge(EdgeType, Edge));
        }

        [AsyncCommand]
        public async Task Activate()
        {
            await Task.Run(() => _basket.Activate());
        }

        [AsyncCommand]
        public async Task Hide()
        {
            await Task.Run(() => _basket.Hide());
        }

        [AsyncCommand]
        public async Task Close()
        {
            await Task.Run(() => _basket.Close());
        }

        [Command]
        public void ResubmitOnTimerChanged()
        {
            if (ResubmitOnTimer)
            {
                _ = Task.Run(() => _basket.EnableResubmitTimer(ResubmitIntervalSec));
            }
            else
            {
                _ = Task.Run(() => _basket.DisableResubmitTimer(ResubmitIntervalSec));
            }
        }

        [Command]
        public void TicketOpenSettingsChanged()
        {
            if (OpenTicket)
            {
                _ = Task.Run(() => _basket.EnableOpenTicket());
            }
            else
            {
                _ = Task.Run(() => _basket.DisableOpenTicket());
            }
        }

        [Command]
        public void OpenTicketOnManagerSettingsChanged()
        {
            if (OpenTicketOnManager)
            {
                _ = Task.Run(() => _basket.EnableTicketProxy());
            }
            else
            {
                _ = Task.Run(() => _basket.DisableTicketProxy());
            }
        }
        #endregion

        internal void Update(IBasket basket)
        {
            ModuleTitle = basket.ModuleTitle;
            SampleDescription = basket.SampleDescription;
            Trader = basket.Username;
            Host = basket.Host;
            Setup = basket.Setup;
            List = basket.List;
            RowCount = basket.RowCount;
            Fills = basket.Fills;
            ResubmitCountDown = basket.ResubmitCountDown;
            ResubmitIntervalSec = basket.ResubmitIntervalSec;
            ResubmitOnTimer = basket.ResubmitOnTimer;
            Edge = basket.GetEdge();
            EdgeType = basket.GetEdgeType();
            OpenTicket = basket.GetOpenTicketState();
            if (InstanceId != basket.InstanceId)
            {
                InstanceId = basket.InstanceId;
                SubscribePnl();
            }
        }

        private void SubscribePnl()
        {
            try
            {
                _portfolioManager.Subscribe(InstanceId, SubscriptionFieldType.FirmInstancePosition, this);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribePnl));
            }
        }

        private void UnsubscribePnl()
        {
            try
            {
                _ = _portfolioManager.UnsubscribeAllAsync(this);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribePnl));
            }
        }

        internal void Dispose()
        {
            UnsubscribePnl();
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            try
            {
                switch (key.Type)
                {
                    case SubscriptionFieldType.FirmInstancePosition:
                        HandlePositionUpdate(value as IPosition);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribedDataUpdateValue));
            }
        }

        public void HandlePositionUpdate(IPosition instancePosition)
        {
            try
            {
                if (AdjustedPnl != instancePosition.AdjustedPnl)
                {
                    AdjustedPnl = instancePosition.AdjustedPnl;
                }
                if (RealizedPnl != instancePosition.RealizedPnl)
                {
                    RealizedPnl = instancePosition.RealizedPnl;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandlePositionUpdate));
            }
        }
    }
}
