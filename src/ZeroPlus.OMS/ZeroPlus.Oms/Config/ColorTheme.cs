namespace ZeroPlus.Oms.Config
{
    public class ColorTheme
    {
        public string Name { get; set; }
        public string Transparent { get; set; }
        public string WhiteColorFg { get; set; }
        public string WhiteColor { get; set; }
        public string GreenColorFg { get; set; }
        public string GreenColor { get; set; }
        public string GreenColorLight { get; set; }
        public string RedColorFg { get; set; }
        public string RedColor { get; set; }
        public string RedColorLight { get; set; }
        public string BlueColorFg { get; set; }
        public string BlueColor { get; set; }
        public string LightYellowColor { get; set; }
        public string CanceledColor { get; set; }
        public string PendingNewColorFg { get; set; }
        public string PendingNewColor { get; set; }
        public string PendingNewColorLight { get; set; }
        public string GreenFocusedColor { get; set; }
        public string GreenFocusedColorLight { get; set; }
        public string RedFocusedColor { get; set; }
        public string RedFocusedColorLight { get; set; }
        public string CanceledFocusedColor { get; set; }
        public string PendingNewFocusedColor { get; set; }
        public string PendingNewFocusedColorLight { get; set; }
        public string UncertainColorFg { get; set; }
        public string UncertainColor { get; set; }
        public string UncertainFocusedColor { get; set; }
        public string OrangeColor { get; set; }
        public string OrangeFocusedColor { get; set; }

        public override int GetHashCode()
        {
            return (Name ?? "").GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is ColorTheme other && other.Name == Name;
        }
    }
}
