using System;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Oms.Ui.Models
{
    public record ImpliedQuoteModel
    {
        public double Price { get; set; }
        public double Theo { get; set; }
        public double DeltaMovement { get; set; }
        public double NonDeltaMovement { get; set; }
        public DateTime Timestamp { get; set; }

        public ImpliedQuoteModel()
        {
            Price = double.NaN;
            Theo = double.NaN;
            DeltaMovement = double.NaN;
            NonDeltaMovement = double.NaN;
            Timestamp = default;
        }

        public ImpliedQuoteModel(DerivedValueUpdateModel update, bool isBid)
        {
            if (isBid)
            {
                Price = update.ImpliedBidRecord;
                Theo = update.ImpliedBidRecordTheo;
                DeltaMovement = update.ImpliedBidRecordTheoMovement;
                NonDeltaMovement = update.ImpliedBidRecordNonDeltaMovement;
                Timestamp = update.ImpliedBidRecordTimestamp;
            }
            else
            {
                Price = update.ImpliedAskRecord;
                Theo = update.ImpliedAskRecordTheo;
                DeltaMovement = update.ImpliedAskRecordTheoMovement;
                NonDeltaMovement = update.ImpliedAskRecordNonDeltaMovement;
                Timestamp = update.ImpliedAskRecordTimestamp;
            }
        }
    }
}
