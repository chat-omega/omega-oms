using DevExpress.Data;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors.Settings;
using DevExpress.Xpf.Grid;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class TableCustomizationViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private static readonly Color _transparent = Color.FromScRgb(0, 1, 1, 1);
        public GridControl Grid { get; set; }
        public Dictionary<string, ColumnConfigModel> ColumnFieldNameToConfigMap { get; set; }
        public Dispatcher Dispatcher { get; set; }
        public List<EditSettingsHorizontalAlignment> EditSettingsHorizontalAlignments => ((EditSettingsHorizontalAlignment[])Enum.GetValues(typeof(EditSettingsHorizontalAlignment))).ToList();
        public List<UnboundColumnType> UnboundColumnTypes => ((UnboundColumnType[])Enum.GetValues(typeof(UnboundColumnType))).ToList();

        [Bindable]
        public partial ObservableCollection<GridColumn> VisibleColumns { get; set; }

        [Bindable]
        public partial ObservableCollection<GridColumn> HiddenColumns { get; set; }

        public TableCustomizationViewModel()
        {
            ColumnFieldNameToConfigMap = new Dictionary<string, ColumnConfigModel>();
            VisibleColumns = new ObservableCollection<GridColumn>();
            HiddenColumns = new ObservableCollection<GridColumn>();
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        public void Customize(GridControl grid, Dictionary<string, ColumnConfigModel> columnFieldNameToConfigMap)
        {
            Grid = grid;
            ColumnFieldNameToConfigMap = columnFieldNameToConfigMap;
            Refresh();
        }

        [Command]
        public void Show(ObservableCollectionCore<object> objects)
        {
            foreach (object obj in objects)
            {
                if (obj is GridColumn column)
                {
                    column.Visible = true;
                }
            }
            Refresh();
        }

        [Command]
        public void Hide(ObservableCollectionCore<object> objects)
        {
            foreach (object obj in objects)
            {
                if (obj is GridColumn column)
                {
                    column.Visible = false;
                }
            }
            Refresh();
        }

        [Command]
        public void FontChanged(object[] parameters)
        {
            try
            {
                if (parameters == null || parameters.Length < 2)
                {
                    return;
                }

                GridColumn target = (GridColumn)parameters[0];
                double font = Convert.ToDouble(parameters[1]);

                string fieldName = target.FieldName;
                if (!ColumnFieldNameToConfigMap.TryGetValue(fieldName, out ColumnConfigModel configModel))
                {
                    configModel = new ColumnConfigModel
                    {
                        FieldName = fieldName
                    };
                    ColumnFieldNameToConfigMap[fieldName] = configModel;
                }
                configModel.FontSize = font;
                LoadFont(target, font);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(FontChanged));
            }
        }

        [Command]
        public void HorizontalAlignmentChangedCommand(object[] parameters)
        {
            try
            {
                if (parameters == null || parameters.Length < 2)
                {
                    return;
                }

                GridColumn target = (GridColumn)parameters[0];
                EditSettingsHorizontalAlignment horizontalAlignment = (EditSettingsHorizontalAlignment)parameters[1];

                string fieldName = target.FieldName;
                if (!ColumnFieldNameToConfigMap.TryGetValue(fieldName, out ColumnConfigModel configModel))
                {
                    configModel = new ColumnConfigModel
                    {
                        FieldName = fieldName
                    };
                    ColumnFieldNameToConfigMap[fieldName] = configModel;
                }
                configModel.HorizontalAlignment = horizontalAlignment;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(FontChanged));
            }
        }

        [Command]
        public void TypeChangedCommand(object[] parameters)
        {
            try
            {
                if (parameters == null || parameters.Length < 2)
                {
                    return;
                }

                GridColumn target = (GridColumn)parameters[0];
                UnboundColumnType type = (UnboundColumnType)parameters[1];

                string fieldName = target.FieldName;
                if (!ColumnFieldNameToConfigMap.TryGetValue(fieldName, out ColumnConfigModel configModel))
                {
                    configModel = new ColumnConfigModel
                    {
                        FieldName = fieldName
                    };
                    ColumnFieldNameToConfigMap[fieldName] = configModel;
                }
                configModel.UnboundColumnType = type;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(FontChanged));
            }
        }

        [Command]
        public void ForegroundChanged(object[] parameters)
        {
            try
            {
                if (parameters == null || parameters.Length < 2)
                {
                    return;
                }

                GridColumn target = (GridColumn)parameters[0];
                Color color = (Color)parameters[1];

                string fieldName = target.FieldName;
                if (!ColumnFieldNameToConfigMap.TryGetValue(fieldName, out ColumnConfigModel configModel))
                {
                    configModel = new ColumnConfigModel
                    {
                        FieldName = fieldName
                    };
                    ColumnFieldNameToConfigMap[fieldName] = configModel;
                }
                configModel.Foreground = color;
                LoadForeground(target, color);

            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ForegroundChanged));
            }
        }

        [Command]
        public void BackgroundChanged(object[] parameters)
        {
            try
            {
                if (parameters == null || parameters.Length < 2)
                {
                    return;
                }

                GridColumn target = (GridColumn)parameters[0];
                Color color = (Color)parameters[1];

                string fieldName = target.FieldName;
                if (!ColumnFieldNameToConfigMap.TryGetValue(fieldName, out ColumnConfigModel configModel))
                {
                    configModel = new ColumnConfigModel
                    {
                        FieldName = fieldName
                    };
                    ColumnFieldNameToConfigMap[fieldName] = configModel;
                }
                configModel.Background = color;
                LoadBackground(target, color, allowTransaparent: true);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BackgroundChanged));
            }
        }

        private void Refresh()
        {
            VisibleColumns.Clear();
            HiddenColumns.Clear();
            if (Grid == null)
            {
                return;
            }

            foreach (GridColumn column in Grid.Columns)
            {
                if (column.Visible)
                {
                    if (column.Header == null || (column.Header is string headerString && string.IsNullOrWhiteSpace(headerString)))
                    {
                        column.Header = Regex.Replace(column.FieldName, "(\\B[A-Z])", " $1");
                    }
                    VisibleColumns.Add(column);
                }
                else
                {
                    HiddenColumns.Add(column);
                }
            }
        }

        public void Load(GridControl grid, Dictionary<string, ColumnConfigModel> columnFieldNameToConfigMap)
        {
            foreach (GridColumn column in grid.Columns)
            {
                string fieldName = column.FieldName;
                if (columnFieldNameToConfigMap.TryGetValue(fieldName, out ColumnConfigModel config))
                {
                    LoadFont(column, config.FontSize);
                    LoadForeground(column, config.Foreground);
                    LoadBackground(column, config.Background);
                }
            }
        }

        private static void LoadFont(GridColumn target, double font)
        {
            try
            {
                if (font <= 0)
                {
                    return;
                }

                Style style = new()
                {
                    TargetType = typeof(LightweightCellEditor)
                };

                if (target.ActualCellStyle != null)
                {
                    foreach (SetterBase setter in target.ActualCellStyle.Setters)
                    {
                        style.Setters.Add(setter);
                    }
                }

                style.Setters.Add(new Setter()
                {
                    Property = LightweightCellEditorBase.FontSizeProperty,
                    Value = font,
                });
                target.CellStyle = style;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadFont));
            }
        }

        private static void LoadForeground(GridColumn target, Color color, bool allowTransaparent = false)
        {
            try
            {
                if (color == _transparent && !allowTransaparent)
                {
                    return;
                }

                SolidColorBrush brush = new(color);

                Style style = new()
                {
                    TargetType = typeof(LightweightCellEditor)
                };

                if (target.ActualCellStyle != null)
                {
                    foreach (SetterBase setter in target.ActualCellStyle.Setters)
                    {
                        style.Setters.Add(setter);
                    }
                }

                style.Setters.Add(new Setter()
                {
                    Property = LightweightCellEditorBase.ForegroundProperty,
                    Value = brush,
                });
                target.CellStyle = style;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadForeground));
            }
        }

        private static void LoadBackground(GridColumn target, Color color, bool allowTransaparent = false)
        {
            try
            {
                if (color == _transparent && !allowTransaparent)
                {
                    return;
                }

                SolidColorBrush brush = new(color);

                Style style = new()
                {
                    TargetType = typeof(LightweightCellEditor)
                };

                if (target.ActualCellStyle != null)
                {
                    foreach (SetterBase setter in target.ActualCellStyle.Setters)
                    {
                        style.Setters.Add(setter);
                    }
                }

                style.Setters.Add(new Setter()
                {
                    Property = LightweightCellEditor.BackgroundProperty,
                    Value = brush,
                });
                target.CellStyle = style;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadBackground));
            }
        }
    }
}
