using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroPlus.Oms.Subscription;

internal class DataResult
{
    private double _value = double.NaN;
    private readonly int _timeout;
    private readonly bool _useTimeout;
    private readonly CancellationToken _token;

    public bool ValueIsSet;

    public double Value
    {
        get => _value;
        set
        {
            _value = value;
            ValueIsSet = true;
        }
    }

    public DataResult(CancellationToken token, int timeout, bool useTimeout)
    {
        _token = token;
        _timeout = timeout;
        _useTimeout = useTimeout;
    }

    public bool ValueReceived()
    {
        return ValueIsSet;
    }

    public async Task WaitForValueAsync()
    {
        if (!ValueIsSet)
        {
            if (_useTimeout)
            {
                var delay = 100;
                var count = Math.Max(1, _timeout / delay);
                for (int i = 0; i < count; i++)
                {
                    await Task.Delay(delay, _token);
                    if (ValueIsSet)
                    {
                        break;
                    }
                }
            }

            ValueIsSet = true;
        }
    }
}