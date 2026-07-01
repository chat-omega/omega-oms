using System.Collections.Concurrent;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Oms.Ui.Models;

public class UnderlyingSubmissionsSummaryModel : SubmissionsSummaryModel
{
    private readonly Dispatcher _dispatcher;
    private readonly ConcurrentDictionary<string, TraderSubmissionsSummaryModel> _keyToModelMap = [];

    public ExchangeSubmissionsSummaryModel Parent { get; set; }

    public UnderlyingSubmissionsSummaryModel(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public TraderSubmissionsSummaryModel GetModel(SubmissionsSummary model)
    {
        if (model.Trader == null)
        {
            return null;
        }

        if (!_keyToModelMap.TryGetValue(model.Trader, out var child))
        {
            child = new TraderSubmissionsSummaryModel
            {
                Name = model.Trader,
                Parent = this,
            };
            _keyToModelMap[model.Trader] = child;
            _dispatcher?.BeginInvoke(() => Breakdown.Add(child));
        }

        return child;
    }

    public override void Update(SubmissionsSummary submissionsSummary)
    {
        TotalSubs = submissionsSummary.UnderlyingTotalSubmissions;
        UniqueSubs = submissionsSummary.UnderlyingUniqueSubmissions;
        Parent.Update(submissionsSummary);
    }
}