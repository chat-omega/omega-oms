using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using Newtonsoft.Json;
using NLog;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class LoopDynamicIncrementConfigViewModel : DynamicConfigEditorBase
    {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();


        public override Module Module => Module.DynamicIncrementConfigs;


        [Bindable]
        public partial ObservableCollection<DynamicIncrementConfigModel> DynamicIncrementConfigs { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double MaxPercentOfMarketWidth { get; set; }

        public LoopDynamicIncrementConfigViewModel(OmsCore omsCore) : base(omsCore)
        {
        }

        public void SetModel(LoopIncrementConfigModel model)
        {
            Model = model;
            if (model != null)
            {
                Title = model.Title;
                MaxPercentOfMarketWidth = model.MaxPercentOfMarketWidth;
                DynamicIncrementConfigs = model.DynamicIncrementConfigs?.ToObservableCollection() ?? new ObservableCollection<DynamicIncrementConfigModel>();
            }
        }

        [Command]
        public void AddNewDynamicIncrementConfigCommand()
        {
            DynamicIncrementConfigModel sizeupConfigModel = new();
            DynamicIncrementConfigs.Add(sizeupConfigModel);
        }

        [Command]
        public void RemoveDynamicIncrementItemCommand(DynamicIncrementConfigModel sizeupConfigModel)
        {
            DynamicIncrementConfigs.Remove(sizeupConfigModel);
        }

        [Command]
        public async Task SaveDynamicIncrementConfigCommand()
        {
            if (Model != null && Model is LoopIncrementConfigModel loopIncrementConfigModel)
            {
                loopIncrementConfigModel.Title = Title;
                loopIncrementConfigModel.DynamicIncrementConfigs = DynamicIncrementConfigs.ToList();
                loopIncrementConfigModel.MaxPercentOfMarketWidth = MaxPercentOfMarketWidth;
                await Save(loopIncrementConfigModel.GetAsJson());
            }
        }
    }
}
