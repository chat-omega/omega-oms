using System.Collections.Concurrent;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Oms.Ui.Models;

public class BrokerSubmissionsSummaryModel : SubmissionsSummaryModel
{
    private readonly Dispatcher _dispatcher;
    private readonly ConcurrentDictionary<Exchange, ExchangeSubmissionsSummaryModel> _keyToModelMap = [];

    public BrokerSubmissionsSummaryModel(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public TraderSubmissionsSummaryModel GetModel(SubmissionsSummary model)
    {
        if (!_keyToModelMap.TryGetValue(model.Exchange, out var child))
        {
            child = new ExchangeSubmissionsSummaryModel(_dispatcher)
            {
                Name = model.Exchange.ToString(),
                Parent = this,
            };
            _keyToModelMap[model.Exchange] = child;
            _dispatcher?.BeginInvoke(() => Breakdown.Add(child));
        }

        return child.GetModel(model);
    }

    public override void Update(SubmissionsSummary submissionsSummary)
    {
        TotalSubs = submissionsSummary.BrokerTotalSubmissions;
        UniqueSubs = submissionsSummary.BrokerUniqueSubmissions;
    }
}