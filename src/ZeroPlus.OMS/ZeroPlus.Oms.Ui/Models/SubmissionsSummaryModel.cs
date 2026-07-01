using System.Collections.ObjectModel;
using DevExpress.Mvvm;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Oms.Ui.Models;

public abstract partial class SubmissionsSummaryModel : BindableBase
{

    [Bindable]
    public partial string Name { get; set; }
    [Bindable]
    public partial uint TotalSubs { get; set; }
    [Bindable]
    public partial uint UniqueSubs { get; set; }

    public ObservableCollection<SubmissionsSummaryModel> Breakdown { get; set; } = [];

    public abstract void Update(SubmissionsSummary submissionsSummary);
}