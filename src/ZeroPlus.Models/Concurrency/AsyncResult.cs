using System;
using System.Threading.Tasks;

namespace ZeroPlus.Models.Concurrency
{
    public class AsyncResult<TResult> : AsyncResultNoResult
    {
        private TResult? _result;

        public AsyncResult(AsyncCallback? asyncCallback, object state)
          : base(asyncCallback, state)
        {
        }

        public new TResult? EndInvoke()
        {
            base.EndInvoke();
            return _result;
        }

        public new TResult? EndInvoke(int millisecondsTimeout)
        {
            base.EndInvoke(millisecondsTimeout);
            return _result;
        }

        public new async Task<TResult?> EndInvokeAsync()
        {
            await base.EndInvokeAsync();
            return _result;
        }

        public new async Task<TResult?> EndInvokeAsync(int millisecondsTimeout)
        {
            await base.EndInvokeAsync(millisecondsTimeout);
            return _result;
        }

        public void SetAsCompleted(TResult result, bool completedSynchronously)
        {
            _result = result;
            SetAsCompleted(null, completedSynchronously);
        }
    }
}