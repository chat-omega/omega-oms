using System.Collections.Generic;

namespace ZeroPlus.Models.Data.Trading.Interfaces;

public interface IComplexOrderSlim : IOrderSlim
{
    HashSet<IComplexOrderLeg> Legs { get; set; }
    IComplexOrderLeg GetLeg(string legId);
}