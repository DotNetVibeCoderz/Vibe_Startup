using Microsoft.EntityFrameworkCore;
using QRCoder;
using Bioskop.Data;
using Bioskop.Models;

namespace Bioskop.Services;

/// <summary>
/// Service untuk generate QR code dan manajemen tiket
/// </summary>
public class TicketService
{
    private readonly ApplicationDbContext _context;

    public TicketService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Generate QR Code image untuk tiket
    /// </summary>
    public string GenerateQrCodeImage(string qrData)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(qrCodeData);
        byte[] qrCodeBytes = pngQrCode.GetGraphic(20);
        return Convert.ToBase64String(qrCodeBytes);
    }

    /// <summary>
    /// Scan/validate tiket by QR Code data
    /// </summary>
    public async Task<(Ticket? ticket, string? message)> ValidateTicketByQrAsync(string qrCode)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Order!).ThenInclude(o => o.Showtime!).ThenInclude(s => s.Movie)
            .Include(t => t.Order!).ThenInclude(o => o.Showtime!).ThenInclude(s => s.Studio)
            .Include(t => t.Seat)
            .FirstOrDefaultAsync(t => t.QrCode == qrCode);

        if (ticket == null) return (null, "Tiket tidak ditemukan");
        if (ticket.Status == "Used") return (null, "Tiket sudah digunakan");
        if (ticket.Status == "Cancelled") return (null, "Tiket sudah dibatalkan");

        var showtime = ticket.Order?.Showtime;
        if (showtime == null) return (null, "Jadwal tidak ditemukan");

        // Check if too early (max 2 hours before show)
        if (DateTime.UtcNow < showtime.StartTime.AddHours(-2))
            return (null, $"Belum bisa check-in. Check-in dibuka mulai {showtime.StartTime.AddHours(-2):HH:mm}");

        // Check if too late (after show ended)
        if (DateTime.UtcNow > showtime.EndTime)
            return (null, "Showtime sudah berakhir");

        // Mark as used
        ticket.Status = "Used";
        ticket.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (ticket, "Tiket valid! Selamat menonton!");
    }

    public async Task<List<Ticket>> GetTicketsByOrderAsync(int orderId)
    {
        return await _context.Tickets
            .Include(t => t.Seat)
            .Where(t => t.OrderId == orderId)
            .ToListAsync();
    }

    public async Task<Ticket?> GetTicketByQrAsync(string qrCode)
    {
        return await _context.Tickets
            .Include(t => t.Order!).ThenInclude(o => o.Showtime!).ThenInclude(s => s.Movie)
            .Include(t => t.Order!).ThenInclude(o => o.Showtime!).ThenInclude(s => s.Studio)
            .Include(t => t.Seat)
            .FirstOrDefaultAsync(t => t.QrCode == qrCode);
    }

    /// <summary>
    /// Generate tiket yang siap diprint (HTML)
    /// </summary>
    public string GeneratePrintableTicket(Ticket ticket)
    {
        var qrImage = GenerateQrCodeImage(ticket.QrCode);
        var movie = ticket.Order?.Showtime?.Movie;
        var studio = ticket.Order?.Showtime?.Studio;
        var showtime = ticket.Order?.Showtime;
        var seat = ticket.Seat;

        return $@"
<!DOCTYPE html>
<html><head><title>Tiket - {movie?.Title}</title>
<style>
    body {{ font-family: 'Segoe UI', sans-serif; margin:0; padding:20px; }}
    .ticket {{ max-width:400px; margin:auto; border:2px dashed #333; padding:20px; border-radius:10px; }}
    .ticket h2 {{ text-align:center; color:#e74c3c; margin-bottom:5px; }}
    .ticket .movie {{ font-size:1.4em; font-weight:bold; text-align:center; }}
    .ticket .info {{ margin:15px 0; }}
    .ticket .info table {{ width:100%; }}
    .ticket .info td {{ padding:5px; }}
    .ticket .info td:first-child {{ font-weight:bold; width:40%; }}
    .ticket .qr {{ text-align:center; margin:15px 0; }}
    .ticket .qr img {{ width:150px; height:150px; }}
    .ticket .footer {{ text-align:center; margin-top:15px; font-size:0.8em; color:#666; }}
</style></head><body>
<div class='ticket'>
    <h2>🎬 BIOSKOP</h2>
    <div class='movie'>{movie?.Title ?? "N/A"}</div>
    <div class='info'>
        <table>
            <tr><td>No. Tiket</td><td>: {ticket.TicketNumber}</td></tr>
            <tr><td>No. Order</td><td>: {ticket.Order?.OrderNumber}</td></tr>
            <tr><td>Studio</td><td>: {studio?.Name}</td></tr>
            <tr><td>Kursi</td><td>: {seat?.RowLabel}{seat?.ColumnNumber} ({seat?.SeatType})</td></tr>
            <tr><td>Tanggal</td><td>: {showtime?.StartTime:dd MMM yyyy}</td></tr>
            <tr><td>Jam</td><td>: {showtime?.StartTime:HH:mm} - {showtime?.EndTime:HH:mm}</td></tr>
            <tr><td>Tipe</td><td>: {showtime?.ShowType}</td></tr>
            <tr><td>Harga</td><td>: Rp {ticket.Price:N0}</td></tr>
        </table>
    </div>
    <div class='qr'><img src='data:image/png;base64,{qrImage}' alt='QR Code'/></div>
    <div class='footer'>Tunjukkan QR ini di pintu masuk.<br/>Terima kasih telah menonton di Bioskop!</div>
</div></body></html>";
    }
}
