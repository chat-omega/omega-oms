using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class ModifyStagedOrdersViewModel : ViewModelBase
    {
        public delegate void ModifyBasketEventHandler(string account, string route, TimeInForce? tif);
        public delegate void ModifyBasketQtyPxEventHandler(bool updateQty, int qty, bool updatePx, double px);

        public event ModifyBasketEventHandler ModifyBasketEvent;
        public event ModifyBasketQtyPxEventHandler ModifyBasketQtyPxEvent;


        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        [Bindable]
        public partial string Account { get; set; }

        [Bindable]
        public partial string Route { get; set; }

        [Bindable]
        public partial TimeInForce? Tif { get; set; }

        [Bindable]
        public partial bool UpdateQty { get; set; }

        [Bindable]
        public partial int Qty { get; set; }

        [Bindable]
        public partial bool UpdatePrice { get; set; }

        [Bindable]
        public partial double Price { get; set; }

        [Bindable]
        public partial ObservableCollection<string> AccountsList { get; set; }

        [Bindable]
        public partial ObservableCollection<string> RoutesList { get; set; }

        public List<TimeInForce> TifsList => Enum.GetValues<TimeInForce>().ToList();

        public Dispatcher Dispatcher { get; set; }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        public void InitializeForATR()
        {
            AccountsList = new ObservableCollection<string>();
            RoutesList = new ObservableCollection<string>();
            _ = InitAsync();
        }

        private async Task InitAsync()
        {
            HashSet<string> uniqueRoutes = new(StringComparer.OrdinalIgnoreCase);
            var currentBroker = OmsCore.Config.DefaultBroker;

            List<ZPAccount> accounts = await OmsCore.OrderClient.AccountsLookup.GetAccountsAsync(AccountsLookup.AccountsType.All);

            var routeLookup = OmsCore.OrderClient?.RouteLookup;
            var ogRoutes = !string.IsNullOrWhiteSpace(currentBroker)
                ? (routeLookup?.GetRoutesForBroker(currentBroker) ?? Array.Empty<string>())
                : (routeLookup?.GetRoutes() ?? Array.Empty<string>());
            foreach (var route in ogRoutes)
            {
                uniqueRoutes.Add(route);
            }

            var uniqueAccounts = accounts?.Select(x => x.Acronym).ToHashSet() ?? [];

            await Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (string account in uniqueAccounts)
                {
                    AccountsList.Add(account);
                }

                foreach (string route in uniqueRoutes.OrderBy(x => x))
                {
                    RoutesList.Add(route);
                }
            }));
        }

        [Command]
        public void Modify()
        {
            ModifyBasketEvent?.Invoke(Account, Route, Tif);
            CurrentWindowService?.Close();
        }

        [Command]
        public void ModifyQtyPx()
        {
            ModifyBasketQtyPxEvent?.Invoke(UpdateQty, Qty, UpdatePrice, Price);
            CurrentWindowService?.Close();
        }

        [Command]
        public void Cancel()
        {
            CurrentWindowService?.Close();
        }
    }
}
