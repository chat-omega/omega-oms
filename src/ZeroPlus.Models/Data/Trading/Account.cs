using System.Collections.Generic;
using ZeroPlus.Models.Data.Models;

namespace ZeroPlus.Models.Data.Trading
{
    public class Account
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public string? Acronym { get; set; }
        public List<OrderRoutingInfoModel> Routes { get; } = new();

        public override string ToString()
        {
            return $"Id: {Id}, Acronym: {Acronym}, Routes: {string.Join("|", Routes)}.";
        }
    }
}
