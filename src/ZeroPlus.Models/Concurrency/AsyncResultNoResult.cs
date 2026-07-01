using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Models.Extensions;

namespace ZeroPlus.Models.Concurrency
{
    public class AsyncResultNoResult : IAsyncResult
    {
        private const int _stateCompletedAsynchronously = 2;
        private const int _stateCompletedSynchronously = 1;
        private const int _statePending = 0;
        private readonly object _asyncState;
        private readonly AsyncCallback? _asyncCallback;
        private ManualResetEvent? _asyncWaitHandle;
        private Exception? _exception;
        private int _completedState;

        public object AsyncState => _asyncState;
        public bool CompletedSynchronously => _completedState == _stateCompletedSynchronously;
        public bool IsCompleted => (uint)_completedState > 0U;

        public WaitHandle AsyncWaitHandle
        {
            get
            {
                _asyncWaitHandle ??= new ManualResetEvent(IsCompleted);
                return _asyncWaitHandle;
            }
        }

        public AsyncResultNoResult(AsyncCallback? asyncCallback, object state)
        {
            _asyncCallback = asyncCallback;
            _asyncState = state;
        }

        public async Task EndInvokeAsync()
        {
            if (!IsCompleted)
            {
                await AsyncWaitHandle.WaitOneAsync();
                AsyncWaitHandle.Close();
                _asyncWaitHandle = null;
            }
            if (_exception != null)
            {
                throw _exception;
            }
        }

        public async Task EndInvokeAsync(int millisecondsTimeout)
        {
            if (!IsCompleted)
            {
                await AsyncWaitHandle.WaitOneAsync(millisecondsTimeout);
                AsyncWaitHandle.Close();
                _asyncWaitHandle = null;
            }
            if (_exception != null)
            {
                throw _exception;
            }
        }

        public void EndInvoke()
        {
            if (!IsCompleted)
            {
                AsyncWaitHandle.WaitOne();
                AsyncWaitHandle.Close();
                _asyncWaitHandle = null;
            }
            if (_exception != null)
            {
                throw _exception;
            }
        }

        public void EndInvoke(int millisecondsTimeout)
        {
            if (!IsCompleted)
            {
                AsyncWaitHandle.WaitOne(millisecondsTimeout, false);
                AsyncWaitHandle.Close();
                _asyncWaitHandle = null;
            }
            if (_exception != null)
            {
                throw _exception;
            }
        }

        public void SetAsCompleted(Exception? exception, bool completedSynchronously)
        {
            _exception = exception;
            if (Interlocked.Exchange(ref _completedState, completedSynchronously ? _stateCompletedSynchronously : _stateCompletedAsynchronously) != _statePending)
            {
                throw new InvalidOperationException("You can set a result only once");
            }

            _asyncWaitHandle?.Set();
            _asyncCallback?.Invoke(this);
        }
    }
}
