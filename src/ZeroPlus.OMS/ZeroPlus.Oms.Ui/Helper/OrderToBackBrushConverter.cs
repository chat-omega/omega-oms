using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(double), typeof(string))]
    public sealed class OrderToBackBrushConverter : IValueConverter
    {
        private readonly ColorTheme _colorTheme = OmsCore.Config.ColorTheme;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (parameter is string property)
                {
                    if (value is Side side)
                    {
                        switch (property.ToUpper())
                        {
                            case "FOREGROUND":
                                return side == ZeroPlus.Models.Data.Enums.Side.Buy
                                    ? _colorTheme.GreenColorFg
                                    : _colorTheme.RedColorFg;
                            case "BACKGROUND":
                                return side == ZeroPlus.Models.Data.Enums.Side.Buy
                                    ? _colorTheme.GreenColor
                                    : _colorTheme.RedColor;
                        }
                    }
                    else if (value is OmsOrderModel omsOrderModel)
                    {
                        switch (property.ToUpper())
                        {
                            case "FOREGROUND":
                                if (omsOrderModel.OrderStatus == OrderStatus.Canceled && omsOrderModel.FilledQty == 0)
                                {
                                    return omsOrderModel.IsComplexOrder
                                        ? omsOrderModel.Price >= 0 ? _colorTheme.GreenColor : _colorTheme.RedColor
                                        : omsOrderModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? _colorTheme.GreenColor : _colorTheme.RedColor;
                                }
                                if (omsOrderModel.OrderStatus == OrderStatus.PendingNew)
                                {
                                    return _colorTheme.PendingNewColorFg;
                                }
                                if (omsOrderModel.OrderStatus == OrderStatus.Rejected)
                                {
                                    return _colorTheme.BlueColorFg;
                                }
                                return omsOrderModel.IsComplexOrder
                                    ? omsOrderModel.Price >= 0 ? _colorTheme.GreenColorFg : _colorTheme.RedColorFg
                                    : omsOrderModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? _colorTheme.GreenColorFg : _colorTheme.RedColorFg;
                            case "BACKGROUND":
                                if (omsOrderModel.OrderStatus == OrderStatus.Canceled && omsOrderModel.FilledQty == 0)
                                {
                                    return _colorTheme.CanceledColor;
                                }
                                if (omsOrderModel.OrderStatus == OrderStatus.PendingNew)
                                {
                                    return _colorTheme.PendingNewColor;
                                }
                                return omsOrderModel.IsComplexOrder
                                    ? omsOrderModel.Price >= 0 ? _colorTheme.GreenColor : _colorTheme.RedColor
                                    : omsOrderModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? _colorTheme.GreenColor : _colorTheme.RedColor;
                            case "FOCUSED":
                            case "MOUSEOVER":
                                if (omsOrderModel.OrderStatus == OrderStatus.Canceled && omsOrderModel.FilledQty == 0)
                                {
                                    return _colorTheme.CanceledFocusedColor;
                                }
                                if (omsOrderModel.OrderStatus == OrderStatus.PendingNew)
                                {
                                    return _colorTheme.PendingNewFocusedColor;
                                }
                                return omsOrderModel.IsComplexOrder
                                    ? omsOrderModel.Price >= 0 ? _colorTheme.GreenFocusedColor : _colorTheme.RedFocusedColor
                                    : omsOrderModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? _colorTheme.GreenFocusedColor : _colorTheme.RedFocusedColor;
                        }
                    }
                    else if (value is OmsOrderUpdateModel orderUpdateModel)
                    {
                        switch (property.ToUpper())
                        {
                            case "FONTWEIGHT":
                                if (orderUpdateModel.OrderStatus is OrderStatus.New or OrderStatus.PartiallyFilled)
                                {
                                    return FontWeights.DemiBold;
                                }
                                return FontWeights.Normal;
                            case "FOREGROUND":
                                if (orderUpdateModel.OrderStatus == OrderStatus.Canceled)
                                {
                                    return orderUpdateModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? _colorTheme.GreenColor : _colorTheme.RedColor;
                                }
                                if (orderUpdateModel.OrderStatus == OrderStatus.Rejected)
                                {
                                    return _colorTheme.BlueColorFg;
                                }
                                if (orderUpdateModel.OrderStatus is OrderStatus.New or OrderStatus.PartiallyFilled)
                                {
                                    return _colorTheme.LightYellowColor;
                                }
                                if (orderUpdateModel.OrderStatus == OrderStatus.PendingNew)
                                {
                                    return _colorTheme.PendingNewColorFg;
                                }
                                return orderUpdateModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? _colorTheme.GreenColorFg : _colorTheme.RedColorFg;
                            case "BACKGROUND":
                                if (orderUpdateModel.OrderStatus == OrderStatus.Canceled)
                                {
                                    return _colorTheme.CanceledColor;
                                }
                                if (orderUpdateModel.OrderStatus == OrderStatus.PendingNew)
                                {
                                    return _colorTheme.PendingNewColor;
                                }
                                return orderUpdateModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? _colorTheme.GreenColor : _colorTheme.RedColor;
                            case "FOCUSED":
                            case "MOUSEOVER":
                                if (orderUpdateModel.OrderStatus == OrderStatus.Canceled)
                                {
                                    return _colorTheme.CanceledFocusedColor;
                                }
                                if (orderUpdateModel.OrderStatus == OrderStatus.PendingNew)
                                {
                                    return _colorTheme.PendingNewFocusedColor;
                                }
                                return orderUpdateModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? _colorTheme.GreenFocusedColor : _colorTheme.RedFocusedColor;
                        }
                    }
                    else if (value is OpraDatabaseTradeModel trade)
                    {
                        switch (property.ToUpper())
                        {
                            case "FOREGROUND":
                                if (trade.IsFirm)
                                {
                                    return _colorTheme.PendingNewColorFg;
                                }
                                return !double.IsNaN(trade.DeltaAdjTheo) ? trade.DeltaAdjTheo > trade.Price ? _colorTheme.GreenColorFg : _colorTheme.RedColorFg : trade.Price >= 0 ? _colorTheme.GreenColorFg : _colorTheme.RedColorFg;
                            case "BACKGROUND":
                                if (trade.IsFirm)
                                {
                                    return _colorTheme.PendingNewColor;
                                }
                                if (trade.ShowIndicator)
                                {
                                    return trade.BuyIndicator ? _colorTheme.GreenColorLight : _colorTheme.RedColorLight;
                                }
                                return !double.IsNaN(trade.DeltaAdjTheo) ? trade.DeltaAdjTheo > trade.Price ? _colorTheme.GreenColor : _colorTheme.RedColor : trade.Price >= 0 ? _colorTheme.GreenColor : _colorTheme.RedColor;
                            case "FOCUSED":
                            case "MOUSEOVER":
                                if (trade.IsFirm)
                                {
                                    return _colorTheme.PendingNewFocusedColor;
                                }
                                if (trade.ShowIndicator)
                                {
                                    return trade.BuyIndicator ? _colorTheme.GreenFocusedColorLight : _colorTheme.RedFocusedColorLight;
                                }
                                return !double.IsNaN(trade.DeltaAdjTheo) ? trade.DeltaAdjTheo > trade.Price ? _colorTheme.GreenFocusedColor : _colorTheme.RedFocusedColor : trade.Price >= 0 ? _colorTheme.GreenFocusedColor : _colorTheme.RedFocusedColor;
                        }
                    }
                    else if (value is OrderModel orderModel)
                    {
                        switch (property.ToUpper())
                        {
                            case "FOREGROUND":
                                return orderModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? _colorTheme.GreenColorFg : _colorTheme.RedColorFg;
                            case "BACKGROUND":
                                return orderModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? _colorTheme.GreenColor : _colorTheme.RedColor;
                            case "FOCUSED":
                            case "MOUSEOVER":
                                return orderModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? _colorTheme.GreenFocusedColor : _colorTheme.RedFocusedColor;
                        }
                    }
                    else if (value is PairOrderModel pairOrderModel)
                    {
                        switch (property.ToUpper())
                        {
                            case "FOREGROUND":
                                return pairOrderModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? _colorTheme.GreenColorFg : _colorTheme.RedColorFg;
                            case "BACKGROUND":
                                return _colorTheme.Transparent;
                            case "FOCUSED":
                            case "MOUSEOVER":
                                return _colorTheme.Transparent;
                        }
                    }
                    else if (value is PairOrderLegModel pairOrderLegModel)
                    {
                        switch (property.ToUpper())
                        {
                            case "FOREGROUND":
                                return pairOrderLegModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? _colorTheme.GreenColorFg : _colorTheme.RedColorFg;
                            case "BACKGROUND":
                                if (pairOrderLegModel.Side is ZeroPlus.Models.Data.Enums.Side.Buy or ZeroPlus.Models.Data.Enums.Side.BuyToCover)
                                {
                                    return _colorTheme.GreenColor;
                                }
                                return _colorTheme.RedColor;
                            case "FOCUSED":
                            case "MOUSEOVER":
                                if (pairOrderLegModel.Side is ZeroPlus.Models.Data.Enums.Side.Buy or ZeroPlus.Models.Data.Enums.Side.BuyToCover)
                                {
                                    return _colorTheme.GreenFocusedColor;
                                }
                                return _colorTheme.RedFocusedColor;
                        }
                    }
                    else if (value is EdgeScanFeedModel model)
                    {
                        switch (property.ToUpper())
                        {
                            case "FOREGROUND":
                                if (model.Uncertain || model.QtyMismatch)
                                {
                                    return model.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? _colorTheme.GreenColor : _colorTheme.RedColor;
                                }
                                if (model.IsFirm)
                                {
                                    return _colorTheme.PendingNewColorFg;
                                }
                                if (model.Uncertain || model.QtyMismatch)
                                {
                                    return _colorTheme.UncertainColorFg;
                                }
                                return model.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? _colorTheme.GreenColorFg : _colorTheme.RedColorFg;
                            case "BACKGROUND":
                                if (model.IsFirm)
                                {
                                    return _colorTheme.PendingNewColor;
                                }
                                if (model.PossibleFirm)
                                {
                                    return _colorTheme.PendingNewColorLight;
                                }
                                if (model.Uncertain || model.QtyMismatch)
                                {
                                    return _colorTheme.UncertainColor;
                                }
                                if (model.PossibleCopyCat)
                                {
                                    return _colorTheme.OrangeColor;
                                }
                                return model.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? _colorTheme.GreenColor : _colorTheme.RedColor;
                            case "FOCUSED":
                            case "MOUSEOVER":
                                if (model.IsFirm)
                                {
                                    return _colorTheme.PendingNewFocusedColor;
                                }
                                if (model.PossibleFirm)
                                {
                                    return _colorTheme.PendingNewFocusedColorLight;
                                }
                                if (model.Uncertain || model.QtyMismatch)
                                {
                                    return _colorTheme.UncertainFocusedColor;
                                }
                                if (model.PossibleCopyCat)
                                {
                                    return _colorTheme.OrangeFocusedColor;
                                }
                                return model.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? _colorTheme.GreenFocusedColor : _colorTheme.RedFocusedColor;
                        }
                    }
                    else if (value is TradeFeedModel tradeFeedModel)
                    {
                        switch (property.ToUpper())
                        {
                            case "FOREGROUND":
                                if (tradeFeedModel.IsFirm)
                                {
                                    return _colorTheme.PendingNewColorFg;
                                }
                                if (tradeFeedModel.IsCopyCat)
                                {
                                    return _colorTheme.WhiteColor;
                                }
                                if (tradeFeedModel.Side is ZeroPlus.Models.Data.Enums.Side.Buy or ZeroPlus.Models.Data.Enums.Side.BuyToCover)
                                {
                                    return _colorTheme.GreenColorFg;
                                }

                                return _colorTheme.RedColorFg;
                            case "BACKGROUND":
                                if (tradeFeedModel.IsFirm)
                                {
                                    return _colorTheme.PendingNewColor;
                                }
                                if (tradeFeedModel.IsCopyCat)
                                {
                                    return _colorTheme.OrangeColor;
                                }
                                if (tradeFeedModel.Side is ZeroPlus.Models.Data.Enums.Side.Buy or ZeroPlus.Models.Data.Enums.Side.BuyToCover)
                                {
                                    return _colorTheme.GreenColor;
                                }

                                return _colorTheme.RedColor;
                            case "FOCUSED":
                            case "MOUSEOVER":
                                if (tradeFeedModel.IsFirm)
                                {
                                    return _colorTheme.PendingNewFocusedColor;
                                }
                                if (tradeFeedModel.IsCopyCat)
                                {
                                    return _colorTheme.OrangeFocusedColor;
                                }
                                if (tradeFeedModel.Side is ZeroPlus.Models.Data.Enums.Side.Buy or ZeroPlus.Models.Data.Enums.Side.BuyToCover)
                                {
                                    return _colorTheme.GreenFocusedColor;
                                }
                                return _colorTheme.RedFocusedColor;
                        }
                    }
                    else if (value is IHaveSide haveSide)
                    {
                        switch (property.ToUpper())
                        {
                            case "FOREGROUND":
                                switch (haveSide.Side)
                                {
                                    case Side.Buy:
                                    case Side.BuyToCover:
                                        return _colorTheme.GreenColorFg;
                                    case Side.Sell:
                                    case Side.SellShort:
                                        return _colorTheme.RedColorFg;
                                    default:
                                        return _colorTheme.WhiteColor;
                                }
                            case "BACKGROUND":
                                switch (haveSide.Side)
                                {
                                    case Side.Buy:
                                    case Side.BuyToCover:
                                        return _colorTheme.GreenColor;
                                    case Side.Sell:
                                    case Side.SellShort:
                                        return _colorTheme.RedColor;
                                    default:
                                        return "#2d313b";
                                }
                            case "FOCUSED":
                                switch (haveSide.Side)
                                {
                                    case Side.Buy:
                                    case Side.BuyToCover:
                                        return _colorTheme.GreenFocusedColor;
                                    case Side.Sell:
                                    case Side.SellShort:
                                        return _colorTheme.RedFocusedColor;
                                    default:
                                        return "#3b404a";
                                }
                            case "MOUSEOVER":
                                switch (haveSide.Side)
                                {
                                    case Side.Buy:
                                    case Side.BuyToCover:
                                        return _colorTheme.GreenFocusedColorLight;
                                    case Side.Sell:
                                    case Side.SellShort:
                                        return _colorTheme.RedFocusedColorLight;
                                    default:
                                        return "#4e5563";
                                }
                        }
                    }
                    else
                    {
                        switch (property.ToUpper())
                        {
                            case "FOREGROUND":
                                return _colorTheme.WhiteColor;
                            case "BACKGROUND":
                                return _colorTheme.Transparent;
                        }
                    }
                }
                return _colorTheme.Transparent;
            }
            catch (Exception)
            {
                return _colorTheme.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
