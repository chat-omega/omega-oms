using DevExpress.Mvvm;
using System;
using System.Collections.Generic;
using ZeroPlus.Oms.Ui.Models.Volatility;

namespace ZeroPlus.Oms.Ui.ViewModels.Volatility
{
    public class LegViewModel : BindableBase
    {
        public LegViewModel()
        {
            // Defaults
            Quantity = 1;
            Side = PositionSide.Long;
            Type = OptionType.Call;
            IsValid = true;
        }

        public bool IsValid
        {
            get => GetProperty(() => IsValid);
            set => SetProperty(() => IsValid, value);
        }

        public PositionSide Side
        {
            get => GetProperty(() => Side);
            set => SetProperty(() => Side, value);
        }

        public OptionType Type
        {
            get => GetProperty(() => Type);
            set => SetProperty(() => Type, value);
        }

        public double Strike
        {
            get => GetProperty(() => Strike);
            set => SetProperty(() => Strike, value);
        }

        public DateTime ExpiryDate
        {
            get => GetProperty(() => ExpiryDate);
            set => SetProperty(() => ExpiryDate, value);
        }

        public int Quantity
        {
            get => GetProperty(() => Quantity);
            set => SetProperty(() => Quantity, value);
        }

        public double EntryPrice
        {
            get => GetProperty(() => EntryPrice);
            set => SetProperty(() => EntryPrice, value);
        }

        // For Reference in Calculations
        public double TimeToExpiryYears(DateTime evaluationDate)
        {
            if (ExpiryDate <= evaluationDate) return 0;
            return (ExpiryDate - evaluationDate).TotalDays / 365.0;
        }
    }
}
