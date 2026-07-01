using System.Collections.Generic;

namespace ZeroPlus.Models.Data.Trading.Interfaces
{
    public interface IComplexOrder : IOrder, IComplexOrderSlim
    {
        HashSet<IComplexOrderLeg> Legs { get; set; }

        IComplexOrderLeg GetLeg(string legId);
    }
}