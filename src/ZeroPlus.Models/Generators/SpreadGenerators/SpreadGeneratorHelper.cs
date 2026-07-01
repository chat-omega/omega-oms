using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Exceptions;

namespace ZeroPlus.Models.Generators.SpreadGenerators
{
    internal class SpreadGeneratorHelper
    {
        internal static List<List<SpreadHolder>> DistibuteWithDynamicQuota(List<List<SpreadHolder>> results, int count, CancellationToken token)
        {
            List<List<SpreadHolder>> finalResults = new();
            int groupQuota = (int)Math.Ceiling((double)count / results.Count);

            while (results.Count > 0)
            {
                token.ThrowIfCancellationRequested();

                List<List<SpreadHolder>> resultsThatFellShortOfQuota = results.Where(x => x.Count <= groupQuota).ToList();

                foreach (List<SpreadHolder> result in resultsThatFellShortOfQuota)
                {
                    finalResults.Add(result);
                    results.Remove(result);
                }

                int missing = resultsThatFellShortOfQuota.Sum(x => groupQuota - x.Count);
                if (results.Count == 0)
                {
                    break;
                }
                else if (missing > 0)
                {
                    groupQuota += missing / results.Count;
                }
                else
                {
                    for (int i = 0; i < results.Count; i++)
                    {
                        finalResults.Add(SelectSpreadsEvenly(results[i].ToArray(), groupQuota, token).ToList());
                    }
                    break;
                }
            }

            return finalResults;
        }

        internal static SpreadHolder[] SelectSpreadsEvenly(SpreadHolder[] spreads, int count, CancellationToken token)
        {
            if (count < 1)
            {
                throw new SlimException("Invalid count");
            }

            double step = count == 1 ? 1 : (spreads.Length - 1) / (double)(count - 1);

            SpreadHolder[] selected = new SpreadHolder[count];

            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                int index = (int)Math.Round(step * i, 0);
                selected[i] = spreads[index];
            }

            return selected;
        }
    }
}
