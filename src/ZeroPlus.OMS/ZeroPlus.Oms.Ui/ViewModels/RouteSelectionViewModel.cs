using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Enums;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class RouteSelectionViewModel : ViewModelBase
    {
        private List<string> _allRoutes = new();
        private string _selectedRoute;
        private string _broker;
        private InstanceMode _instanceMode;

        [Bindable]
        public partial List<SelectionModel> Routes { get; set; }

        [Bindable]
        public partial SelectionModel Route { get; set; }

        [Bindable]
        public partial bool IsValid { get; set; }

        public string SelectedRoute
        {
            get => _selectedRoute;
            private set
            {
                if (_selectedRoute != value)
                {
                    _selectedRoute = value;
                    RaisePropertyChanged(nameof(SelectedRoute));
                    RaisePropertyChanged(nameof(DisplayName));
                }
            }
        }

        public string DisplayName => string.IsNullOrWhiteSpace(SelectedRoute) ? null : SelectedRoute;

        public OmsCore OmsCore { get; }

        public RouteSelectionViewModel(OmsCore omsCore)
        {
            OmsCore = omsCore;
            Routes = new List<SelectionModel>();
            if (OmsCore.Config != null)
            {
                OmsCore.Config.PropertyChanged += ConfigOnPropertyChanged;
            }
        }

        private void ConfigOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OmsCore.Config.SavedBroker)
                || e.PropertyName == nameof(OmsCore.Config.DefaultBroker))
            {
                Refresh(OmsCore.Config.DefaultBroker, _instanceMode);
            }
        }

        public void Refresh(string broker, InstanceMode mode)
        {
            _broker = broker;
            _instanceMode = mode;

            var routes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var routeLookup = OmsCore?.OrderClient?.RouteLookup;
            if (routeLookup != null && !string.IsNullOrWhiteSpace(broker))
            {
                foreach (var r in routeLookup.GetRoutesForBroker(broker))
                {
                    routes.Add(r);
                }
            }

            _allRoutes = routes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            Routes = _allRoutes.Select(x => new SelectionModel(x) { Selected = string.Equals(x, SelectedRoute, StringComparison.OrdinalIgnoreCase), Enabled = true }).ToList();

            IsValid = !string.IsNullOrWhiteSpace(SelectedRoute) && _allRoutes.Contains(SelectedRoute, StringComparer.OrdinalIgnoreCase);
        }

        public void LoadFromRoute(string route)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                SetSelectedRoute(null);
                return;
            }

            var name = StripBrokerPrefix(route);
            SetSelectedRoute(name);
        }

        public string BuildRouteString()
        {
            if (string.IsNullOrWhiteSpace(SelectedRoute))
            {
                return null;
            }
            if (string.IsNullOrWhiteSpace(_broker))
            {
                return SelectedRoute;
            }
            return $"{_broker}-{SelectedRoute}";
        }

        public static string StripBrokerPrefix(string route)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                return route;
            }
            int idx = route.IndexOf('-');
            if (idx > 0 && idx < route.Length - 1)
            {
                return route.Substring(idx + 1);
            }
            return route;
        }

        [Command]
        public void RouteSelectedCommand(SelectionModel model)
        {
            if (model == null)
            {
                return;
            }

            foreach (var r in Routes)
            {
                if (r != model)
                {
                    r.Selected = false;
                }
            }

            Route = model is { Selected: true } ? model : null;
            SetSelectedRoute(Route?.Name);
        }

        private void SetSelectedRoute(string name)
        {
            SelectedRoute = name;
            IsValid = !string.IsNullOrWhiteSpace(name);
            if (Routes != null)
            {
                foreach (var r in Routes)
                {
                    r.Selected = string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }
}
