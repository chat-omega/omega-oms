using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Automation;

/// <summary>
/// Manages the automated closing of trading positions through configurable price-based strategies.
/// </summary>
public interface ICloser : IDisposable
{
    /// <summary>
    /// Indicates whether the closer is currently enabled based on trading configuration and manual override settings.
    /// </summary>
    bool Enabled { get; }
    /// <summary>
    /// Controls whether the closer operates in manual mode, bypassing automated trading rules.
    /// </summary>
    bool Manual { get; set; }

    /// <summary>
    /// Continues the closing process by adjusting the order price based on configured increments.
    /// </summary>
    /// <param name="qty">Optional quantity to close. If 0, uses the existing close quantity.</param>
    /// <returns>True if the closing order was submitted successfully, false if stopped due to price limits or disposal.</returns>
    bool ContClose(int qty = 0);

    /// <summary>
    /// Initiates the position closing process with specified parameters.
    /// </summary>
    /// <param name="lastFillPx">Price of the last fill that triggered this closing process</param>
    /// <param name="qty">Quantity to close</param>
    /// <param name="closingEdge">Price adjustment from the last fill price for the initial closing order</param>
    /// <param name="closeMaxLoss">Maximum acceptable loss before stopping the closing process</param>
    /// <param name="priceIncrement">Minimum price movement for subsequent closing attempts</param>
    /// <param name="closeInterval">Time in milliseconds before canceling and adjusting unfilled closing orders</param>
    /// <param name="manualClose">Whether this closing process was manually initiated</param>
    /// <param name="type">Custom identifier for the closing process type. Defaults to system-determined values if empty.</param>  
    void StartCloser(
        double lastFillPx,
        int qty,
        double closingEdge,
        double closeMaxLoss,
        double priceIncrement,
        int closeInterval,
        bool manualClose = false,
        OrderSubType? type = null);

    /// <summary>
    /// Stops the closing process, canceling any pending orders unless configured to leave them resting.
    /// </summary>
    void Stop();
}