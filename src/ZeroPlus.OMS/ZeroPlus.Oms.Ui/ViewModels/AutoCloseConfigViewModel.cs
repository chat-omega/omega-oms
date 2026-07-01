using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class AutoCloseConfigViewModel : CustomizableTableViewModelBase
    {
        private string _title;
        private bool _selected;
        private ObservableCollection<AutoCloseConfigTierModel> _autoCloseConfigTiers;
        private OrderTicket _parent;
        [JsonIgnore]
        public OrderTicket Parent
        {
            get => _parent; set => SetValue(ref _parent, value);
        }
        [JsonProperty]
        public string Title
        {
            get => _title; set => SetValue(ref _title, value);
        }
        [JsonProperty]
        public bool Selected
        {
            get => _selected; set => SetValue(ref _selected, value);
        }
        [JsonProperty]
        public ObservableCollection<AutoCloseConfigTierModel> AutoCloseConfigTiers
        {
            get => _autoCloseConfigTiers; set => SetValue(ref _autoCloseConfigTiers, value);
        }

        public AutoCloseConfigViewModel()
        {
            Title = string.Empty;
            AutoCloseConfigTiers = new ObservableCollection<AutoCloseConfigTierModel>();
        }

        [Command]
        public void RemoveAutoCloseTierItemCommand(AutoCloseConfigTierModel model)
        {
            AutoCloseConfigTiers.Remove(model);
        }

        [Command]
        public void AddNewAutoCloseTierConfigCommand()
        {
            AutoCloseConfigTierModel model = new();
            AutoCloseConfigTiers.Add(model);
        }

        [Command]
        public void SaveAutoCloseTierConfigCommand()
        {
            if (!Parent.AutoCloseConfigModels.Contains(this))
            {
                Parent.AutoCloseConfigModels.Add(this);
            }
            Parent.SaveAutoCloseConfigModels();
        }
    }
}
