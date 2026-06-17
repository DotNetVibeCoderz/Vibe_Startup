namespace Bioskop.Models;

/// <summary>
/// Join table: Snack yang dibeli dalam satu order
/// </summary>
public class OrderSnack
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int SnackId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; } // Harga saat pembelian (snapshot)
    public decimal Subtotal { get; set; } // UnitPrice * Quantity

    // Navigation
    public Order? Order { get; set; }
    public Snack? Snack { get; set; }
}
