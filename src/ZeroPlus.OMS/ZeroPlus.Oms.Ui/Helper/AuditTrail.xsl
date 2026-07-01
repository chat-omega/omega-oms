<?xml version="1.0" encoding="ISO-8859-1"?>

<xsl:stylesheet version="1.0"
xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

  <xsl:template match="/">
    <html>
      <head>
        <style type="text/css">
          *
          {
          margin:5px;
          padding:0;
          border:0;
          }

          body	{
          background:#1e1e1e;
          color:#00cc00;
          font-size: 10px;
          font-family: Verdana, Arial, Helvetica, sans-serif;
          }
          #trant	{
          width:100%;
          font-size: 10px;
          font-family: Verdana, Arial, Helvetica, sans-serif;
          margin:0;
          padding:0;
          border:1px solid #555;
          border-collapse:collapse;
          }
          #ordt	{
          font-size: 10px;
          font-family: Verdana, Arial, Helvetica, sans-serif;
          margin:0;
          padding:0;
          border:1px solid #555;
          border-collapse:collapse;
          }
          th		{
          text-align:left;
          color:#ccc;
          background:#000;
          padding:2px;

          }
          td		{
          padding:2px;
          border-bottom:1px solid #000;
          }
          td.col	{
          background:#000;
          font-weight:bold;
          border-bottom:1px solid #222;
          }
          tr		{
          border:0;
          }
          #tbar	{
          display:block;
          width:100%;
          background:#000;
          margin:5px 0 5px 0;
          padding:2px;;
          font-size:12px;
          font-weight:bold;
          }

        </style>
      </head>
      <body>
        <table id="ordt">
          <xsl:for-each select="OrderTransactionHistory/Order">
            <tr>
              <td class="col">ID</td>
              <td>
                <xsl:value-of select="@ID"/>
              </td>
            </tr>
            <tr>
              <td class="col">Account</td>
              <td>
                <xsl:value-of select="@Account"/>
              </td>
            </tr>
            <tr>
              <td class="col">User</td>
              <td>
                <xsl:value-of select="@User"/>
              </td>
            </tr>
            <tr>
              <td class="col">Symbol</td>
              <td>
                <xsl:value-of select="@Symbol"/>
              </td>
            </tr>
            <tr>
              <td class="col">Route</td>
              <td>
                <xsl:value-of select="@Route"/>
              </td>
            </tr>
            <tr>
              <td class="col">Side</td>
              <td>
                <xsl:value-of select="@Side"/>
              </td>
            </tr>
            <tr>
              <td class="col">OrdType</td>
              <td>
                <xsl:value-of select="@OrdType"/>
              </td>
            </tr>
            <tr>
              <td class="col">TimeInForce</td>
              <td>
                <xsl:value-of select="@TimeInForce"/>
              </td>
            </tr>
            <tr>
              <td class="col">OrdStatus</td>
              <td>
                <xsl:value-of select="@OrdStatus"/>
              </td>
            </tr>
            <tr>
              <td class="col">Price</td>
              <td>
                <xsl:value-of select='format-number(@Price, "#,###.00##")'/>
              </td>
            </tr>
            <tr>
              <td class="col">LastPx</td>
              <td>
                <xsl:value-of select='format-number(@LastPx, "#,###.00##")'/>
              </td>
            </tr>
            <tr>
              <td class="col">AvgPx</td>
              <td>
                <xsl:value-of select='format-number(@AvgPx, "#,###.00####")'/>
              </td>
            </tr>
            <tr>
              <td class="col">Qty</td>
              <td>
                <xsl:value-of select='format-number(@Qty, "#,###")'/>
              </td>
            </tr>
            <tr>
              <td class="col">CumQty</td>
              <td>
                <xsl:value-of select='format-number(@CumQty, "#,###")'/>
              </td>
            </tr>
            <tr>
              <td class="col">LeavesQty</td>
              <td>
                <xsl:value-of select='format-number(@LeavesQty, "#,###")'/>
              </td>
            </tr>
            <tr>
              <td class="col">PositionEffect</td>
              <td>
                <xsl:value-of select="@PositionEffect"/>
              </td>
            </tr>
            <tr>
              <td class="col">Instructions</td>
              <td>
                <xsl:value-of select="@Instructions"/>
              </td>
            </tr>
            <tr>
              <td class="col">Comment</td>
              <td>
                <xsl:value-of select="@Comment"/>
              </td>
            </tr>
            <tr>
              <td class="col">Tag</td>
              <td>
                <xsl:value-of select="@Tag"/>
              </td>
            </tr>
            <tr>
              <td class="col">Source</td>
              <td>
                <xsl:value-of select="@Source"/>
              </td>
            </tr>
            <tr>
              <td class="col">RoutingSession</td>
              <td>
                <xsl:value-of select="@RoutingSession"/>
              </td>
            </tr>
            <tr>
              <td class="col">OrderAltId</td>
              <td>
                <xsl:for-each select="/OrderTransactionHistory/OrderUpdates/*">
                  <xsl:if test="local-name()='Transaction'">
                    <xsl:if test="position() = last()">
                      <xsl:value-of select="@OrderAltId"/>
                    </xsl:if>
                  </xsl:if>
                </xsl:for-each>
              </td>
            </tr>
            <tr>
              <td class="col">SubmitTime</td>
              <td>
                <xsl:value-of select="@SubmitTime"/>
              </td>
            </tr>
            <tr>
              <td class="col">LastUpdateTime</td>
              <td>
                <xsl:value-of select="@LastUpdateTime"/>
              </td>
            </tr>
          </xsl:for-each>
          <xsl:for-each select="OrderTransactionHistory/ComplexOrder">
            <tr>
              <td class="col">ID</td>
              <td>
                <xsl:value-of select="@ID"/>
              </td>
            </tr>
            <tr>
              <td class="col">Account</td>
              <td>
                <xsl:value-of select="@Account"/>
              </td>
            </tr>
            <tr>
              <td class="col">User</td>
              <td>
                <xsl:value-of select="@User"/>
              </td>
            </tr>
            <tr>
              <td class="col">Symbol</td>
              <td>
                <xsl:value-of select="@Symbol"/>
              </td>
            </tr>
            <tr>
              <td class="col">Route</td>
              <td>
                <xsl:value-of select="@Route"/>
              </td>
            </tr>
            <tr>
              <td class="col">OrdType</td>
              <td>
                <xsl:value-of select="@OrdType"/>
              </td>
            </tr>
            <tr>
              <td class="col">TimeInForce</td>
              <td>
                <xsl:value-of select="@TimeInForce"/>
              </td>
            </tr>
            <tr>
              <td class="col">OrdStatus</td>
              <td>
                <xsl:value-of select="@OrdStatus"/>
              </td>
            </tr>
            <tr>
              <td class="col">Price</td>
              <td>
                <xsl:value-of select='format-number(@Price, "#,###.00####")'/>
              </td>
            </tr>
            <tr>
              <td class="col">Qty</td>
              <td>
                <xsl:value-of select="@Qty"/>
              </td>
            </tr>
            <tr>
              <td class="col">CumQty</td>
              <td>
                <xsl:value-of select="@CumQty"/>
              </td>
            </tr>
            <tr>
              <td class="col">LeavesQty</td>
              <td>
                <xsl:value-of select="@LeavesQty"/>
              </td>
            </tr>
            <tr>
              <td class="col">PositionEffect</td>
              <td>
                <xsl:value-of select="@PositionEffect"/>
              </td>
            </tr>
            <tr>
              <td class="col">Instructions</td>
              <td>
                <xsl:value-of select="@Instructions"/>
              </td>
            </tr>
            <tr>
              <td class="col">Comment</td>
              <td>
                <xsl:value-of select="@Comment"/>
              </td>
            </tr>
            <tr>
              <td class="col">Tag</td>
              <td>
                <xsl:value-of select="@Tag"/>
              </td>
            </tr>
            <tr>
              <td class="col">Source</td>
              <td>
                <xsl:value-of select="@Source"/>
              </td>
            </tr>
            <tr>
              <td class="col">RoutingSession</td>
              <td>
                <xsl:value-of select="@RoutingSession"/>
              </td>
            </tr>
            <tr>
              <td class="col">OrderAltId</td>
              <td>
                <xsl:for-each select="/OrderTransactionHistory/OrderUpdates/*">
                  <xsl:if test="local-name()='Transaction'">
                    <xsl:if test="position() = last()">
                      <xsl:value-of select="@OrderAltId"/>
                    </xsl:if>
                  </xsl:if>
                </xsl:for-each>
              </td>
            </tr>
            <tr>
              <td class="col">SubmitTime</td>
              <td>
                <xsl:value-of select="@SubmitTime"/>
              </td>
            </tr>
            <tr>
              <td class="col">LastUpdateTime</td>
              <td>
                <xsl:value-of select="@LastUpdateTime"/>
              </td>
            </tr>
          </xsl:for-each>
        </table>
        <div id="tbar">Transactions</div>
        <table id="trant">
          <tr>
            <th>Type</th>
            <th>RequestID</th>
            <th>Username</th>
            <th>OrdStatus</th>
            <th>CurrentOrderID</th>
            <th>Exchange</th>
            <th>Price</th>
            <th>LastPx</th>
            <th>AvgPx</th>
            <th>SpreadAvgPx</th>
            <th>Qty</th>
            <th>LastQty</th>
            <th>CumQty</th>
            <th>LeavesQty</th>
            <th>Comment</th>
            <th>LastUpdateTime</th>
          </tr>
          <xsl:for-each select="OrderTransactionHistory/OrderUpdates/*">
            <xsl:if test="local-name()='Transaction'">
              <tr>
                <td>Transaction</td>
                <td>
                  <xsl:value-of select="@RequestID"/>
                </td>
                <td>
                  <xsl:value-of select="@Username"/>
                </td>
                <td>
                  <xsl:value-of select="@OrdStatus"/>
                </td>
                <td>
                  <xsl:value-of select="@CurrentOrderID"/>
                </td>
                <td>
                  <xsl:value-of select="@Exchange"/>
                </td>
                <td>
                  <xsl:value-of select='format-number(@Price, "#,###.00####")'/>
                </td>
                <td>
                  <xsl:value-of select='format-number(@LastPx, "#,###.00####")'/>
                </td>
                <td>
                  <xsl:value-of select='format-number(@AvgPx, "#,###.00####")'/>
                </td>
                <td>
                  <xsl:value-of select='format-number(@SpreadAvgPx, "#,###.00####")'/>
                </td>
                <td>
                  <xsl:value-of select='format-number(@Qty, "#,###")'/>
                </td>
                <td>
                  <xsl:value-of select='format-number(@LastQty, "#,###")'/>
                </td>
                <td>
                  <xsl:value-of select='format-number(@CumQty, "#,###")'/>
                </td>
                <td>
                  <xsl:value-of select='format-number(@LeavesQty, "#,###")'/>
                </td>
                <td>
                  <xsl:value-of select="@Comment"/>
                </td>
                <td>
                  <xsl:value-of select="@LastUpdateTime"/>
                </td>
              </tr>
            </xsl:if>
            <xsl:if test="local-name()='CancelReplaceRequest'">
              <tr>
                <td>Replace Request</td>
                <td>
                  <xsl:value-of select="@ID"/>
                </td>
                <td>
                  <xsl:value-of select="@Username"/>
                </td>
                <td></td>
                <td>
                  <xsl:value-of select="@CurrentOrderID"/>
                </td>
                <td></td>
                <td>
                  <xsl:value-of select='format-number(@Price, "#,###.00####")'/>
                </td>
                <td></td>
                <td></td>
                <td>
                  <xsl:value-of select='format-number(@Qty, "#,###")'/>
                </td>
                <td></td>
                <td></td>
                <td></td>
                <td></td>
                <td>
                  <xsl:value-of select="@LastUpdateTime"/>
                </td>
              </tr>
            </xsl:if>
            <xsl:if test="local-name()='CancelRequest'">
              <tr>
                <td>Cancel Request</td>
                <td>
                  <xsl:value-of select="@ID"/>
                </td>
                <td>
                  <xsl:value-of select="@Username"/>
                </td>
                <td></td>
                <td>
                  <xsl:value-of select="@CurrentOrderID"/>
                </td>
                <td></td>
                <td></td>
                <td></td>
                <td></td>
                <td></td>
                <td></td>
                <td></td>
                <td></td>
                <td></td>
                <td>
                  <xsl:value-of select="@LastUpdateTime"/>
                </td>
              </tr>
            </xsl:if>
            <xsl:if test="local-name()='CancelReject'">
              <tr>
                <td>Cancel Reject</td>
                <td>
                  <xsl:value-of select="@ID"/>
                </td>
                <td></td>
                <td></td>
                <td>
                  <xsl:value-of select="@CurrentOrderID"/>
                </td>
                <td></td>
                <td></td>
                <td></td>
                <td></td>
                <td></td>
                <td></td>
                <td></td>
                <td></td>
                <td>
                  <xsl:value-of select="@Comment"/>
                </td>
                <td>
                  <xsl:value-of select="@LastUpdateTime"/>
                </td>
              </tr>
            </xsl:if>
          </xsl:for-each>
        </table>
      </body>
    </html>
  </xsl:template>
</xsl:stylesheet>