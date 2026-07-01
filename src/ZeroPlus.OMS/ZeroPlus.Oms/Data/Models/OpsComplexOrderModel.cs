using System.Collections.Generic;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Oms.Data.Models;

public class OpsComplexOrderModel : OpsOrderModel, IComplexOrderSlim
{
    public new HashSet<IComplexOrderLeg> Legs { get; set; }

    public OpsComplexOrderModel()
    {
        IsComplexOrder = true;
        Legs = [];
    }

    public IComplexOrderLeg GetLeg(string legId)
    {
        return default!;
    }
}