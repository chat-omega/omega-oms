namespace ZeroPlus.Oms.Ui.Models
{
    public class DataBentoHdModel
    {
        public string ts_event { get; set; }
        public int rtype { get; set; }
        public int publisher_id { get; set; }
        public int instrument_id { get; set; }
        public string open { get; set; }
        public string high { get; set; }
        public string low { get; set; }
        public string close { get; set; }
        public string volume { get; set; }
        public string symbol { get; set; }
    }

}
