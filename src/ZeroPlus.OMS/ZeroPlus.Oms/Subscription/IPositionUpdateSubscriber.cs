using System.Collections.Generic;
using ZeroPlus.Oms.Data.Trading;

namespace ZeroPlus.Oms.Subscription
{
    public interface IPositionUpdateSubscriber
    {
        void AddMultipleUpdatedPosition(IEnumerable<OmsPosition> positions);
        void AddUpdatedPosition(OmsPosition position);
    }
}