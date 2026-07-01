using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Requests
{
    public class PairOrderRequest : IHaveRisk
    {
        public PairOrderRequestType PairOrderRequestType { get; set; }
        public string? Account { get; set; }
        public string? Route { get; set; }
        public string? Tag { get; set; }
        public string? ClientOrderId { get; set; }
        public string? ClientOrderIdLeg1 { get; set; }
        public string? ClientOrderIdLeg2 { get; set; }
        public string? Locate { get; set; }
        public string? Style { get; set; }
        public string? TriggerMethod { get; set; }
        public string? TriggerValueCurrency { get; set; }
        public InitSide InitSide { get; set; }
        public double TriggerValue { get; set; }
        public double BuyTermsRatio { get; set; }
        public double SellTermsRatio { get; set; }
        public bool Staged { get; set; }
        public bool ClaimRequire { get; set; }
        public string? Leg1Symbol { get; set; }
        public Side Leg1Side { get; set; }
        public int Leg1Quantity { get; set; }
        public string? Leg2Symbol { get; set; }
        public Side Leg2Side { get; set; }
        public int Leg2Quantity { get; set; }
        public OrderType OrderType { get; set; }
        public TimeInForce TimeInForce { get; set; }
        public uint UserId { get; set; }
        public uint RiskCheckId { get; set; }
        public bool RiskCheckPassed { get; set; }
        public string? RiskCheckMessage { get; set; }

        public override string ToString()
        {
            return $"{nameof(Account)}: {Account}, " +
                   $"{nameof(Route)}: {Route}, " +
                   $"{nameof(PairOrderRequestType)}: {PairOrderRequestType}, " +
                   $"{nameof(Tag)}: {Tag}, " +
                   $"{nameof(ClientOrderId)}: {ClientOrderId}, " +
                   $"{nameof(ClientOrderIdLeg1)}: {ClientOrderIdLeg1}, " +
                   $"{nameof(ClientOrderIdLeg2)}: {ClientOrderIdLeg2}, " +
                   $"{nameof(Locate)}: {Locate}, " +
                   $"{nameof(Staged)}: {Staged}, " +
                   $"{nameof(ClaimRequire)}: {ClaimRequire}, " +
                   $"{nameof(Leg1Symbol)}: {Leg1Symbol}," +
                   $"{nameof(Leg1Side)}: {Leg1Side}, " +
                   $"{nameof(Leg1Quantity)}: {Leg1Quantity}, " +
                   $"{nameof(Leg2Symbol)}: {Leg2Symbol}, " +
                   $"{nameof(Leg2Side)}: {Leg2Side}, " +
                   $"{nameof(Leg2Quantity)}: {Leg2Quantity}, " +
                   $"{nameof(TriggerValue)}: {TriggerValue}, " +
                   $"{nameof(OrderType)}: {OrderType}.";
        }
    }
}
