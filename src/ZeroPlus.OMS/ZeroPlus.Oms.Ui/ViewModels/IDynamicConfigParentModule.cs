using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels;

public interface IDynamicConfigParentModule
{
    int GetCurrentConfigId(Module configModule);
    IDynamicConfigModel GetDynamicConfig(Module configModule, string json = null);
    void LoadDynamicConfig(Module configModule, IDynamicConfigModel currentModel);
    void EditDynamicConfig(Module configModule, IDynamicConfigModel selectedModel);
}