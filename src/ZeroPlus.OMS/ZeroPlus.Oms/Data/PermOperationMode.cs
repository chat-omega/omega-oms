using System;
using ZeroPlus.Models.Data.Enums;
namespace ZeroPlus.Oms.Data
{
    public class PermOperationMode
    {
        public PermMode[] PermModes { get; } = (PermMode[])Enum.GetValues(typeof(PermMode));
        public PermSide[] PermSides { get; } = (PermSide[])Enum.GetValues(typeof(PermSide));

        public int Count { get; set; }

        public PermMode PermMode { get; set; }
        public PermSide PermSide { get; set; }
        public bool MaintainBaseStrategy { get; set; }

        public PermOperationMode()
        {
            Count = 1;
            PermMode = PermMode.StrikeUp;
            PermSide = PermSide.All;
        }
    }
}
