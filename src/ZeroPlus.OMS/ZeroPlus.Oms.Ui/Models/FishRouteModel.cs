using DevExpress.Mvvm;
using System.Collections.Generic;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class FishRouteModel : BindableBase
    {
        public HashSet<string> RoutesList { get; set; }

        [Bindable]
        public partial string Routes { get; set; }

        partial void OnRoutesChanged(string value) => UpdateRoutesList();

        [Bindable]
        public partial double Edge { get; set; }

        [Bindable]
        public partial double Increment { get; set; }

        [Bindable]
        public partial double Interval { get; set; }

        public FishRouteModel(string routes, double edge, double increment, double interval)
        {
            Routes = routes;
            Edge = edge;
            Increment = increment;
            Interval = interval;
        }

        private void UpdateRoutesList()
        {
            RoutesList = new();
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
                    RoutesList.Add(route);
                }
            }
        }
    }
}
