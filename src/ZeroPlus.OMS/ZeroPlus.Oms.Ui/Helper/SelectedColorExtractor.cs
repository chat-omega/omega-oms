using DevExpress.Xpf.Grid;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(GridColumn), typeof(Color))]
    public class SelectedColorExtractor : IValueConverter
    {
        private static readonly Color DefaultColor = Color.FromScRgb(0, 1, 1, 1);
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                RowData rowData = value as RowData;
                TableCustomizationViewModel dataContext = rowData.View.DataContext as TableCustomizationViewModel;
                if (rowData.Row is GridColumn column)
                {
                    string fieldName = column.FieldName;
                    if (dataContext.ColumnFieldNameToConfigMap.TryGetValue(fieldName, out Models.ColumnConfigModel configModel))
                    {
                        switch ((string)parameter)
                        {
                            case "BG":
                                return configModel.Background;
                            case "FG":
                                return configModel.Foreground;
                        }
                    }
                    return DefaultColor;
                }
                else
                {
                    return DefaultColor;
                }
            }
            catch (Exception)
            {
                return DefaultColor;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
