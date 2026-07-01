using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.Modules
{
    public interface IModuleFactory
    {
        ModuleWindow CreateModule(Module module, bool loadDefault = true);
        ModuleWindow CreateModule(Module module, string id, bool loadDefault = true);
        ModuleWindow CreateModule(Module module, string id, string config);
        ModuleWindow CreateModule(Module module, string id, ConfigSave config);
        ModuleWindow CreateWindow(Module module, string id = null, bool loadDefault = true);
        bool IsPersistentDispatcher(Dispatcher dispatcher);
    }
}