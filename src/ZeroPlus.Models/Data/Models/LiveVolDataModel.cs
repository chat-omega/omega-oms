using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZeroPlus.Models.Data.Models
{
    public record LiveVolDataModel
    {
        [Column("Upload_Timestamp")]
        public DateTimeOffset UploadTimestamp { get; set; }

        [Column("Symbol")]
        public string Symbol { get; set; } = string.Empty;

        [Column("52_Week_High")]
        public double? Week52High { get; set; }

        [Column("52_Week_Low")]
        public double? Week52Low { get; set; }

        [Column("Close_Price")]
        public double? ClosePrice { get; set; }

        [Column("High_Price")]
        public double? HighPrice { get; set; }

        [Column("Last_Price")]
        public double? LastPrice { get; set; }

        [Column("Low_Price")]
        public double? LowPrice { get; set; }

        [Column("Open_Price")]
        public double? OpenPrice { get; set; }

        [Column("Percent_Change_from_Close")]
        public double? PercentChangeFromClose { get; set; }

        [Column("Percent_Change_from_Open")]
        public double? PercentChangeFromOpen { get; set; }

        [Column("Price_Change_From_Close")]
        public double? PriceChangeFromClose { get; set; }

        [Column("Price_Change_from_Open")]
        public double? PriceChangeFromOpen { get; set; }

        [Column("Price_Percent_of_52_Week_Range")]
        public double? PricePercentOf52WeekRange { get; set; }

        [Column("Price_Percentile_Rank")]
        public double? PricePercentileRank { get; set; }

        [Column("SD_Change_From_Close")]
        public double? SdChangeFromClose { get; set; }

        [Column("Average_IV30")]
        public double? AverageIv30 { get; set; }

        [Column("Expiry1_IV")]
        public double? Expiry1Iv { get; set; }

        [Column("Expiry1_IV_1_Day_Close")]
        public double? Expiry1Iv1DayClose { get; set; }

        [Column("Expiry1_IV_1_Week_Close")]
        public double? Expiry1Iv1WeekClose { get; set; }

        [Column("Expiry1_IV_Change")]
        public double? Expiry1IvChange { get; set; }

        [Column("Expiry1_IV_Percentage_Change")]
        public double? Expiry1IvPercentageChange { get; set; }

        [Column("Expiry2_IV")]
        public double? Expiry2Iv { get; set; }

        [Column("Expiry2_IV_1_Day_Close")]
        public double? Expiry2Iv1DayClose { get; set; }

        [Column("Expiry2_IV_1_Week_Close")]
        public double? Expiry2Iv1WeekClose { get; set; }

        [Column("Expiry2_IV_Change")]
        public double? Expiry2IvChange { get; set; }

        [Column("Expiry2_IV_Percentage_Change")]
        public double? Expiry2IvPercentageChange { get; set; }

        [Column("Expiry3_IV")]
        public double? Expiry3Iv { get; set; }

        [Column("Expiry3_IV_Change")]
        public double? Expiry3IvChange { get; set; }

        [Column("Expiry3_IV_Percentage_Change")]
        public double? Expiry3IvPercentageChange { get; set; }

        [Column("Expiry4_IV")]
        public double? Expiry4Iv { get; set; }

        [Column("Expiry5_IV")]
        public double? Expiry5Iv { get; set; }

        [Column("Expiry6_IV")]
        public double? Expiry6Iv { get; set; }

        [Column("Expiry7_IV")]
        public double? Expiry7Iv { get; set; }

        [Column("Expiry8_IV")]
        public double? Expiry8Iv { get; set; }

        [Column("HV10")]
        public double? Hv10 { get; set; }

        [Column("HV180")]
        public double? Hv180 { get; set; }

        [Column("HV20")]
        public double? Hv20 { get; set; }

        [Column("HV30")]
        public double? Hv30 { get; set; }

        [Column("HV30_1_Day_Close")]
        public double? Hv30_1DayClose { get; set; }

        [Column("HV30_3_Day_Close")]
        public double? Hv30_3DayClose { get; set; }

        [Column("HV30_5_Day_Close")]
        public double? Hv30_5DayClose { get; set; }

        [Column("HV30_Percent_of_52_Week_Range")]
        public double? Hv30PercentOf52WeekRange { get; set; }

        [Column("HV30_Percentile_Rank")]
        public double? Hv30PercentileRank { get; set; }

        [Column("HV30_Week_Ago")]
        public double? Hv30WeekAgo { get; set; }

        [Column("HV360")]
        public double? Hv360 { get; set; }

        [Column("HV60")]
        public double? Hv60 { get; set; }

        [Column("HV60_1_Day_Close")]
        public double? Hv60_1DayClose { get; set; }

        [Column("HV60_3_Day_Close")]
        public double? Hv60_3DayClose { get; set; }

        [Column("HV60_5_Day_Close")]
        public double? Hv60_5DayClose { get; set; }

        [Column("HV90")]
        public double? Hv90 { get; set; }

        [Column("HV90_1_Day_Close")]
        public double? Hv90_1DayClose { get; set; }

        [Column("HV90_3_Day_Close")]
        public double? Hv90_3DayClose { get; set; }

        [Column("HV90_5_Day_Close")]
        public double? Hv90_5DayClose { get; set; }

        [Column("IV180")]
        public double? Iv180 { get; set; }

        [Column("IV180_1_Day_Close")]
        public double? Iv180_1DayClose { get; set; }

        [Column("IV30")]
        public double? Iv30 { get; set; }

        [Column("IV30_1_Day_Close")]
        public double? Iv30_1DayClose { get; set; }

        [Column("IV30_1_Month_Close")]
        public double? Iv30_1MonthClose { get; set; }

        [Column("IV30_1_Week_Close")]
        public double? Iv30_1WeekClose { get; set; }

        [Column("IV30_3_Day_Change")]
        public double? Iv30_3DayChange { get; set; }

        [Column("IV30_3_Day_Close")]
        public double? Iv30_3DayClose { get; set; }

        [Column("IV30_5_Day_Change")]
        public double? Iv30_5DayChange { get; set; }

        [Column("IV30_5_Day_Close")]
        public double? Iv30_5DayClose { get; set; }

        [Column("IV30_52_Week_High")]
        public double? Iv30_52WeekHigh { get; set; }

        [Column("IV30_52_Week_Low")]
        public double? Iv30_52WeekLow { get; set; }

        [Column("IV30_Change")]
        public double? Iv30Change { get; set; }

        [Column("IV30_Open")]
        public double? Iv30Open { get; set; }

        [Column("IV30_Percent_of_52_Week_Range")]
        public double? Iv30PercentOf52WeekRange { get; set; }

        [Column("IV30_Percentage_Change")]
        public double? Iv30PercentageChange { get; set; }

        [Column("IV30_Percentile_Rank")]
        public double? Iv30PercentileRank { get; set; }

        [Column("IV360")]
        public double? Iv360 { get; set; }

        [Column("IV360_1_Day_Close")]
        public double? Iv360_1DayClose { get; set; }

        [Column("IV60")]
        public double? Iv60 { get; set; }

        [Column("IV60_1_Day_Close")]
        public double? Iv60_1DayClose { get; set; }

        [Column("IV60_3_Day_Close")]
        public double? Iv60_3DayClose { get; set; }

        [Column("IV60_5_Day_Change")]
        public double? Iv60_5DayChange { get; set; }

        [Column("IV60_5_Day_Close")]
        public double? Iv60_5DayClose { get; set; }

        [Column("IV60_Change")]
        public double? Iv60Change { get; set; }

        [Column("IV60_Percent_of_52_Week_Range")]
        public double? Iv60PercentOf52WeekRange { get; set; }

        [Column("IV60_Percentage_Change")]
        public double? Iv60PercentageChange { get; set; }

        [Column("IV60_Percentile_Rank")]
        public double? Iv60PercentileRank { get; set; }

        [Column("IV90")]
        public double? Iv90 { get; set; }

        [Column("IV90_1_Day_Close")]
        public double? Iv90_1DayClose { get; set; }

        [Column("IV90_3_Day_Close")]
        public double? Iv90_3DayClose { get; set; }

        [Column("IV90_5_Day_Change")]
        public double? Iv90_5DayChange { get; set; }

        [Column("IV90_5_Day_Close")]
        public double? Iv90_5DayClose { get; set; }

        [Column("IV90_Change")]
        public double? Iv90Change { get; set; }

        [Column("IV90_Percent_of_52_Week_Range")]
        public double? Iv90PercentOf52WeekRange { get; set; }

        [Column("IV90_Percentage_Change")]
        public double? Iv90PercentageChange { get; set; }

        [Column("IV90_Percentile_Rank")]
        public double? Iv90PercentileRank { get; set; }

        [Column("One_Day_Standard_Deviation")]
        public double? OneDayStandardDeviation { get; set; }

        [Column("Percent_of_Average_IV30")]
        public double? PercentOfAverageIv30 { get; set; }

        [Column("Expiry1_IV_vs_Expiry2_IV")]
        public double? Expiry1IvVsExpiry2Iv { get; set; }

        [Column("Expiry1_IV_vs_Expiry3_IV")]
        public double? Expiry1IvVsExpiry3Iv { get; set; }

        [Column("Expiry1_IV_vs_Expiry4_IV")]
        public double? Expiry1IvVsExpiry4Iv { get; set; }

        [Column("Expiry1_vs_Expiry2_vol_ratio")]
        public double? Expiry1VsExpiry2VolRatio { get; set; }

        [Column("Expiry1_vs_Expiry3_vol_ratio")]
        public double? Expiry1VsExpiry3VolRatio { get; set; }

        [Column("Expiry2_IV_vs_Expiry3_IV")]
        public double? Expiry2IvVsExpiry3Iv { get; set; }

        [Column("Expiry2_IV_vs_Expiry4_IV")]
        public double? Expiry2IvVsExpiry4Iv { get; set; }

        [Column("Expiry2_vs_Expiry3_IV_ratio")]
        public double? Expiry2VsExpiry3IvRatio { get; set; }

        [Column("Expiry3_IV_vs_Expiry4_IV")]
        public double? Expiry3IvVsExpiry4Iv { get; set; }

        [Column("Expiry4_IV_vs_Expiry5_IV")]
        public double? Expiry4IvVsExpiry5Iv { get; set; }

        [Column("Expiry5_IV_vs_Expiry6_IV")]
        public double? Expiry5IvVsExpiry6Iv { get; set; }

        [Column("Expiry6_IV_vs_Expiry7_IV")]
        public double? Expiry6IvVsExpiry7Iv { get; set; }

        [Column("Expiry7_IV_vs_Expiry8_IV")]
        public double? Expiry7IvVsExpiry8Iv { get; set; }

        [Column("HV180_vs_IV30")]
        public double? Hv180VsIv30 { get; set; }

        [Column("HV180_vs_IV60")]
        public double? Hv180VsIv60 { get; set; }

        [Column("IV30_HV30_Ratio")]
        public double? Iv30Hv30Ratio { get; set; }

        [Column("IV30_vs_HV10")]
        public double? Iv30VsHv10 { get; set; }

        [Column("IV30_vs_HV20")]
        public double? Iv30VsHv20 { get; set; }

        [Column("IV30_vs_HV30")]
        public double? Iv30VsHv30 { get; set; }

        [Column("IV30_vs_IV60")]
        public double? Iv30VsIv60 { get; set; }

        [Column("IV30_vs_IV90")]
        public double? Iv30VsIv90 { get; set; }

        [Column("IV360_vs_HV360")]
        public double? Iv360VsHv360 { get; set; }

        [Column("IV60_HV60_Ratio")]
        public double? Iv60Hv60Ratio { get; set; }

        [Column("IV60_vs_HV10")]
        public double? Iv60VsHv10 { get; set; }

        [Column("IV60_vs_HV20")]
        public double? Iv60VsHv20 { get; set; }

        [Column("IV60_vs_HV30")]
        public double? Iv60VsHv30 { get; set; }

        [Column("IV60_vs_HV60")]
        public double? Iv60VsHv60 { get; set; }

        [Column("IV60_vs_IV90")]
        public double? Iv60VsIv90 { get; set; }

        [Column("IV90_vs_HV10")]
        public double? Iv90VsHv10 { get; set; }

        [Column("IV90_vs_HV20")]
        public double? Iv90VsHv20 { get; set; }

        [Column("IV90_vs_HV30")]
        public double? Iv90VsHv30 { get; set; }

        [Column("IV90_vs_HV90")]
        public double? Iv90VsHv90 { get; set; }

        [Column("Average_Underlying_Volume")]
        public long? AverageUnderlyingVolume { get; set; }

        [Column("Percent_Average_Underlying_Volume")]
        public long? PercentAverageUnderlyingVolume { get; set; }

        [Column("Underlying_Volume")]
        public long? UnderlyingVolume { get; set; }

        [Column("VWAP")]
        public double? Vwap { get; set; }

        [Column("Average_Call_Delta")]
        public double? AverageCallDelta { get; set; }

        [Column("Average_Call_Gamma")]
        public double? AverageCallGamma { get; set; }

        [Column("Average_Call_Open_Interest")]
        public long? AverageCallOpenInterest { get; set; }

        [Column("Average_Call_Premium")]
        public double? AverageCallPremium { get; set; }

        [Column("Average_Call_Vega")]
        public double? AverageCallVega { get; set; }

        [Column("Average_Call_Volume")]
        public long? AverageCallVolume { get; set; }

        [Column("Average_Calls_Between_Bid_Ask")]
        public long? AverageCallsBetweenBidAsk { get; set; }

        [Column("Average_Calls_On_Ask")]
        public long? AverageCallsOnAsk { get; set; }

        [Column("Average_Calls_On_Bid")]
        public long? AverageCallsOnBid { get; set; }

        [Column("Average_Open_Interest")]
        public long? AverageOpenInterest { get; set; }

        [Column("Average_Option_Volume")]
        public long? AverageOptionVolume { get; set; }

        [Column("Average_Otm_Calls_On_Ask")]
        public long? AverageOtmCallsOnAsk { get; set; }

        [Column("Average_Otm_Calls_On_Bid")]
        public long? AverageOtmCallsOnBid { get; set; }

        [Column("Average_Otm_Puts_On_Ask")]
        public long? AverageOtmPutsOnAsk { get; set; }

        [Column("Average_Otm_Puts_On_Bid")]
        public long? AverageOtmPutsOnBid { get; set; }

        [Column("Average_Put_Delta")]
        public double? AveragePutDelta { get; set; }

        [Column("Average_Put_Gamma")]
        public double? AveragePutGamma { get; set; }

        [Column("Average_Put_Open_Interest")]
        public long? AveragePutOpenInterest { get; set; }

        [Column("Average_Put_Premium")]
        public double? AveragePutPremium { get; set; }

        [Column("Average_Put_Vega")]
        public double? AveragePutVega { get; set; }

        [Column("Average_Put_Volume")]
        public long? AveragePutVolume { get; set; }

        [Column("Average_Puts_Between_Bid_Ask")]
        public long? AveragePutsBetweenBidAsk { get; set; }

        [Column("Average_Puts_On_Ask")]
        public long? AveragePutsOnAsk { get; set; }

        [Column("Average_Puts_On_Bid")]
        public long? AveragePutsOnBid { get; set; }

        [Column("Average_Trade_Size")]
        public double? AverageTradeSize { get; set; }

        [Column("Call_Open_Interest")]
        public long? CallOpenInterest { get; set; }

        [Column("Call_Open_Interest_1_Day_Ago")]
        public long? CallOpenInterest1DayAgo { get; set; }

        [Column("Call_Open_Interest_1_Day_Change_Percent")]
        public double? CallOpenInterest1DayChangePercent { get; set; }

        [Column("Call_Premium")]
        public double? CallPremium { get; set; }

        [Column("Call_Put_Ratio")]
        public double? CallPutRatio { get; set; }

        [Column("Call_Trade_Count")]
        public long? CallTradeCount { get; set; }

        [Column("Call_Volume")]
        public long? CallVolume { get; set; }

        [Column("Call_Volume_1_Day_Ago")]
        public long? CallVolume1DayAgo { get; set; }

        [Column("Call_Volume_Percent_of_Call_Open_Interest")]
        public double? CallVolumePercentOfCallOpenInterest { get; set; }

        [Column("Calls_Between_Bid_and_Ask")]
        public long? CallsBetweenBidAndAsk { get; set; }

        [Column("Calls_on_Ask")]
        public long? CallsOnAsk { get; set; }

        [Column("Calls_on_Bid")]
        public long? CallsOnBid { get; set; }

        [Column("Cumulative_Call_Delta")]
        public double? CumulativeCallDelta { get; set; }

        [Column("Cumulative_Call_Gamma")]
        public double? CumulativeCallGamma { get; set; }

        [Column("Cumulative_Call_Vega")]
        public double? CumulativeCallVega { get; set; }

        [Column("Cumulative_Put_Delta")]
        public double? CumulativePutDelta { get; set; }

        [Column("Cumulative_Put_Gamma")]
        public double? CumulativePutGamma { get; set; }

        [Column("Cumulative_Put_Vega")]
        public double? CumulativePutVega { get; set; }

        [Column("Option_Volume")]
        public long? OptionVolume { get; set; }

        [Column("Option_Volume_Percent_of_Option_Open_Interest")]
        public double? OptionVolumePercentOfOptionOpenInterest { get; set; }

        [Column("OTM_Calls_on_Ask")]
        public long? OtmCallsOnAsk { get; set; }

        [Column("OTM_Calls_on_Bid")]
        public long? OtmCallsOnBid { get; set; }

        [Column("OTM_Puts_on_Ask")]
        public long? OtmPutsOnAsk { get; set; }

        [Column("OTM_Puts_on_Bid")]
        public long? OtmPutsOnBid { get; set; }

        [Column("Partial_Day_Average_Call_Volume")]
        public long? PartialDayAverageCallVolume { get; set; }

        [Column("Partial_Day_Average_Option_Volume")]
        public long? PartialDayAverageOptionVolume { get; set; }

        [Column("Partial_Day_Average_Put_Volume")]
        public long? PartialDayAveragePutVolume { get; set; }

        [Column("Partial_Day_Average_Underlying_Volume")]
        public long? PartialDayAverageUnderlyingVolume { get; set; }

        [Column("Percent_of_Average_Call_Volume")]
        public long? PercentOfAverageCallVolume { get; set; }

        [Column("Percent_of_Average_Put_Volume")]
        public long? PercentOfAveragePutVolume { get; set; }

        [Column("Percent_of_Average_Volume")]
        public long? PercentOfAverageVolume { get; set; }

        [Column("Percent_of_Calls_on_Ask")]
        public double? PercentOfCallsOnAsk { get; set; }

        [Column("Percent_of_Calls_on_Bid")]
        public double? PercentOfCallsOnBid { get; set; }

        [Column("Percent_of_OTM_Calls_on_Ask")]
        public double? PercentOfOtmCallsOnAsk { get; set; }

        [Column("Percent_of_OTM_Calls_on_Bid")]
        public double? PercentOfOtmCallsOnBid { get; set; }

        [Column("Percent_of_OTM_Puts_on_Ask")]
        public double? PercentOfOtmPutsOnAsk { get; set; }

        [Column("Percent_of_OTM_Puts_on_Bid")]
        public double? PercentOfOtmPutsOnBid { get; set; }

        [Column("Percent_of_Partial_Day_Average_Call_Volume")]
        public long? PercentOfPartialDayAverageCallVolume { get; set; }

        [Column("Percent_of_Partial_Day_Average_Option_Volume")]
        public long? PercentOfPartialDayAverageOptionVolume { get; set; }

        [Column("Percent_of_Partial_Day_Average_Put_Volume")]
        public long? PercentOfPartialDayAveragePutVolume { get; set; }

        [Column("Percent_of_Partial_Day_Average_Underlying_Volume")]
        public long? PercentOfPartialDayAverageUnderlyingVolume { get; set; }

        [Column("Percent_of_Puts_on_Ask")]
        public double? PercentOfPutsOnAsk { get; set; }

        [Column("Percent_of_Puts_on_Bid")]
        public double? PercentOfPutsOnBid { get; set; }

        [Column("Put_Call_Ratio")]
        public double? PutCallRatio { get; set; }

        [Column("Put_Open_Interest")]
        public long? PutOpenInterest { get; set; }

        [Column("Put_Open_Interest_1_Day_Ago")]
        public long? PutOpenInterest1DayAgo { get; set; }

        [Column("Put_Open_Interest_1_Day_Change_Percent")]
        public double? PutOpenInterest1DayChangePercent { get; set; }

        [Column("Put_Premium")]
        public double? PutPremium { get; set; }

        [Column("Put_Trade_Count")]
        public long? PutTradeCount { get; set; }

        [Column("Put_Volume")]
        public long? PutVolume { get; set; }

        [Column("Put_Volume_1_Day_Ago")]
        public long? PutVolume1DayAgo { get; set; }

        [Column("Put_Volume_Percent_of_Put_Open_Interest")]
        public double? PutVolumePercentOfPutOpenInterest { get; set; }

        [Column("Puts_Between_Bid_and_Ask")]
        public long? PutsBetweenBidAndAsk { get; set; }

        [Column("Puts_on_Ask")]
        public long? PutsOnAsk { get; set; }

        [Column("Puts_on_Bid")]
        public long? PutsOnBid { get; set; }

        [Column("Sum_Call_Volume_3_Day")]
        public long? SumCallVolume3Day { get; set; }

        [Column("Sum_Call_Volume_5_Day")]
        public long? SumCallVolume5Day { get; set; }

        [Column("Sum_Call_Volume_Last_2D")]
        public long? SumCallVolumeLast2D { get; set; }

        [Column("Sum_Call_Volume_Last_4D")]
        public long? SumCallVolumeLast4D { get; set; }

        [Column("Sum_Put_Volume_3_Day")]
        public long? SumPutVolume3Day { get; set; }

        [Column("Sum_Put_Volume_5_Day")]
        public long? SumPutVolume5Day { get; set; }

        [Column("Sum_Put_Volume_Last_2D")]
        public long? SumPutVolumeLast2D { get; set; }

        [Column("Sum_Put_Volume_Last_4D")]
        public long? SumPutVolumeLast4D { get; set; }

        [Column("Total_Open_Interest")]
        public long? TotalOpenInterest { get; set; }

        [Column("Total_Open_Interest_1_Day_Change_Percent")]
        public double? TotalOpenInterest1DayChangePercent { get; set; }

        [Column("Total_Option_Trades_On_The_Day")]
        public long? TotalOptionTradesOnTheDay { get; set; }

        [Column("Company_Name")]
        public string? CompanyName { get; set; }

        [Column("Industry")]
        public string? Industry { get; set; }

        [Column("Market_Capitalization")]
        public long? MarketCapitalization { get; set; }

        [Column("Price_To_Earnings_Ratio")]
        public double? PriceToEarningsRatio { get; set; }

        [Column("Sector")]
        public string? Sector { get; set; }

        [Column("Shares_Outstanding")]
        public long? SharesOutstanding { get; set; }

        [Column("Average_Historical_Earnings_Move")]
        public double? AverageHistoricalEarningsMove { get; set; }

        [Column("Average_Implied_Earnings_Move")]
        public double? AverageImpliedEarningsMove { get; set; }

        [Column("Days_After_Earnings")]
        public long? DaysAfterEarnings { get; set; }

        [Column("Days_To_Next_Earnings_Date")]
        public long? DaysToNextEarningsDate { get; set; }

        [Column("Days_Until_Next_Dividend_Date")]
        public long? DaysUntilNextDividendDate { get; set; }

        [Column("Forward_Volatility")]
        public double? ForwardVolatility { get; set; }

        [Column("Implied_Earnings_Move")]
        public double? ImpliedEarningsMove { get; set; }

        [Column("Last_Earnings_Date")]
        public DateTime? LastEarningsDate { get; set; }

        [Column("Last_Earnings_Time_Of_Day")]
        public string? LastEarningsTimeOfDay { get; set; }

        [Column("Next_Dividend_Amount")]
        public double? NextDividendAmount { get; set; }

        [Column("Next_Dividend_Date")]
        public DateTime? NextDividendDate { get; set; }

        [Column("Next_Earnings_Status")]
        public string? NextEarningsStatus { get; set; }

        [Column("Next_Earnings_Time")]
        public string? NextEarningsTime { get; set; }
    }
}
