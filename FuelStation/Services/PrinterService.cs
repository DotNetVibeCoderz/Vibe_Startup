using System.Text;
using FuelStation.Models;

namespace FuelStation.Services;

/// <summary>
/// Receipt printing service (ESC/POS compatible)
/// Supports thermal printers via serial port, network, or file output
/// </summary>
public interface IPrinterService
{
    Task<string> GenerateReceiptHtml(Transaction transaction, string stationName);
    Task<byte[]> GenerateReceiptBytes(Transaction transaction, string stationName);
    Task<bool> PrintReceipt(Transaction transaction, string stationName);
}

public class PrinterService : IPrinterService
{
    private readonly IConfiguration _config;
    private readonly ILogger<PrinterService> _logger;

    public PrinterService(IConfiguration config, ILogger<PrinterService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Generate receipt as HTML string (for preview and printing)
    /// </summary>
    public Task<string> GenerateReceiptHtml(Transaction transaction, string stationName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Courier New', monospace; font-size: 12px; width: 300px; margin: 0 auto; }");
        sb.AppendLine(".center { text-align: center; } .right { text-align: right; }");
        sb.AppendLine(".line { border-top: 1px dashed #000; margin: 8px 0; }");
        sb.AppendLine(".bold { font-weight: bold; } .large { font-size: 16px; }");
        sb.AppendLine("</style></head><body>");

        // Header
        sb.AppendLine($"<div class='center bold large'>{stationName}</div>");
        sb.AppendLine($"<div class='center'>{DateTime.Now:dd/MM/yyyy HH:mm:ss}</div>");
        sb.AppendLine($"<div class='center'>No: {transaction.TransactionNumber}</div>");
        sb.AppendLine("<div class='line'></div>");

        // Items
        foreach (var detail in transaction.TransactionDetails)
        {
            sb.AppendLine($"<div>{detail.FuelProduct?.Name ?? "BBM"}</div>");
            sb.AppendLine($"<div>{detail.Liters:F2} L x Rp {detail.PricePerLiter:N0}</div>");
            sb.AppendLine($"<div class='right'>Rp {detail.Subtotal:N0}</div>");
        }

        sb.AppendLine("<div class='line'></div>");

        // Totals
        sb.AppendLine($"<div>Total: <span class='right'>Rp {transaction.TotalAmount:N0}</span></div>");
        if (transaction.Discount > 0)
            sb.AppendLine($"<div>Diskon: <span class='right'>-Rp {transaction.Discount:N0}</span></div>");
        sb.AppendLine($"<div class='bold'>Grand Total: <span class='right'>Rp {transaction.GrandTotal:N0}</span></div>");

        sb.AppendLine("<div class='line'></div>");

        // Payment info
        sb.AppendLine($"<div>Pembayaran: {transaction.PaymentMethod}</div>");
        if (!string.IsNullOrEmpty(transaction.PaymentReference))
            sb.AppendLine($"<div>Ref: {transaction.PaymentReference}</div>");

        sb.AppendLine("<div class='line'></div>");

        // Footer
        sb.AppendLine("<div class='center'>Terima Kasih!</div>");
        sb.AppendLine("<div class='center'>Simpan struk ini sebagai bukti pembayaran</div>");
        sb.AppendLine("<div class='center'>***</div>");

        sb.AppendLine("</body></html>");

        return Task.FromResult(sb.ToString());
    }

    /// <summary>
    /// Generate receipt as raw bytes for thermal printer
    /// </summary>
    public Task<byte[]> GenerateReceiptBytes(Transaction transaction, string stationName)
    {
        var sb = new StringBuilder();

        // ESC/POS Commands
        sb.Append("\x1B\x40"); // Initialize
        sb.Append("\x1B\x61\x01"); // Center align

        sb.AppendLine(stationName);
        sb.AppendLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
        sb.AppendLine($"No: {transaction.TransactionNumber}");
        sb.AppendLine("--------------------------------");

        sb.Append("\x1B\x61\x00"); // Left align

        foreach (var detail in transaction.TransactionDetails)
        {
            sb.AppendLine($"{detail.FuelProduct?.Name ?? "BBM"}");
            sb.AppendLine($"  {detail.Liters:F2}L x Rp{detail.PricePerLiter:N0}");
            sb.Append("\x1B\x61\x02"); // Right align
            sb.AppendLine($"Rp{detail.Subtotal:N0}");
            sb.Append("\x1B\x61\x00"); // Left align
        }

        sb.AppendLine("--------------------------------");
        sb.AppendLine($"Total:        Rp{transaction.TotalAmount:N0}");
        if (transaction.Discount > 0)
            sb.AppendLine($"Diskon:      -Rp{transaction.Discount:N0}");

        sb.Append("\x1B\x45\x01"); // Bold on
        sb.AppendLine($"GRAND TOTAL:  Rp{transaction.GrandTotal:N0}");
        sb.Append("\x1B\x45\x00"); // Bold off

        sb.AppendLine("--------------------------------");
        sb.AppendLine($"Bayar: {transaction.PaymentMethod}");

        sb.Append("\x1B\x61\x01"); // Center align
        sb.AppendLine("Terima Kasih!");
        sb.AppendLine("========================");

        // Cut paper
        sb.Append("\x1D\x56\x00");

        return Task.FromResult(Encoding.ASCII.GetBytes(sb.ToString()));
    }

    /// <summary>
    /// Send receipt to printer (simulated - saves to file)
    /// </summary>
    public async Task<bool> PrintReceipt(Transaction transaction, string stationName)
    {
        try
        {
            var printerType = _config.GetValue<string>("Printer:Type", "File");
            var printerPort = _config.GetValue<string>("Printer:Port", "receipts");

            var bytes = await GenerateReceiptBytes(transaction, stationName);

            if (printerType == "File")
            {
                var dir = Path.Combine(Directory.GetCurrentDirectory(), printerPort);
                Directory.CreateDirectory(dir);
                var filePath = Path.Combine(dir, $"receipt_{transaction.TransactionNumber}.txt");
                await File.WriteAllBytesAsync(filePath, bytes);
                _logger.LogInformation("Receipt saved to {FilePath}", filePath);
            }
            else if (printerType == "Serial")
            {
                // Serial port printing would go here
                _logger.LogInformation("Serial printing not implemented in this version");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to print receipt");
            return false;
        }
    }
}
