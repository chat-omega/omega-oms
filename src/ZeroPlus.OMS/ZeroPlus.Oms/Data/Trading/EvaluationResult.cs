namespace ZeroPlus.Oms.Data.Trading
{
    public class EvaluationResult
    {
        public string Description;
        public string GeneralDescription;
        public string BaseType;

        internal void Reset()
        {
            BaseType = "";
            GeneralDescription = "";
            Description = "";
        }
    }
}
