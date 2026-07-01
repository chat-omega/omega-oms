
def exit_check(data_map: dict):
    # available indicators: Ema, PrevEma, Ema2, PrevEma2, Ema3, PrevEma3, Macd, PrevMacd, Signal, PrevSignal
    # available data:       Bid, PrevBid, Mid, PrevMid, Ask, PrevAsk, Delta, PrevDelta, Theo, PrevTheo
    # available pos info:   Position, WorkingQty, AvgBuyPx, AvgSellPx, TotalBuyQty, TotalSellQty, RealPnl, UnrealPnl
    prev_macd = data_map["PrevMacd"]
    macd = data_map["Macd"]
    pos = data_map["Position"]
    print(f"Checking for exit. Pos: {pos}, Evaluating MACD: {macd}, prev MACD {prev_macd}.")
    if pos > 0 and prev_macd > macd + 0.01:
        print("Sending SELL signal.")
        return "Sell"
    elif pos < 0 and macd > prev_macd + 0.01:
        print("Sending BUY signal.")
        return "Buy"
    else:
        return None