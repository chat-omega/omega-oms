using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Utils;
using ZeroPlus.Oms.Clients;
using OmsOption = ZeroPlus.Oms.Data.Securities.Option;
using OmsOptionType = ZeroPlus.Oms.Data.Securities.OptionType;
using ModelsOption = ZeroPlus.Models.Data.Securities.Option;
using ModelsSecurityType = ZeroPlus.Models.Data.Enums.SecurityType;

namespace ZeroPlus.Oms.Ui.Models
{
    internal static class PermutationAdapter
    {
        public static readonly PermutationTreeCache TreeCache = new();

        public static async Task<IEnumerable<ModelsOption>> BuildModelsChainAsync(QuoteClient quoteClient, string underlying, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            if (quoteClient == null || string.IsNullOrWhiteSpace(underlying))
            {
                return null;
            }

            List<OmsOption> chain = await quoteClient.GetSymbols(underlying);
            if (chain == null || chain.Count == 0)
            {
                return null;
            }

            List<ModelsOption> converted = new(chain.Count);
            for (int i = 0; i < chain.Count; i++)
            {
                OmsOption omsOption = chain[i];
                if (omsOption == null) continue;
                ModelsOption modelsOption = ToModelsOption(omsOption);
                if (modelsOption == null) continue;
                converted.Add(modelsOption);
            }
            return converted;
        }

        private static ModelsOption ToModelsOption(OmsOption omsOption)
        {
            if (omsOption == null) return null;

            return new ModelsOption
            {
                Symbol = omsOption.OptionSymbol ?? string.Empty,
                RootSymbol = omsOption.RootSymbol,
                Expiration = omsOption.Expiration,
                PutCall = omsOption.Type == OmsOptionType.CALL ? PutCall.Call : PutCall.Put,
                Strike = omsOption.Strike,
                MinimumTick = omsOption.MinimumTick,
                Multiplier = omsOption.Multiplier == 0 ? 100 : omsOption.Multiplier,
                SecurityType = ModelsSecurityType.Option,
                Underlying = new Security
                {
                    Symbol = omsOption.UnderlyingSymbol ?? string.Empty,
                    SecurityType = ModelsSecurityType.Stock,
                },
            };
        }
    }
}
