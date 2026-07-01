using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Models.Extensions;

namespace ZeroPlus.Oms.Ui.Automation
{
    public class DataLoadedNotifier
    {
        private const int DEFAULT_INTERVAL = 1000;
        private readonly ManualResetEventSlim _manualReset = new(false);

        public bool IsSet { get; set; }

        public void Set()
        {
            if (!IsSet)
            {
                IsSet = true;
                _manualReset.Set();
            }
        }

        public void Reset()
        {
            IsSet = false;
            _manualReset.Reset();
        }

        public async Task<bool> WaitForLoadAsync(int timeout = DEFAULT_INTERVAL)
        {
            return IsSet || await _manualReset.WaitOneAsync(timeout);
        }
    }
}
