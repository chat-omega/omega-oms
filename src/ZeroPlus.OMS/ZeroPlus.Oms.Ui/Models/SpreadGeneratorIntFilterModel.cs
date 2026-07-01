using DevExpress.Mvvm;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Generators.SpreadGenerators.Settings;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class SpreadGeneratorIntFilterModel : BindableBase, ISpreadGeneratorIntFilter
    {
        public static List<char> FiltersList { get; } = new List<char> { '>', '≥', '<', '≤', '=' };

        [Bindable]
        public partial bool Enabled { get; set; }

        [Bindable]
        public partial char Filter { get; set; }

        [Bindable]
        public partial int Value { get; set; }

        public SpreadGeneratorIntFilterModel()
        {
            Filter = FiltersList.FirstOrDefault();
        }

        public SpreadGeneratorIntFilterModel Clone()
        {
            return new SpreadGeneratorIntFilterModel()
            {
                Enabled = Enabled,
                Filter = Filter,
                Value = Value,
            };
        }
    }
}
