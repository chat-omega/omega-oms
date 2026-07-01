using System;

namespace ZeroPlus.Oms.Clients;

public interface IOmsPositionSubscriber
{
    void SubscibedPositionUpdateValue(Tuple<string, string> key, object value);
}