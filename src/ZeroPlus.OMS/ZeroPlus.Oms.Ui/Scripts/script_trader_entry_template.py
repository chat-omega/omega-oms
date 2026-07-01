
def entry_check(data_map: dict):
    # available indicators: Ema, PrevEma, Ema2, PrevEma2, Ema3, PrevEma3, Macd, PrevMacd, Signal, PrevSignal
    # available data:       Bid, PrevBid, Mid, PrevMid, Ask, PrevAsk, Delta, PrevDelta, Theo, PrevTheo
    # available pos info:   Position, WorkingQty, AvgBuyPx, AvgSellPx, TotalBuyQty, TotalSellQty, RealPnl, UnrealPnl
    prev_macd = data_map["PrevMacd"]
    macd = data_map["Macd"]
    print(f"Checking for entry. Evaluating MACD: {macd}, prev MACD {prev_macd}.")
    if macd > 0 and prev_macd < 0:
        print("Sending BUY signal.")
        return "Buy"
    elif macd < 0 and prev_macd > 0:
        print("Sending SELL signal.")
        return "Sell"
    else:
        return None