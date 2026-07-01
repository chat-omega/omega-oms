using DevExpress.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ZeroPlus.Oms.Ui.Models
{

    public partial class SmartRoutesCancelDelayModel : BindableBase
    {

        [Bindable]
        public partial string Route { get; set; }

        [Bindable]
        public partial double CancelDelay { get; set; }

        public SmartRoutesCancelDelayModel(string route, double cancelDelay)
        {
            Route = route;
            CancelDelay = cancelDelay;
        }
    }

    public partial class SmartRouteModel : BindableBase
    {
        public Dictionary<int, Tuple<string, double>> IndexToRouteMap { get; set; }

        [Bindable]
        public partial string Name { get; set; }

        [Bindable]
        public partial string Routes { get; set; }

        partial void OnRoutesChanged(string value) => UpdateRoutesMap();

        [Bindable]
        public partial ObservableCollection<SmartRoutesCancelDelayModel> RouteModels { get; set; }

        public SmartRouteModel(string name, Dictionary<int, Tuple<string, double>> routesMap)
        {
            Name = name;
            IndexToRouteMap = routesMap;
            RouteModels = new ObservableCollection<SmartRoutesCancelDelayModel>();
            string routes = "";
            foreach (int key in routesMap.Keys.OrderBy(x => x))
            {
                routes += routesMap[key].Item1 + ", ";
            }
            Routes = routes;
        }

        public void UpdateRoutesMap()
        {
            Dictionary<string, double> lookup = new();
            foreach (Tuple<string, double> kvp in IndexToRouteMap.Values)
            {
                lookup[kvp.Item1] = kvp.Item2;
            }

            Dictionary<int, Tuple<string, double>> indexToRoutesMap = new();
            if (Routes != null)
            {
                string[] routes = Routes.Split(',');
                for (int i = 0; i < routes.Length; i++)
                {
                    string route = routes[i];
                    if (string.IsNullOrWhiteSpace(route))
                    {
                        continue;
                    }
                    route = route.Trim().ToUpper();
                    if (!lookup.TryGetValue(route, out double smartRouteOverwatchTimer))
                    {
                        smartRouteOverwatchTimer = OmsCore.Config.SmartRouteOverwatchTimer;
                    }
                    indexToRoutesMap[i] = new Tuple<string, double>(route, smartRouteOverwatchTimer);
                }
            }

            IndexToRouteMap = indexToRoutesMap;
            RouteModels.Clear();
            foreach (Tuple<string, double> map in IndexToRouteMap.Values)
            {
                RouteModels.Add(new SmartRoutesCancelDelayModel(map.Item1, map.Item2));
            }
        }
    }
}