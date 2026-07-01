using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Models
{
    public struct HardSideKey
    {
        private string _key;

        public string Underlying { get; set; }
        public int ExpirationKey { get; set; }
        public BaseStrategy BaseStrategy { get; set; }

        public override string ToString()
        {
            _key ??= Underlying + ExpirationKey + BaseStrategy;
            return _key;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Underlying, ExpirationKey, BaseStrategy);
        }
    }
}
