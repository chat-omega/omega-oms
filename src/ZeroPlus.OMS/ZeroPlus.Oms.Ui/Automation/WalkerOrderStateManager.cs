using System;

namespace ZeroPlus.Oms.Ui.Automation;

public class WalkerOrderStateManager
{
    public string LastOrderId { get; set; }
    public int IncrementCounter { get; set; }

    public Func<double> NextPriceCalculator { get; set; }
    public Func<double> StopPriceCalculator { get; set; }

    public int OrderResubmitCount { get; set; }
    public int OrderMaxResubmit { get; set; }

    internal PxCalculator PriceCalculator { get; }

    public WalkerOrderStateManager(PxCalculator priceCalculator)
    {
        PriceCalculator = priceCalculator;
    }

    public void Dispose()
    {
        LastOrderId = null;
        IncrementCounter = 0;
        OrderMaxResubmit = 0;
        OrderResubmitCount = 0;
        PriceCalculator?.Dispose();
    }
}