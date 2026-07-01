using System;

namespace ZeroPlus.Oms.Ui.Models
{
    public struct StrikeInfoModel : IComparable, IEquatable<StrikeInfoModel>
    {
        public bool IsUnique { get; set; }

        public double Strike { get; set; }

        public StrikeInfoModel(StrikeInfoModel strikeInfo)
        {
            IsUnique = strikeInfo.IsUnique;
            Strike = strikeInfo.Strike;
        }

        public StrikeInfoModel(bool isUnique, double strike)
        {
            IsUnique = isUnique;
            Strike = strike;
        }

        public StrikeInfoModel(double strike)
        {
            IsUnique = false;
            Strike = strike;
        }

        public override bool Equals(object obj)
        {
            return obj is StrikeInfoModel strikeInfo &&
                                          strikeInfo.Strike == Strike;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public int CompareTo(object obj)
        {
            if (obj is StrikeInfoModel strikeInfo)
            {
                return Strike.CompareTo(strikeInfo.Strike);
            }
            else
            {
                throw new ArgumentException();
            }
        }

        public bool Equals(StrikeInfoModel other)
        {
            return other.Strike == Strike;
        }

        public static bool operator ==(StrikeInfoModel a, StrikeInfoModel b)
        {
            return a.Strike == b.Strike;
        }

        public static bool operator !=(StrikeInfoModel a, StrikeInfoModel b)
        {
            return a.Strike != b.Strike;
        }

        public static bool operator >=(StrikeInfoModel a, StrikeInfoModel b)
        {
            return a.Strike >= b.Strike;
        }

        public static bool operator <=(StrikeInfoModel a, StrikeInfoModel b)
        {
            return a.Strike <= b.Strike;
        }

        public static bool operator >(StrikeInfoModel a, StrikeInfoModel b)
        {
            return a.Strike > b.Strike;
        }

        public static bool operator <(StrikeInfoModel a, StrikeInfoModel b)
        {
            return a.Strike < b.Strike;
        }

        public static bool operator ==(double strike, StrikeInfoModel b)
        {
            return strike == b.Strike;
        }

        public static bool operator !=(double strike, StrikeInfoModel b)
        {
            return strike != b.Strike;
        }

        public static bool operator >=(double strike, StrikeInfoModel b)
        {
            return strike >= b.Strike;
        }

        public static bool operator <=(double strike, StrikeInfoModel b)
        {
            return strike <= b.Strike;
        }

        public static bool operator >(double strike, StrikeInfoModel b)
        {
            return strike > b.Strike;
        }

        public static bool operator <(double strike, StrikeInfoModel b)
        {
            return strike < b.Strike;
        }

        public static bool operator ==(StrikeInfoModel a, double strike)
        {
            return a.Strike == strike;
        }

        public static bool operator !=(StrikeInfoModel a, double strike)
        {
            return a.Strike != strike;
        }

        public static bool operator >=(StrikeInfoModel a, double strike)
        {
            return a.Strike >= strike;
        }

        public static bool operator <=(StrikeInfoModel a, double strike)
        {
            return a.Strike <= strike;
        }

        public static bool operator >(StrikeInfoModel a, double strike)
        {
            return a.Strike > strike;
        }

        public static bool operator <(StrikeInfoModel a, double strike)
        {
            return a.Strike < strike;
        }

        public override string ToString()
        {
            return Strike.ToString();
        }
    }
}
