using System.Collections.Concurrent;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Data.Trading
{
    public class ComplexOrder : Order, IComplexOrder
    {
        private readonly object _lock;
        private readonly ConcurrentDictionary<int, IComplexOrderLeg> _legIdToLegMap = new ConcurrentDictionary<int, IComplexOrderLeg>();
        public HashSet<IComplexOrderLeg> Legs { get; set; }

        public ComplexOrder()
        {
            _lock = new object();
            Legs = new HashSet<IComplexOrderLeg>();
            IsComplexOrder = true;
        }

        public ComplexOrder(ISecurityBook? securityBook) : base(securityBook)
        {
            _lock = new object();
            Legs = new HashSet<IComplexOrderLeg>();
            IsComplexOrder = true;
        }

        public IComplexOrderLeg GetLeg(string legId)
        {
            if (string.IsNullOrWhiteSpace(legId))
            {
                legId = "";
            }
            int id = GetHashCode() + legId.GetHashCode();
            if (!_legIdToLegMap.TryGetValue(id, out IComplexOrderLeg? orderLeg))
            {
                orderLeg = new ComplexOrderLeg(SecurityBook);
                _legIdToLegMap[id] = orderLeg;
                lock (_lock)
                {
                    Legs.Add(orderLeg);
                }
            }
            return orderLeg;
        }
    }
}
