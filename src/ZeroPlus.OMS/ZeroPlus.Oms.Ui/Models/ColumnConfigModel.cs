using DevExpress.Data;
using DevExpress.Xpf.Editors.Settings;
using System.Windows.Media;

namespace ZeroPlus.Oms.Ui.Models
{
    public class ColumnConfigModel
    {
        public string FieldName { get; set; }
        public string Header { get; set; }
        public bool Visible { get; set; }
        public double FontSize { get; set; }
        public Color Foreground { get; set; } = Color.FromScRgb(0, 1, 1, 1);
        public Color Background { get; set; } = Color.FromScRgb(0, 1, 1, 1);
        public UnboundColumnType UnboundColumnType { get; set; }
        public EditSettingsHorizontalAlignment HorizontalAlignment { get; set; }
    }
}
