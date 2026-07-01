using ZeroPlus.Oms.Enums;

namespace ZeroPlus.Oms.Helper;

public static class InstanceModeExtensions
{
    public static bool IsAutoTraderInstance(this InstanceMode instanceMode)
    {
        switch (instanceMode)
        {
            case InstanceMode.AT_TB:
            case InstanceMode.AT_SILEXX:
            case InstanceMode.AT_ZPFIX:
                return true;
        }

        return false;
    }
}