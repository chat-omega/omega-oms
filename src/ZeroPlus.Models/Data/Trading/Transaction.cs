using System;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;

namespace ZeroPlus.Models.Data.Trading
{
    public class Transaction
    {
        public Security? Security { get; }
        public DateTime UpdateTime { get; }
        public Venue? Venue { get; }
        public string? Account { get; }
        public string? OrderId { get; }
        public string? Description { get; }
        public string? Underlying { get; }
        public string? Symbol { get; }
        public string? Trader { get; }
        public string? Login { get; }
        public string? Comment { get; }
        public double Delta { get; }
        public double Price { get; }
        public double Fees { get; }
        public Side Side { get; }
        public int OrderQty { get; }
        public int Qty { get; }
        public int Contracts { get; }
        public int StockContracts { get; }
        public ExecutionType ExecutionType { get; }
        public double NominalPrice => Multiplier * Qty * Price * (Side == Side.Buy || Side == Side.BuyToCover ? 1 : -1);
        public double Multiplier { init; get; }
        public SessionRoute SessionRoute { get; }

        public Transaction(Security? security,
                           DateTime updateTime,
                           Venue? venue,
                           string? account,
                           string? orderId,
                           string? description,
                           string? underlying,
                           string? symbol,
                           string? trader,
                           string? login,
                           string? comment,
                           double delta,
                           double price,
                           double fees,
                           Side side,
                           int orderQty,
                           int qty,
                           int contracts,
                           int stockContracts,
                           ExecutionType executionType,
                           SessionRoute sessionRoute = SessionRoute.Default)
        {
            Security = security;
            if (Security is not null) Multiplier = Security!.Multiplier;
            UpdateTime = updateTime;
            OrderId = orderId;
            Description = description;
            Underlying = underlying;
            Symbol = symbol;
            Trader = trader;
            Login = login;
            Comment = comment;
            Delta = delta;
            Price = price;
            Fees = fees;
            Side = side;
            OrderQty = orderQty;
            Qty = qty;
            Contracts = contracts;
            StockContracts = stockContracts;
            ExecutionType = executionType;
            Venue = venue;
            Account = account;
            SessionRoute = sessionRoute;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Security, UpdateTime, Account, OrderId, Symbol, Price, Qty, Side);
        }
    }
}
