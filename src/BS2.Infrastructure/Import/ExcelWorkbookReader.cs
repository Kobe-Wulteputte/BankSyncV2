using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace BS2.Infrastructure.Import;

public sealed record ImportedRow(
    string Sheet, int RowNumber, string BankName, decimal Amount, DateOnly Date, string Account, string Name,
    string Category, string Group, bool Reimbursed, string Description, string ExternalId);

public sealed record WorkbookReadResult(IReadOnlyList<ImportedRow> Rows, IReadOnlyList<string> Errors);

/// <summary>Reads the V1 workbook: one sheet per year, header row, ten fixed columns.</summary>
public static class ExcelWorkbookReader
{
    private static readonly string[] DateFormats = ["dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy-MM-dd"];

    public static WorkbookReadResult Read(Stream xlsx)
    {
        var rows = new List<ImportedRow>();
        var errors = new List<string>();
        using var wb = new XLWorkbook(xlsx);

        foreach (var ws in wb.Worksheets.Where(w => Regex.IsMatch(w.Name, @"^\d{4}$")))
        {
            var last = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 2; r <= last; r++)
            {
                var id = Text(ws.Cell(r, 10));
                var amountCell = ws.Cell(r, 2);
                if (id.Length == 0 && amountCell.IsEmpty()) continue;

                var where = $"{ws.Name}!{r}";
                decimal amount;
                if (amountCell.IsEmpty() || Text(amountCell).Length == 0)
                {
                    // Rows with an id but no amount exist in the workbook (manual bookkeeping lines); keep them at 0.
                    amount = 0;
                    errors.Add($"{where}: empty amount, imported as 0");
                }
                else if (!TryAmount(amountCell, out amount)) { errors.Add($"{where}: invalid amount '{Text(amountCell)}'"); continue; }
                if (!TryDate(ws.Cell(r, 3), out var date)) { errors.Add($"{where}: invalid date '{Text(ws.Cell(r, 3))}'"); continue; }

                rows.Add(new ImportedRow(ws.Name, r, Text(ws.Cell(r, 1)), amount, date, Text(ws.Cell(r, 4)),
                    Text(ws.Cell(r, 5)), Text(ws.Cell(r, 6)), Text(ws.Cell(r, 7)),
                    Text(ws.Cell(r, 8)).Equals("TRUE", StringComparison.OrdinalIgnoreCase),
                    Text(ws.Cell(r, 9)), id));
            }
        }
        return new WorkbookReadResult(rows, errors);
    }

    private static string Text(IXLCell c) => c.IsEmpty() ? "" : c.GetString().Trim();

    private static bool TryAmount(IXLCell c, out decimal amount)
    {
        amount = 0;
        if (c.IsEmpty()) return false;
        if (c.DataType == XLDataType.Number) { amount = (decimal)c.GetDouble(); return true; }
        return decimal.TryParse(c.GetString().Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out amount);
    }

    private static bool TryDate(IXLCell c, out DateOnly date)
    {
        date = default;
        if (c.IsEmpty()) return false;
        if (c.DataType == XLDataType.DateTime) { date = DateOnly.FromDateTime(c.GetDateTime()); return true; }
        return DateOnly.TryParseExact(c.GetString().Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
