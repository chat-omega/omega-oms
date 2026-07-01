using DevExpress.Xpf.Core;
using System;
using System.Windows;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for EdgeScanFeedFilterSelectionView.xaml
    /// </summary>
    public partial class EdgeScanFeedFilterSelectionView : ThemedWindow
    {
        public EdgeScanFeedFilterSelectionView()
        {
            InitializeComponent();
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void GridControl_CustomColumnsDisplayText(object sender, DevExpress.Xpf.Grid.CustomColumnDisplayTextEventArgs e)
        {
            if (e.Value is DateTime dateTime)
            {
                switch (e.Column.FieldName)
                {
                    case "LastUpdateTime":
                        e.DisplayText = dateTime.ToString("MM/dd/yy hh:mm tt");
                        break;
                    case "LutTimeOnly":
                        e.DisplayText = dateTime.ToString("T");
                        break;
                    case "DateAdded":
                        e.DisplayText = dateTime.ToString("d");
                        break;
                    case "NearExpiration":
                    case "FarExpiration":
                        if (dateTime == DateTime.MinValue)
                        {
                            e.DisplayText = "";
                        }
                        else
                        {
                            e.DisplayText = dateTime.ToString("MMM dd yy");
                        }
                        break;
                    default:
                        if (e.Column.FieldName.StartsWith("Expiration"))
                        {
                            if (dateTime == DateTime.MinValue)
                            {
                                e.DisplayText = "";
                            }
                            else
                            {
                                e.DisplayText = dateTime.ToString("MMM dd yy");
                            }
                        }
                        else
                        {
                            e.DisplayText = dateTime.ToString("hh:mm:ss.ffff tt");
                        }

                        break;
                }
            }
            else if (e.Value is double doubleVal)
            {
                if (double.IsNaN(doubleVal))
                {
                    e.DisplayText = "";
                }
                else if (e.Column.FieldName is "Price" or "AveragePrice")
                {
                    e.DisplayText = doubleVal.ToString("#,##0.00;(#,##0.00)");
                }
                else if (e.Column.FieldName == "Delta")
                {
                    e.DisplayText = doubleVal.ToString("n3");
                }
                else
                {
                    e.DisplayText = doubleVal.ToString("n2");
                }
            }
            else if (e.Value is TimeSpan timeSpan)
            {
                e.DisplayText = timeSpan.TotalMilliseconds.ToString("n0");
            }
        }
    }
}
