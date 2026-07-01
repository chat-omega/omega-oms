using System;

namespace ZeroPlus.Models.Data.Update;

public class CancelRisk : OrderRisk
{
    public DateTime SubmitTime { get; set; }
}