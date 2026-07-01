using DevExpress.Mvvm;
using System;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Oms.Ui.Models
{
    public class TradeFeedModel : BindableBase, ITradeFeedModel
    {
        private int _id;
        private bool _isFirm;
        private bool _isCopyCat;
        private int _quantity;
        private BaseStrategy _baseStrategy;
        private double _price;
        private double _bid;
        private double _ask;
        private double _delta;
        private DateTime _tradeTime;
        private string _exchange;
        private string _symbol;
        private string _description;
        private string _underlying;
        private Side _side;

        public int Id { get => _id; set => SetValue(ref _id, value); }
        public bool IsFirm { get => _isFirm; set => SetValue(ref _isFirm, value); }
        public bool IsCopyCat { get => _isCopyCat; set => SetValue(ref _isCopyCat, value); }
        public int Quantity { get => _quantity; set => SetValue(ref _quantity, value); }
        public BaseStrategy BaseStrategy { get => _baseStrategy; set => SetValue(ref _baseStrategy, value); }
        public double Price { get => _price; set => SetValue(ref _price, value); }
        public double Bid { get => _bid; set => SetValue(ref _bid, value); }
        public double Ask { get => _ask; set => SetValue(ref _ask, value); }
        public double Delta { get => _delta; set => SetValue(ref _delta, value); }
        public DateTime TradeTime { get => _tradeTime; set => SetValue(ref _tradeTime, value); }
        public string Exchange { get => _exchange; set => SetValue(ref _exchange, value); }
        public string Symbol { get => _symbol; set => SetValue(ref _symbol, value); }
        public string Description { get => _description; set => SetValue(ref _description, value); }
        public string Underlying { get => _underlying; set => SetValue(ref _underlying, value); }
        public Side Side { get => _side; set => SetValue(ref _side, value); }
    }
}