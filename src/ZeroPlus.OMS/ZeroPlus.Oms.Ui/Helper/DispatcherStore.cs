using System.Collections.Concurrent;
using System.Windows.Threading;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Helper
{
    public class DispatcherStore
    {
        private readonly ConcurrentDictionary<Module, Dispatcher> _moduleToDispatcherMap = new();

        public void SetModuleCommonDispatcher(Module module, Dispatcher dispatcher)
        {
            _moduleToDispatcherMap[module] = dispatcher;
        }

        public Dispatcher GetDispatcherForModule(Module module)
        {
            _moduleToDispatcherMap.TryGetValue(module, out var dispatcher);
            return dispatcher;
        }

        public bool IsPersistentDispatcher(Dispatcher dispatcher)
        {
            return _moduleToDispatcherMap.Values.Contains(dispatcher);
        }
    }
}
