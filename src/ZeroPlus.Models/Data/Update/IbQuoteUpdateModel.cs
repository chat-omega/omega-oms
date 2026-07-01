namespace ZeroPlus.Models.Data.Update
{
    public class IbQuoteUpdateModel
    {
        public int TickerId { get; set; }
        public int BidSize { get; set; }
        public int AskSize { get; set; }
        public int LastSize { get; set; }
        public int Volume { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double Last { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Open { get; set; }
        public double Close { get; set; }
        public string? BidExch { get; set; }
        public string? AskExch { get; set; }
        public string? LastExch { get; set; }
        public string? Symbol { get; set; }
        public double ImpliedVolatility { get; set; }
        public double Delta { get; set; }
        public double OptPrice { get; set; }
        public double PvDividend { get; set; }
        public double Gamma { get; set; }
        public double Vega { get; set; }
        public double Theta { get; set; }
        public double UndPrice { get; set; }

        public void Update(IbQuoteUpdateModel model)
        {
            TickerId = model.TickerId;
            BidSize = model.BidSize;
            AskSize = model.AskSize;
            LastSize = model.LastSize;
            Volume = model.Volume;
            Bid = model.Bid;
            Ask = model.Ask;
            Last = model.Last;
            High = model.High;
            Low = model.Low;
            Open = model.Open;
            Close = model.Close;
            BidExch = model.BidExch;
            AskExch = model.AskExch;
            LastExch = model.LastExch;
            Symbol = model.Symbol;
            ImpliedVolatility = model.ImpliedVolatility;
            Delta = model.Delta;
            OptPrice = model.OptPrice;
            PvDividend = model.PvDividend;
            Gamma = model.Gamma;
            Vega = model.Vega;
            Theta = model.Theta;
            UndPrice = model.UndPrice;
        }
    }
}
