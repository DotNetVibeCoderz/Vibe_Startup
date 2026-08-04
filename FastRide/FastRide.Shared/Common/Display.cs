using System.Globalization;
using FastRide.Shared.Models;

namespace FastRide.Shared.Common;

/// <summary>
/// Formatting and the status→signal-colour mapping, shared by the admin console and both
/// mobile apps so a status never means one thing on the dashboard and another on a phone.
///
/// The class names it returns belong to the FastRide design system, which all three
/// front-ends implement identically.
/// </summary>
public static class Display
{
    private static readonly CultureInfo Indonesia = ResolveCulture();

    /// <summary>Falls back to the invariant culture on trimmed or ICU-less targets.</summary>
    private static CultureInfo ResolveCulture()
    {
        try
        {
            return CultureInfo.GetCultureInfo("id-ID");
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    public static string Rupiah(decimal amount) => "Rp " + amount.ToString("N0", Indonesia);

    public static string RupiahShort(decimal amount) => amount switch
    {
        >= 1_000_000_000 => $"Rp {amount / 1_000_000_000:0.#} M",
        >= 1_000_000 => $"Rp {amount / 1_000_000:0.#} jt",
        >= 1_000 => $"Rp {amount / 1_000:0.#} rb",
        _ => "Rp " + amount.ToString("N0", Indonesia)
    };

    public static string Number(int value) => value.ToString("N0", Indonesia);

    public static string Date(DateTime value) => value.ToLocalTime().ToString("dd MMM yyyy", Indonesia);

    public static string DateTimeShort(DateTime value) => value.ToLocalTime().ToString("dd MMM · HH:mm", Indonesia);

    public static string Clock(DateTime value) => value.ToLocalTime().ToString("HH:mm:ss", Indonesia);

    public static string Distance(double km) => km.ToString("0.0", Indonesia) + " km";

    /// <summary>Relative age, for "how long has this order been waiting".</summary>
    public static string Since(DateTime value)
    {
        var elapsed = DateTime.UtcNow - value;

        return elapsed switch
        {
            { TotalSeconds: < 60 } => $"{Math.Max(0, (int)elapsed.TotalSeconds)} dtk lalu",
            { TotalMinutes: < 60 } => $"{(int)elapsed.TotalMinutes} mnt lalu",
            { TotalHours: < 24 } => $"{(int)elapsed.TotalHours} jam lalu",
            { TotalDays: < 30 } => $"{(int)elapsed.TotalDays} hari lalu",
            _ => Date(value)
        };
    }

    // ─────────────────── status → signal semantics ───────────────────

    public static string PillClass(OrderStatus status) => status switch
    {
        OrderStatus.Requested => "pill pill--wait",
        OrderStatus.Accepted or OrderStatus.DriverArrived or OrderStatus.Started => "pill pill--move",
        OrderStatus.Completed => "pill pill--go",
        OrderStatus.Cancelled or OrderStatus.Expired => "pill pill--stop",
        _ => "pill pill--idle"
    };

    public static string Label(OrderStatus status) => status switch
    {
        OrderStatus.Requested => "Menunggu",
        OrderStatus.Accepted => "Diterima",
        OrderStatus.DriverArrived => "Driver tiba",
        OrderStatus.Started => "Berjalan",
        OrderStatus.Completed => "Selesai",
        OrderStatus.Cancelled => "Dibatalkan",
        OrderStatus.Expired => "Kedaluwarsa",
        _ => status.ToString()
    };

    public static string PillClass(DriverStatus status) => status switch
    {
        DriverStatus.Online => "pill pill--go",
        DriverStatus.OnTrip => "pill pill--wait",
        DriverStatus.Break => "pill pill--move",
        _ => "pill pill--idle"
    };

    public static string Label(DriverStatus status) => status switch
    {
        DriverStatus.Online => "Online",
        DriverStatus.OnTrip => "Antar",
        DriverStatus.Break => "Istirahat",
        _ => "Offline"
    };

    public static string PipClass(DriverStatus status) => status switch
    {
        DriverStatus.Online => "pip pip--online",
        DriverStatus.OnTrip => "pip pip--ontrip",
        DriverStatus.Break => "pip pip--break",
        _ => "pip pip--offline"
    };

    /// <summary>
    /// A charge in flight is amber — it is waiting on the payer, which is exactly what amber
    /// means everywhere else in the product.
    /// </summary>
    public static string PillClass(PaymentStatus status) => status switch
    {
        PaymentStatus.Completed => "pill pill--go",
        PaymentStatus.Pending or PaymentStatus.AwaitingPayment => "pill pill--wait",
        PaymentStatus.Refunded => "pill pill--move",
        _ => "pill pill--stop"
    };

    public static string PillClass(DocumentStatus status) => status switch
    {
        DocumentStatus.Approved => "pill pill--go",
        DocumentStatus.Pending => "pill pill--wait",
        _ => "pill pill--stop"
    };

    public static string Label(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Tunai",
        PaymentMethod.EWallet => "E-Wallet",
        PaymentMethod.CreditCard => "Kartu Kredit",
        PaymentMethod.BankTransfer => "Transfer Bank",
        PaymentMethod.Qris => "QRIS",
        PaymentMethod.VirtualAccount => "Virtual Account",
        _ => method.ToString()
    };

    public static string Icon(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "bi bi-cash-stack",
        PaymentMethod.EWallet => "bi bi-wallet2",
        PaymentMethod.CreditCard => "bi bi-credit-card",
        PaymentMethod.BankTransfer => "bi bi-bank",
        PaymentMethod.Qris => "bi bi-qr-code",
        PaymentMethod.VirtualAccount => "bi bi-upc-scan",
        _ => "bi bi-cash"
    };

    public static string Label(EWalletChannel channel) => channel switch
    {
        EWalletChannel.GoPay => "GoPay",
        EWalletChannel.Ovo => "OVO",
        EWalletChannel.Dana => "DANA",
        EWalletChannel.ShopeePay => "ShopeePay",
        EWalletChannel.LinkAja => "LinkAja",
        _ => "Otomatis"
    };

    public static string Label(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "Menyiapkan",
        PaymentStatus.AwaitingPayment => "Menunggu bayar",
        PaymentStatus.Completed => "Lunas",
        PaymentStatus.Failed => "Gagal",
        PaymentStatus.Expired => "Kedaluwarsa",
        PaymentStatus.Refunded => "Dikembalikan",
        _ => status.ToString()
    };


    public static string Label(DocumentType type) => type switch
    {
        DocumentType.DriverLicense => "SIM",
        DocumentType.VehicleRegistration => "STNK",
        DocumentType.IdentityCard => "KTP",
        DocumentType.Insurance => "Asuransi",
        DocumentType.VehiclePhoto => "Foto Kendaraan",
        _ => type.ToString()
    };

    public static string Label(VehicleCategory category) => category switch
    {
        VehicleCategory.Economy => "Ekonomi",
        VehicleCategory.Comfort => "Nyaman",
        VehicleCategory.Premium => "Premium",
        VehicleCategory.Bike => "Motor",
        VehicleCategory.Electric => "Listrik",
        _ => category.ToString()
    };

    public static string Icon(VehicleCategory category) => category switch
    {
        VehicleCategory.Economy => "bi bi-car-front",
        VehicleCategory.Comfort => "bi bi-car-front-fill",
        VehicleCategory.Premium => "bi bi-taxi-front-fill",
        VehicleCategory.Bike => "bi bi-scooter",
        VehicleCategory.Electric => "bi bi-ev-front-fill",
        _ => "bi bi-car-front"
    };

    /// <summary>Chart.js series colour token for a status, matching the CSS variables.</summary>
    public static string ChartColour(OrderStatus status) => status switch
    {
        OrderStatus.Requested => "lampu",
        OrderStatus.Accepted or OrderStatus.DriverArrived or OrderStatus.Started => "lintas",
        OrderStatus.Completed => "jalan",
        _ => "sirene"
    };

    /// <summary>How far along the four-stop trip rail a status sits (0-3).</summary>
    public static int TripStep(OrderStatus status) => status switch
    {
        OrderStatus.Requested => 0,
        OrderStatus.Accepted => 1,
        OrderStatus.DriverArrived => 2,
        OrderStatus.Started => 3,
        OrderStatus.Completed => 4,
        _ => 0
    };

    /// <summary>Fallback avatar so a missing photo never renders as a broken image.</summary>
    public static string Avatar(string? url, string fullName)
    {
        if (!string.IsNullOrWhiteSpace(url)) return url;

        var initials = string.Concat(fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(word => char.ToUpperInvariant(word[0])));

        if (initials.Length == 0) initials = "FR";

        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='64' height='64'>" +
                  $"<rect width='64' height='64' rx='32' fill='#26324D'/>" +
                  $"<text x='32' y='42' font-size='26' font-family='Arial' font-weight='bold' " +
                  $"fill='#8FA0C0' text-anchor='middle'>{initials}</text></svg>";

        return "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
    }
}
