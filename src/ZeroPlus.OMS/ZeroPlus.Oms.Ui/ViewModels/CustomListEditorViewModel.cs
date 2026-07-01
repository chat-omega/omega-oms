using System;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Models;
using Newtonsoft.Json;
using ZeroPlus.Comms.Models.Data.Oms.Config;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class CustomListEditorViewModel : DynamicConfigEditorBase
    {
        public override Module Module => Module.CustomList;


        [Bindable]
        public partial ObservableCollection<InputModel> Symbols { get; set; }

        public CustomListEditorViewModel(OmsCore omsCore) : base(omsCore)
        {
        }

        public void Init(CustomListModel model)
        {
            if (model != null)
            {
                Model = model;
                Title = model.Title;
                Symbols = model.SymbolModels.ToObservableCollection();
            }
        }

        [Command]
        public async Task RefreshCommand()
        {
            if (Model != null)
            {
                ConfigSave details = await Task.Run(() => OmsCore.GatewayClient.RequestConfigDataAsync(Model.Id));
                if (details != null)
                {
                    CustomListModel customListModel = JsonConvert.DeserializeObject<CustomListModel>(details.ConfigJson);
                    if (customListModel != null)
                    {
                        Symbols = customListModel.SymbolModels.ToObservableCollection();
                    }
                }
            }
        }

        [Command]
        public void AddCommand()
        {
            Symbols.Add(new InputModel("", OmsCore.User.Username, DateTime.Now));
        }

        [Command]
        public void RemoveCommand(InputModel model)
        {
            Symbols.Remove(model);
        }

        [Command]
        public async Task SaveCommand()
        {
            if (Model is CustomListModel listModel)
            {
                listModel.Title = Title;
                listModel.SymbolModels = Symbols.ToHashSet();
                await Save(listModel.GetAsJson(), skipPermissionCheck: true);
            }
        }
    }
}
