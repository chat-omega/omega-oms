using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using ZeroPlus.Comms.Models.Data.Oms.DomsManager;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Managers;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class DominatorModel : BindableBase, IOmsDataSubscriber
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public List<DomState> ActiveStates { get; } = new List<DomState> { DomState.Dominating, DomState.FullAutoActive };

        public readonly Dominator Dominator;
        readonly PortfolioManagerModel _portfolioManagerModel;

        public List<string> DominatorSetups { get; private set; }
        public List<string> FullAutoSetups { get; private set; }
        public bool NegativeEdgeTest
        {
            get => Dominator?.NegativeTestEdge ?? false;
            set => Dominator.NegativeTestEdge = value;
        }
        public Dictionary<string, List<string>> CustomSetups { get; private set; }
        private OmsAutoTraderSettings omsAutoTraderSettings;
        public OmsAutoTraderSettings OmsAutoTraderSettings
        {
            get => omsAutoTraderSettings;
            set => SetValue(ref omsAutoTraderSettings, value);
        }
        private bool useAutoTrader = false;
        public bool UseAutoTrader
        {
            get => useAutoTrader;
            set
            {
                if (omsAutoTraderSettings is null)
                {
                    useAutoTrader = false;
                }
                else if (!OmsCore.Config.InstanceModeV3.IsAutoTraderInstance())
                {
                    useAutoTrader = false;
                }
                else
                {
                    SetValue(ref useAutoTrader, value);
                }
            }
        }

        void OpenInComplexOrderTicket(object sender, (string symbol, int quantity, OrderUpdateValues values) args)
        {
            var complexOrderTicketView = _factory.CreateModule(Module.ComplexOrderTicket);
            _log.Info($"Opening complex order ticket for {args.symbol} with quantity {args.quantity} and last price {args.values.LastPrice}");
            async void addSpread(object sender, EventArgs a)
            {
                if (complexOrderTicketView.DataContext is ComplexOrderTicketViewModel complexOrderTicketViewModel)
                {
                    await complexOrderTicketViewModel.LoadLegsFromTosAsync(args.symbol);
                    complexOrderTicketViewModel.Quantity = args.quantity;
                    complexOrderTicketViewModel.LastPrice = args.values.LastPrice;
                }
                _log.Info($"Adding spread to complex order ticket for {args.symbol} with quantity {args.quantity} and last price {args.values.LastPrice}");
                complexOrderTicketView.GotFocus -= addSpread;
            }
            complexOrderTicketView.GotFocus += addSpread;
            complexOrderTicketView.Show();
        }

        #region Properties

        [Bindable]
        public partial bool Active { get; set; }

        [Bindable]
        public partial bool Staged { get; set; }

        [Bindable]
        public partial bool UniqueSubmissionsOn { get; set; }

        [Bindable]
        public partial string Source { get; set; }

        [Bindable]
        public partial string Instance { get; set; }

        [Bindable]
        public partial string Trader { get; set; }

        [Bindable]
        public partial string Host { get; set; }

        [Bindable]
        public partial string Setup { get; set; }

        [Bindable]
        public partial string Configs { get; set; }

        [Bindable]
        public partial double RealizedPnl { get; set; }

        [Bindable]
        public partial double AdjustedPnl { get; set; }

        [Bindable]
        public partial string State { get; set; }

        [Bindable]
        public partial bool IsRunning { get; set; }

        [Bindable]
        public partial int DomCount { get; set; }

        [Bindable]
        public partial int Fills { get; set; }

        [Bindable]
        public partial double DeltaMax { get; set; }

        [Bindable]
        public partial int LoopSize { get; set; }

        [Bindable]
        public partial double EdgeMultiplier { get; set; }

        [Bindable]
        public partial string Product { get; set; }

        [Bindable]
        public partial string Type { get; set; }

        [Bindable]
        public partial string ListDate { get; set; }

        [Bindable]
        public partial string ListCreator { get; set; }

        [Bindable]
        public partial string FullName { get; set; }

        [Bindable]
        public partial string SubName { get; set; }

        [Bindable]
        public partial int ListCount { get; set; }

        [Bindable]
        public partial bool ShowNotification { get; set; }
        #endregion
        public bool IsDisposed { get; set; }
        private readonly IModuleFactory _factory;

        public DominatorModel(Dominator dominator, PortfolioManagerModel portfolioManagerModel, IModuleFactory factory)
        {
            _portfolioManagerModel = portfolioManagerModel;
            _factory = factory;
            Dominator = dominator;
            Dominator.ManualIntervention += OpenInComplexOrderTicket;
        }
        #region Commands
        [Command]
        public void StartAsync()
        {
            _ = Task.Run(() => Dominator.Start());
        }

        [Command]
        public void StopAsync()
        {
            _ = Task.Run(() => Dominator.Stop());
        }

        [Command]
        public void AllowUniqueSubmissionsAsync()
        {
            _ = Task.Run(() => Dominator.AllowUniqueSubmissions());
        }

        [Command]
        public void BlockUniqueSubmissionsAsync()
        {
            _ = Task.Run(() => Dominator.BlockUniqueSubmissions());
        }

        public void UpdateSettingsAsync(string settings)
        {
            _ = Task.Run(() => Dominator.UpdateSettings(settings));
        }

        public void LoadFullAutoSetup(string setup)
        {
            _ = Task.Run(() => Dominator.LoadFullAutoSetup(setup));
        }

        public void LoadDominatorSetup(string setup)
        {
            _ = Task.Run(() => Dominator.LoadDominatorSetup(setup));
        }

        public void LoadCustomSetup(string title, string setup)
        {
            _ = Task.Run(() => Dominator.LoadCustomSetup(title, setup));
        }

        [Command]
        public void LoadList(DomListInfo list)
        {
            _ = Task.Run(() => Dominator.LoadList(list.FileLocation, list.SubFileName));
        }

        [Command]
        public void CloseInstance()
        {
            _ = Task.Run(() => Dominator.CloseInstance());
        }
        #endregion

        #region IOmsDataSubscriber
        internal void Update(Dominator dominator)
        {
            if (Trader != dominator.Username)
            {
                Trader = dominator.Username;
            }
            if (Source != dominator.Source)
            {
                Source = dominator.Source;
                SubscribePnl();
            }
            if (Host != dominator.Host)
            {
                Host = dominator.Host;
            }
            DominatorSetups = dominator.DominatorSetups;
            FullAutoSetups = dominator.FullAutoSetups;
            CustomSetups = dominator.CustomSetups;
            if (Setup != dominator.Setup)
            {
                Setup = dominator.Setup;
            }
            if (Configs != dominator.Configs)
            {
                Configs = dominator.Configs;
            }
            if (State != dominator.State.ToString())
            {
                State = dominator.State.ToString();
            }
            if (DomCount != dominator.DomCount)
            {
                DomCount = dominator.DomCount;
            }
            if (Fills != dominator.Fills)
            {
                Fills = dominator.Fills;
            }
            if (EdgeMultiplier != dominator.EdgeMultiplier)
            {
                EdgeMultiplier = dominator.EdgeMultiplier;
            }
            if (DeltaMax != dominator.DeltaMax)
            {
                DeltaMax = dominator.DeltaMax;
            }
            if (LoopSize != dominator.LoopSize)
            {
                LoopSize = dominator.LoopSize;
            }
            if (Product != dominator.Product)
            {
                Product = dominator.Product;
            }
            if (Type != dominator.Type)
            {
                Type = dominator.Type;
            }
            if (ListDate != dominator.ListDate)
            {
                ListDate = dominator.ListDate;
            }
            if (ListCreator != dominator.ListCreator)
            {
                ListCreator = dominator.ListCreator;
            }
            if (FullName != dominator.FullName)
            {
                FullName = dominator.FullName;
            }
            if (SubName != dominator.SubName)
            {
                SubName = dominator.SubName;
            }
            if (ListCount != dominator.ListCount)
            {
                ListCount = dominator.ListCount;
            }
            if (UniqueSubmissionsOn != dominator.UniqueSubmissionsOn)
            {
                UniqueSubmissionsOn = dominator.UniqueSubmissionsOn;
            }
            if (IsRunning != ActiveStates.Contains(dominator.State))
            {
                IsRunning = ActiveStates.Contains(dominator.State);
            }
            if (dominator.ShowNotification && dominator.NotificationTimeout > 0)
            {
                dominator.ShowNotification = false;
                ShowNotification = true;
                SetupClearMessageTimer(dominator.NotificationTimeout);
            }
        }

        private void SubscribePnl()
        {
            try
            {
                string id = GetInstanceId();
                Instance = id;
                _portfolioManagerModel.Subscribe(id, SubscriptionFieldType.FirmInstancePosition, this);
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
                string id = GetInstanceId();
                _portfolioManagerModel.Unsubscribe(id, SubscriptionFieldType.FirmInstancePosition, this);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribePnl));
            }
        }

        private string GetInstanceId()
        {
            return string.Concat(Trader, " - DOM ", Source.AsSpan(Source.Length - 6, 1));
        }

        private void SetupClearMessageTimer(int timeout)
        {
            Timer timer = new(timeout * 1000);
            timer.Elapsed += ClearNotification;
            timer.AutoReset = false;
            timer.Start();
        }

        private void ClearNotification(object sender, ElapsedEventArgs e)
        {
            ShowNotification = false;
        }

        internal void Dispose()
        {
            UnsubscribePnl();
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            try
            {
                if (key.Type == SubscriptionFieldType.FirmInstancePosition)
                {
                    if (value is not null and IPosition position)
                    {
                        if (AdjustedPnl != position.AdjustedPnl)
                        {
                            AdjustedPnl = position.AdjustedPnl;
                        }
                        if (RealizedPnl != position.RealizedPnl)
                        {
                            RealizedPnl = position.RealizedPnl;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribedDataUpdateValue));
            }
        }
        #endregion

        #region DomCommands
        internal void SendTradeToDominator(TradeForDom trade)
        {
            try
            {
                Dominator.SendTradeToDominator(trade);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendTradeToDominator));
            }
        }

        internal void SaveLog(int delay)
        {
            try
            {
                Dominator.SaveLog(delay);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveLog));
            }
        }

        internal void RemoveHighDeltaSpreadsAndStart()
        {
            try
            {
                Dominator.RemoveHighDeltaSpreadsAndStart();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveHighDeltaSpreadsAndStart));
            }
        }

        internal void LoadEmaCapture()
        {
            try
            {
                Dominator.LoadEmaCapture();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadEmaCapture));
            }
        }

        internal void DisplayFirmTradeActivity()
        {
            try
            {
                Dominator.DisplayFirmTradeActivity();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DisplayFirmTradeActivity));
            }
        }

        internal void ChangeLoadPriceChain(bool enable)
        {
            try
            {
                Dominator.ChangeLoadPriceChain(enable);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ChangeLoadPriceChain));
            }
        }

        internal void ChangeLeastDataPossible(bool enable)
        {
            try
            {
                Dominator.ChangeLeastDataPossible(enable);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ChangeLeastDataPossible));
            }
        }

        internal void ChangeRoute(string route)
        {
            try
            {
                Dominator.ChangeRoute(route);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ChangeRoute));
            }
        }

        internal void SelectChannel(string channel)
        {
            try
            {
                Dominator.SelectChannel(channel);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SelectChannel));
            }
        }
        #endregion
    }
}
