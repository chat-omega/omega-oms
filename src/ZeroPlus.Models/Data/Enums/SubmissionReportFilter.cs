namespace ZeroPlus.Models.Data.Enums
{
    /// <summary>
    /// Controls which EdgeScanFeedRunner update stages are reported back to the client.
    /// </summary>
    public enum SubmissionReportFilter
    {
        /// <summary>Every stage (guard/submission/fill/cancel) is reported.</summary>
        AllUpdates = 0,

        /// <summary>Only fill-stage updates are reported; guard/submission/cancel are suppressed.</summary>
        FillsOnly = 1
    }
}
