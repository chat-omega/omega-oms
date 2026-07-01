using System;
using System.Windows.Threading;

namespace ZeroPlus.Oms.Ui.ViewModels.Interfaces
{
    public interface IModuleViewModel : IDisposable
    {
        event ReadyEventHandler Ready;
        
        bool IsReady { get; }
        bool IsDisposed { get; }
        public string ModuleTitle { get; set; }
        OmsCore OmsCore { get; }
        Dispatcher Dispatcher { get; set; }
        bool AllowSave { get; set; }
        void SetDispatcher(Dispatcher dispatcher);
    }
}
