import math

def stoploss_check(data_map: dict):
    # available indicators: Ema, PrevEma, Ema2, PrevEma2, Ema3, PrevEma3, Macd, PrevMacd, Signal, PrevSignal
    # available data:       Bid, PrevBid, Mid, PrevMid, Ask, PrevAsk, Delta, PrevDelta, Theo, PrevTheo
    # available pos info:   Position, WorkingQty, AvgBuyPx, AvgSellPx, TotalBuyQty, TotalSellQty, RealPnl, UnrealPnl
    unreal_pnl = data_map["UnrealPnl"]
    pos = data_map["Position"]
    print(f"Checking for stoploss. Pos: {pos}, Unreal: {unreal_pnl}, prev MACD {prev_macd}.")
    if not math.isinf(unreal_pnl) and unreal_pnl < -1:
        if pos > 0:
            print("Sending SELL signal.")
            return "Sell"
        elif pos < 0:
            print("Sending BUY signal.")
            return "Buy"
    return None