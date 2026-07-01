using System;

namespace ZeroPlus.Models.Data.Update
{
    public interface ISpreadRiskModel
    {
        int Id { get; set; }
        int TotalOpen { get; set; }
        int TotalClose { get; set; }
        bool Action { get; set; }
        DateTime LastTradeTime { get; set; }
        string SpreadDescription { get; set; }
        string Underlying { get; set; }
        string Tags { get; set; }
    }
}