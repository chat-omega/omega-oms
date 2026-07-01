using System.Collections.Generic;
using System.Linq;

namespace ZeroPlus.Models.Data.Requests
{
    public class BasketOrderRequest
    {
        public List<BasketOrderRow> BasketOrderRows { get; set; }
        public string? Token { get; set; }
        public string? ClientOrderId { get; set; }

        public BasketOrderRequest()
        {
            BasketOrderRows = new List<BasketOrderRow>();
        }

        public override string ToString()
        {
            return $"Token: {Token}, OrderId: {ClientOrderId}, BasketOrderRowsCount: {BasketOrderRows?.Count ?? 0}, BasketOrderRows: {(BasketOrderRows != null ? string.Join('\n', BasketOrderRows.Select(x => x.ToString())) : "")}.";
        }
    }
}
