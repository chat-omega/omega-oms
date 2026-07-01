using System;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Enums.Matrix;
using OrderStatus = ZeroPlus.Models.Data.Enums.OrderStatus;
using Side = ZeroPlus.Models.Data.Enums.Side;

namespace ZeroPlus.Models.Extensions
{
    public static class MatrixExtensions
    {
        public static OrderStatus FromMatrixOrderStatus(this Data.Enums.Matrix.OrderStatus matrixOrderStatus)
        {
            switch (matrixOrderStatus)
            {
                case Data.Enums.Matrix.OrderStatus.OPEN:
                case Data.Enums.Matrix.OrderStatus.ORDER_APPROVED:
                case Data.Enums.Matrix.OrderStatus.SENTORDER:
                    return OrderStatus.New;
                case Data.Enums.Matrix.OrderStatus.REJECT:
                case Data.Enums.Matrix.OrderStatus.ORDER_RESPONSE_REJECTED:
                case Data.Enums.Matrix.OrderStatus.CANCEL_REJECTED:
                case Data.Enums.Matrix.OrderStatus.CANCEL_RESPONSE_REJECTED:
                case Data.Enums.Matrix.OrderStatus.MODIFY_REJECTED:
                case Data.Enums.Matrix.OrderStatus.MODIFY_RESPONSE_REJECTED:
                    return OrderStatus.Rejected;
                case Data.Enums.Matrix.OrderStatus.FILL:
                    return OrderStatus.Filled;
                case Data.Enums.Matrix.OrderStatus.PARTIAL_FILL:
                    return OrderStatus.PartiallyFilled;
                case Data.Enums.Matrix.OrderStatus.CANCELED:
                    return OrderStatus.Canceled;
                case Data.Enums.Matrix.OrderStatus.INITIATED:
                case Data.Enums.Matrix.OrderStatus.ADD:
                case Data.Enums.Matrix.OrderStatus.ORDER_ACCEPTED:
                    return OrderStatus.PendingNew;
                case Data.Enums.Matrix.OrderStatus.CANCEL_INITIATED:
                case Data.Enums.Matrix.OrderStatus.CANCEL_ACCEPTED:
                case Data.Enums.Matrix.OrderStatus.CANCEL_SENT:
                case Data.Enums.Matrix.OrderStatus.CANCEL_WAITING:
                case Data.Enums.Matrix.OrderStatus.CANCEL_APPROVED:
                    return OrderStatus.PendingCancel;
                case Data.Enums.Matrix.OrderStatus.MODIFY_INITIATED:
                case Data.Enums.Matrix.OrderStatus.MODIFY_COMBINED:
                case Data.Enums.Matrix.OrderStatus.MODIFY_FAST:
                case Data.Enums.Matrix.OrderStatus.MODIFY_WAITING:
                case Data.Enums.Matrix.OrderStatus.MODIFY_SENT:
                case Data.Enums.Matrix.OrderStatus.MODIFY_PENDING:
                case Data.Enums.Matrix.OrderStatus.MODIFY_ACCEPTED:
                case Data.Enums.Matrix.OrderStatus.MODIFY_RECEIVED:
                case Data.Enums.Matrix.OrderStatus.MODIFY_APPROVED:
                    return OrderStatus.PendingReplace;
                case Data.Enums.Matrix.OrderStatus.STRATEGY_UPDATE:
                    return OrderStatus.Replaced;
                case Data.Enums.Matrix.OrderStatus.DONE_FOR_DAY:
                    return OrderStatus.DoneForDay;
                case Data.Enums.Matrix.OrderStatus.RESTATED:
                    return OrderStatus.Restated;
                case Data.Enums.Matrix.OrderStatus.FILL_CORRECTION:
                    return OrderStatus.Calculated;
                default:
                    throw new ArgumentOutOfRangeException(nameof(matrixOrderStatus), matrixOrderStatus, null);
            }
        }

        public static Side FromMatrixSide(this Data.Enums.Matrix.Side side)
        {
            switch (side)
            {
                case Data.Enums.Matrix.Side.Buy:
                    return Side.Buy;
                case Data.Enums.Matrix.Side.Sell:
                    return Side.Sell;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }
        }

        public static TimeInForce FromMatrixTif(this Tif tif)
        {
            switch (tif)
            {
                case Tif.DAY:
                    return TimeInForce.DAY;
                case Tif.GTC:
                    return TimeInForce.GTC;
                case Tif.OPG:
                    return TimeInForce.OPG;
                case Tif.IOC:
                    return TimeInForce.IOC;
                case Tif.FOK:
                    return TimeInForce.FOK;
                case Tif.GTX:
                    return TimeInForce.GTX;
                case Tif.GTD:
                    return TimeInForce.GTD;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tif), tif, null);
            }
        }
    }
}
