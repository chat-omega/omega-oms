using System;

namespace ZeroPlus.Models.Data.Models
{
    public class LiveContraPartyModel
    {
        public string PermId { get; set; } = string.Empty;
        public DateTime LastUpdateTime { get; set; }
        public int Value { get; set; }
        /// <summary>
        /// Defined by the enum ContraPropertyType
        /// </summary>
        public int Type { get; set; }
    }
}
