using DevExpress.Data.Controls.ExpressionEditor;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.ExpressionEditor;
using System.Windows;
using ZeroPlus.Oms.Ui.ViewModels;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for CustomEdgeFunctionEditorView.xaml
    /// </summary>
    public partial class CustomEdgeFunctionEditorView : ThemedWindow
    {
        public CustomEdgeFunctionEditorView()
        {
            InitializeComponent();
            Loaded += View_Loaded;
        }

        private void View_Loaded(object sender, RoutedEventArgs e)
        {
            var context = ExpressionEditorContextHelper.GetContext(false, false, false);

            context.Functions.Clear();
            context.Constants.Clear();
            context.Columns.Clear();

            context.Functions.Add(new FunctionInfo("Func") { DisplayName = "Power", Name = "Power", ArgumentTypes = new[] { typeof(double), typeof(double) }, UsageSample = "Power(3, 2)", FunctionCategory = "Math", Description = "Returns a specified number raised to the specified power." });
            context.Functions.Add(new FunctionInfo("Func") { DisplayName = "Min", Name = "Min", ArgumentTypes = new[] { typeof(double), typeof(double) }, UsageSample = "Min(1, 2)", FunctionCategory = "Math", Description = "Returns the smaller of two numbers." });
            context.Functions.Add(new FunctionInfo("Func") { DisplayName = "Max", Name = "Max", ArgumentTypes = new[] { typeof(double), typeof(double) }, UsageSample = "Max(1, 2)", FunctionCategory = "Math", Description = "Returns the larger of two specified numbers." });
            context.Functions.Add(new FunctionInfo("Func") { DisplayName = "Round", Name = "Round", ArgumentTypes = new[] { typeof(double), typeof(double) }, UsageSample = "Round(3.222, 2)", FunctionCategory = "Math", Description = "Rounds a value to the nearest integer or specified number of decimal places. The mid number behaviour can be changed by using ExpressionOptions.RoundAwayFromZero during construction of the Expression object." });
            context.Functions.Add(new FunctionInfo("Func") { DisplayName = "Abs", Name = "Abs", ArgumentTypes = new[] { typeof(double) }, UsageSample = "Abs(-1)", FunctionCategory = "Math", Description = "Returns the absolute value of a specified number." });
            context.Functions.Add(new FunctionInfo("Func") { DisplayName = "Floor", Name = "Floor", ArgumentTypes = new[] { typeof(double) }, UsageSample = "Floor(1.5)", FunctionCategory = "Math", Description = "Returns the largest integer less than or equal to the specified number." });
            context.Functions.Add(new FunctionInfo("Func") { DisplayName = "Ceiling", Name = "Ceiling", ArgumentTypes = new[] { typeof(double) }, UsageSample = "Ceiling(1.5)", FunctionCategory = "Math", Description = "Returns the smallest integer greater than or equal to the specified number." });
            context.Functions.Add(new FunctionInfo("Func") { DisplayName = "iif", Name = "iif", ArgumentTypes = new[] { typeof(double) }, UsageSample = "iif(3 % 2 = 1, 'value is true', 'value is false')", FunctionCategory = "Logic", Description = "Returns a value based on a condition." });

            context.Columns.Add(new ColumnInfo("Greeks") { Name = "Delta", Description = "Delta", Type = typeof(double) });
            context.Columns.Add(new ColumnInfo("Greeks") { Name = "Gamma", Description = "Gamma", Type = typeof(double) });
            context.Columns.Add(new ColumnInfo("Greeks") { Name = "Vega", Description = "Vega", Type = typeof(double) });
            context.Columns.Add(new ColumnInfo("Greeks") { Name = "Rho", Description = "Rho", Type = typeof(double) });
            context.Columns.Add(new ColumnInfo("Greeks") { Name = "IV", Description = "IV", Type = typeof(double) });

            context.Columns.Add(new ColumnInfo("Option Pricing") { Name = "HwTheo", Description = "Raw Hanweck Theo", Type = typeof(double) });
            context.Columns.Add(new ColumnInfo("Option Pricing") { Name = "AdjTheo", Description = "Delta Adj Hanweck Theo", Type = typeof(double) });

            context.Columns.Add(new ColumnInfo("Option Pricing") { Name = "BidEma", Description = "Bid Ema", Type = typeof(double) });
            context.Columns.Add(new ColumnInfo("Option Pricing") { Name = "MidEma", Description = "Mid Ema", Type = typeof(double) });
            context.Columns.Add(new ColumnInfo("Option Pricing") { Name = "AskEma", Description = "Ask Ema", Type = typeof(double) });

            context.Columns.Add(new ColumnInfo("Market") { Name = "Bid", Description = "Market Bid", Type = typeof(double) });
            context.Columns.Add(new ColumnInfo("Market") { Name = "Mid", Description = "Market Mid", Type = typeof(double) });
            context.Columns.Add(new ColumnInfo("Market") { Name = "Ask", Description = "Market Ask", Type = typeof(double) });

            context.Columns.Add(new ColumnInfo("Market") { Name = "UnderBid", Description = "Underlying Bid", Type = typeof(double) });
            context.Columns.Add(new ColumnInfo("Market") { Name = "UnderMid", Description = "Underlying Mid", Type = typeof(double) });
            context.Columns.Add(new ColumnInfo("Market") { Name = "UnderAsk", Description = "Underlying Ask", Type = typeof(double) });

            context.Columns.Add(new ColumnInfo("Option") { Name = "DTE", Description = "Days To Expiration", Type = typeof(double) });
            context.Columns.Add(new ColumnInfo("Option") { Name = "CallPut", Description = "Option Type", Type = typeof(string) });
            context.Columns.Add(new ColumnInfo("Option") { Name = "Strike", Description = "Leg 1 Strike", Type = typeof(string) });

            editor.Context = context;
        }

        private void OnSave(object _, RoutedEventArgs e)
        {
            editor.SaveCommand.Execute(default);
            if (DataContext is CustomEdgeFunctionEditorViewModel viewModel)
            {
                viewModel.Save();
            }
        }
    }
}
