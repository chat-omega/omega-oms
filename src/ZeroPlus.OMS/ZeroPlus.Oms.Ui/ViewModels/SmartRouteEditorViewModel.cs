using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class SmartRouteEditorViewModel : CustomizableTableViewModelBase
    {
        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        public Action SaveAction { get; internal set; }

        [Bindable]
        public partial SmartRouteModel Route { get; set; }

        [Command]
        public void CloseCommand()
        {
            Route.IndexToRouteMap.Clear();
            for (int i = 0; i < Route.RouteModels.Count; i++)
            {
                SmartRoutesCancelDelayModel route = Route.RouteModels[i];
                if (string.IsNullOrWhiteSpace(route.Route))
                {
                    continue;
                }
                route.Route = route.Route.Trim().ToUpper();
                Route.IndexToRouteMap[i] = new Tuple<string, double>(route.Route, route.CancelDelay);
            }
            SaveAction?.Invoke();
            CurrentWindowService.Close();
        }
    }
}
