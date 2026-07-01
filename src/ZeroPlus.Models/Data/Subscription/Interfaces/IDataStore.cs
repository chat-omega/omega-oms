using System.Collections.Generic;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;

namespace ZeroPlus.Models.Data.Subscription.Interfaces
{
    public interface IDataStore
    {
        public void GetQuoteDataFor(List<Option> optionChain, SubscriptionFieldType type);
        public void GetQuoteDataFor(string symbol, SubscriptionFieldType type);
        public void GetEmaDataFor(List<Option> optionChain, SubscriptionFieldType type);
        public void GetEmaDataFor(string symbol, SubscriptionFieldType type);
        public void GetHanweckDataFor(List<Option> optionChain, SubscriptionFieldType type);
        public void GetHanweckDataFor(string symbol, SubscriptionFieldType type);
        public void GetRaptorDataFor(List<Option> optionChain, SubscriptionFieldType type);
        public void GetRaptorDataFor(string symbol, SubscriptionFieldType type);
        public void GetVolaDataFor(List<Option> optionChain, SubscriptionFieldType type);

        public ValueTask<double> GetDataAsync(string optionSymbol, bool approximateOnFailure = true);
        public void Reset();
    }
}