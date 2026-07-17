using WashUp.Data;
using WashUp.Models;
using Microsoft.EntityFrameworkCore;

namespace WashUp.Services;

/// <summary>
/// Service for automatic invoice generation and management
/// </summary>
public class InvoiceService
{
    private readonly AppDbContext _db;

    public InvoiceService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Generate invoice for an order
    /// </summary>
    public async Task<Invoice> GenerateInvoiceAsync(int orderId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null) throw new ArgumentException("Order not found");

        // Check if invoice already exists
        var existing = await _db.Invoices.FirstOrDefaultAsync(i => i.OrderId == orderId);
        if (existing != null) return existing;

        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{orderId:D4}";
        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNumber,
            OrderId = orderId,
            Subtotal = order.Subtotal,
            Discount = order.Discount,
            TaxAmount = order.TaxAmount,
            TotalAmount = order.TotalAmount,
            PaymentStatus = order.PaymentStatus == "Lunas" ? "Paid" : "Unpaid",
            PaymentMethod = order.PaymentMethod,
            DueDate = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();
        return invoice;
    }

    /// <summary>
    /// Calculate PPh (income tax) - 10% of taxable income
    /// </summary>
    public decimal CalculatePph(decimal taxableAmount)
    {
        return taxableAmount * 0.1m;
    }

    /// <summary>
    /// Get monthly financial summary
    /// </summary>
    public async Task<object> GetMonthlySummaryAsync(int? branchId = null)
    {
        var thisMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var query = _db.FinancialTransactions.AsQueryable();
        if (branchId.HasValue)
            query = query.Where(t => t.BranchId == branchId);

        var monthTransactions = await query
            .Where(t => t.TransactionDate >= thisMonth)
            .ToListAsync();

        return new
        {
            Month = thisMonth.ToString("yyyy-MM"),
            TotalIncome = monthTransactions.Where(t => t.TransactionType == "Income").Sum(t => t.Amount),
            TotalExpense = monthTransactions.Where(t => t.TransactionType == "Expense").Sum(t => t.Amount),
            TransactionCount = monthTransactions.Count
        };
    }
}
