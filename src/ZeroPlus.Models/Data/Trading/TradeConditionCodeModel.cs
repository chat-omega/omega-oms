
namespace ZeroPlus.Models.Data.Trading
{
    public class TradeConditionCodeModel
    {
        [Newtonsoft.Json.JsonProperty]
        public char Code { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public string Description { get; set; }

        public TradeConditionCodeModel(char code, string description)
        {
            Code = code;
            Description = description;
        }
    }
}
