using System.Collections.Generic;

namespace ZeroPlus.Models.Data.Update
{
    public class RbboUpdateModel
    {
        public const int MaxSlots = 26;

        public int SymbolIndex { get; set; }
        public int SlotCount { get; set; }
        public RbboSlotModel[] Slots { get; } = new RbboSlotModel[MaxSlots];
        public uint KnownMcids { get; set; }
        public uint ChangedMcids { get; set; }

        public void UpdateSlot(int index, byte mcid, double bidPrice, uint bidQty, double askPrice, uint askQty, byte flags = 0)
        {
            Slots[index].Mcid = mcid;
            Slots[index].Flags = flags;
            Slots[index].BidPrice = bidPrice;
            Slots[index].BidQty = bidQty;
            Slots[index].AskPrice = askPrice;
            Slots[index].AskQty = askQty;
        }

        public IEnumerable<RbboSlotModel> GetActiveSlots()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                yield return Slots[i];
            }
        }
    }
}
