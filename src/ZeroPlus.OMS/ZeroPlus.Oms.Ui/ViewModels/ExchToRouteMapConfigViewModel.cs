using System.Collections.Generic;
using System.Collections.ObjectModel;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public delegate void MappingUpdatedHandler(Dictionary<string, string> map);
    public partial class ExchToRouteMapConfigViewModel : CustomizableTableViewModelBase
    {
        public event MappingUpdatedHandler MappingUpdated;
        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        [Bindable]
        public partial ObservableCollection<ExchToRouteMapModel> Mapping { get; set; }

        public ExchToRouteMapConfigViewModel()
        {
            Mapping = new();
        }

        [Command]
        public void AddCommand()
        {
            ExchToRouteMapModel item = new();
            Mapping.Add(item);
        }

        [Command]
        public void SaveCommand()
        {
            Dictionary<string, string> map = new();
            foreach (var kvp in Mapping)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Exchange) && !string.IsNullOrWhiteSpace(kvp.Route))
                {
                    map[kvp.Exchange.ToUpper()] = kvp.Route.ToUpper();
                }
            }
            MappingUpdated?.Invoke(map);
            CurrentWindowService.Close();
        }

        [Command]
        public void RemoveCommand(ExchToRouteMapModel item)
        {
            Mapping.Remove(item);
        }
    }
}
