using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Requests;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class AdminControlsViewModel : ModuleViewModelBase
    {
        private ICollection<string> _allRoutes = [];

        public override Module Module { get; protected set; } = Module.AdminControls;

        public IEnumerable<Venue> Venues { get; } = Enum.GetValues<Venue>();
        public IEnumerable<Broker> Brokers { get; } = Enum.GetValues<Broker>();
        public IEnumerable<MassCancelType> MassCancelTypes { get; } = Enum.GetValues<MassCancelType>();

        public ObservableCollection<string> FilteredExchanges { get; } = [];

        [Bindable(Default = Venue.ZpFix)]
        public partial Venue SelectedVenue { get; set; }

        private Broker _selectedBroker = Broker.MTRX;
        public Broker SelectedBroker
        {
            get => _selectedBroker;
            set
            {
                if (SetValue(ref _selectedBroker, value))
                {
                    RefreshFilteredExchanges();
                }
            }
        }

        [Bindable(Default = MassCancelType.CancelAll)]
        public partial MassCancelType SelectedMassCancelType { get; set; }

        [Bindable]
        public partial string SelectedExchange { get; set; }

        public AdminControlsViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore)
            : base(configBrowserViewModel, omsCore)
        {
            LoadRoutes();
        }

        private void LoadRoutes()
        {
            try
            {
                _allRoutes = OmsCore.AutoTraderClient.GetRoutes();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadRoutes));
            }
        }

        private void RefreshFilteredExchanges()
        {
            SelectedExchange = null;
            FilteredExchanges.Clear();

            var brokerPrefix = SelectedBroker.ToString() + "-";
            foreach (var route in _allRoutes.Where(r => r.StartsWith(brokerPrefix, StringComparison.OrdinalIgnoreCase)))
            {
                FilteredExchanges.Add(route);
            }
        }

        [Command]
        public void SendMassCancelCommand()
        {
            try
            {
                var request = new MassCancelRequest
                {
                    Venue = SelectedVenue,
                    Broker = SelectedBroker,
                    CancelType = SelectedMassCancelType,
                    Exchange = SelectedExchange,
                    Account = OmsCore.Config.DefaultAccount,
                };

                _log.Info($"Sending Mass Cancel. Venue: {request.Venue}, Broker: {request.Broker}, Type: {request.CancelType}, Exchange: {request.Exchange ?? "None"}");
                OmsCore.AutoTraderClient.SendMassCancelRequest(request);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendMassCancelCommand));
            }
        }

        public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
        {
            return default;
        }

        public override Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
        {
            return Task.CompletedTask;
        }
    }
}
