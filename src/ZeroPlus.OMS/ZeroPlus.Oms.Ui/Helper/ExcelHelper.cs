using DevExpress.Spreadsheet;
using NLog;
using System;
using System.Threading.Tasks;

namespace ZeroPlus.Oms.Ui.Helper
{
    public static class ExcelHelper
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public static async Task<object[,]> ReadExcelFileAsync(string file, System.Threading.CancellationToken cancellationToken, IProgress<double> progressIndicator)
        {
            return await Task.Run(() => ReadExcelFile(file, cancellationToken, progressIndicator));
        }

        public static object[,] ReadExcelFile(string file, System.Threading.CancellationToken cancellationToken = default, IProgress<double> progressIndicator = null)
        {
            object[,] values = new object[0, 0];
            try
            {
                using Workbook workbook = new();
                workbook.LoadDocument(file);
                Worksheet worksheet = workbook.Worksheets[0];
                CellRange ranges = worksheet.GetUsedRange();

                values = new object[ranges.RowCount, ranges.ColumnCount];
                foreach (Cell cell in ranges)
                {
                    int rowIndex = cell.RowIndex;
                    int columnIndex = cell.ColumnIndex;
                    values[rowIndex, columnIndex] = cell.Value.ToObject();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReadExcelFile));
            }
            return values;
        }
    }
}