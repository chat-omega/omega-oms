using System.Collections.Concurrent;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Oms.Ui.Models;

public class ExchangeSubmissionsSummaryModel : SubmissionsSummaryModel
{
    private readonly Dispatcher _dispatcher;
    private readonly ConcurrentDictionary<string, UnderlyingSubmissionsSummaryModel> _keyToModelMap = [];
    public BrokerSubmissionsSummaryModel Parent { get; set; }

    public ExchangeSubmissionsSummaryModel(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public TraderSubmissionsSummaryModel GetModel(SubmissionsSummary model)
    {
        if (model.Underlying == null)
        {
            return null;
        }

        if (!_keyToModelMap.TryGetValue(model.Underlying, out var child))
        {
            child = new UnderlyingSubmissionsSummaryModel(_dispatcher)
            {
                Name = model.Underlying,
                Parent = this,
            };
            _keyToModelMap[model.Underlying] = child;
            _dispatcher?.BeginInvoke(() => Breakdown.Add(child));
        }

        return child.GetModel(model);
    }

    public override void Update(SubmissionsSummary submissionsSummary)
    {
        TotalSubs = submissionsSummary.ExchangeTotalSubmissions;
        UniqueSubs = submissionsSummary.ExchangeUniqueSubmissions;
        Parent.Update(submissionsSummary);
    }
}