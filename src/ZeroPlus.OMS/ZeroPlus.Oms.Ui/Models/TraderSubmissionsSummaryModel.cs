using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Oms.Ui.Models;

public class TraderSubmissionsSummaryModel : SubmissionsSummaryModel
{
    public UnderlyingSubmissionsSummaryModel Parent { get; set; }

    public override void Update(SubmissionsSummary submissionsSummary)
    {
        TotalSubs = submissionsSummary.TraderTotalSubmissions;
        UniqueSubs = submissionsSummary.TraderUniqueSubmissions;
        Parent.Update(submissionsSummary);
    }
}