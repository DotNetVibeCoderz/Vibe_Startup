// Kernel functions that let Mas Bolang read Joka's own inventory.
// Every function returns compact Markdown so the chat page can render the
// answer as a table without the model having to reformat it.
using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Joka.Data;

namespace Joka.Services.Chat;

public class JokaDataFunctions
{
    private const int MaxRows = 8;
    private static readonly CultureInfo Id = new("id-ID");

    /// <summary>
    /// People ask for "Bali" and "Jogja". The data is inconsistent about it:
    /// airports store "Denpasar" while hotels store "Bali". So a search matches
    /// the word the user typed OR its alias, never one replacing the other.
    /// </summary>
    private static readonly Dictionary<string, string> CityAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bali"] = "Denpasar",
        ["denpasar"] = "Bali",
        ["kuta"] = "Bali",
        ["seminyak"] = "Bali",
        ["jogja"] = "Yogyakarta",
        ["jogjakarta"] = "Yogyakarta",
        ["yogya"] = "Yogyakarta",
        ["surakarta"] = "Solo",
        ["ibukota"] = "Jakarta",
        ["jkt"] = "Jakarta",
        ["bdg"] = "Bandung",
        ["sby"] = "Surabaya"
    };

    /// <summary>
    /// Returns the trimmed search term plus an alternative spelling to match
    /// alongside it. The alias falls back to the term itself so the generated
    /// SQL always has the same shape.
    /// </summary>
    private static (string? Term, string Alias) CityTerms(string? input)
    {
        var term = input?.Trim();
        if (string.IsNullOrEmpty(term)) return (null, string.Empty);

        return (term, CityAliases.TryGetValue(term, out var alias) ? alias : term);
    }

    private readonly AppDbContext _db;

    public JokaDataFunctions(AppDbContext db) => _db = db;

    // ----------------------------------------------------------------
    // Flights
    // ----------------------------------------------------------------
    [KernelFunction("cari_penerbangan")]
    [Description("Cari tiket pesawat yang tersedia di Joka berdasarkan kota/bandara asal, tujuan, tanggal, dan batas harga.")]
    public async Task<string> SearchFlightsAsync(
        [Description("Kota atau kode bandara asal, misalnya 'Jakarta' atau 'CGK'. Kosongkan untuk semua.")] string? asal = null,
        [Description("Kota atau kode bandara tujuan, misalnya 'Bali' atau 'DPS'. Kosongkan untuk semua.")] string? tujuan = null,
        [Description("Tanggal berangkat format yyyy-MM-dd. Kosongkan untuk semua tanggal.")] string? tanggal = null,
        [Description("Harga maksimum dalam Rupiah. 0 berarti tanpa batas.")] decimal hargaMaksimum = 0)
    {
        var query = _db.Flights.AsNoTracking()
            .Include(f => f.Airline)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .Where(f => f.AvailableSeats > 0);

        var (from, fromAlias) = CityTerms(asal);
        var (to, toAlias) = CityTerms(tujuan);

        if (from is not null)
            query = query.Where(f => f.DepartureAirport!.Code == from
                || f.DepartureAirport!.City.Contains(from)
                || f.DepartureAirport!.Name.Contains(from)
                || f.DepartureAirport!.City.Contains(fromAlias));

        if (to is not null)
            query = query.Where(f => f.ArrivalAirport!.Code == to
                || f.ArrivalAirport!.City.Contains(to)
                || f.ArrivalAirport!.Name.Contains(to)
                || f.ArrivalAirport!.City.Contains(toAlias));

        if (TryParseDate(tanggal, out var day))
            query = query.Where(f => f.DepartureTime.Date == day);

        if (hargaMaksimum > 0)
            query = query.Where(f => f.BasePrice <= hargaMaksimum);

        var rows = await query.OrderBy(f => f.BasePrice).Take(MaxRows).ToListAsync();
        if (rows.Count == 0) return "Tidak ada penerbangan yang cocok dengan kriteria itu.";

        var table = new StringBuilder("| Maskapai | Nomor | Rute | Berangkat | Durasi | Harga | Sisa kursi |\n");
        table.AppendLine("|---|---|---|---|---|---|---|");

        foreach (var f in rows)
        {
            table.AppendLine($"| {f.Airline?.Name} | {f.FlightNumber} " +
                $"| {f.DepartureAirport?.Code}→{f.ArrivalAirport?.Code} " +
                $"| {f.DepartureTime:dd MMM HH:mm} | {Duration(f.DurationMinutes)} " +
                $"| {Rupiah(f.BasePrice)} | {f.AvailableSeats} |");
        }

        return table.ToString();
    }

    // ----------------------------------------------------------------
    // Trains
    // ----------------------------------------------------------------
    [KernelFunction("cari_kereta")]
    [Description("Cari jadwal tiket kereta api di Joka berdasarkan kota/stasiun asal dan tujuan.")]
    public async Task<string> SearchTrainsAsync(
        [Description("Kota atau kode stasiun asal, misalnya 'Jakarta' atau 'GMR'.")] string? asal = null,
        [Description("Kota atau kode stasiun tujuan, misalnya 'Surabaya' atau 'SBY'.")] string? tujuan = null,
        [Description("Tanggal berangkat format yyyy-MM-dd.")] string? tanggal = null)
    {
        var query = _db.TrainSchedules.AsNoTracking()
            .Include(t => t.Train)
            .Include(t => t.DepartureStation)
            .Include(t => t.ArrivalStation)
            .Where(t => t.AvailableSeats > 0);

        var (from, fromAlias) = CityTerms(asal);
        var (to, toAlias) = CityTerms(tujuan);

        if (from is not null)
            query = query.Where(t => t.DepartureStation!.Code == from
                || t.DepartureStation!.City.Contains(from)
                || t.DepartureStation!.Name.Contains(from)
                || t.DepartureStation!.City.Contains(fromAlias));

        if (to is not null)
            query = query.Where(t => t.ArrivalStation!.Code == to
                || t.ArrivalStation!.City.Contains(to)
                || t.ArrivalStation!.Name.Contains(to)
                || t.ArrivalStation!.City.Contains(toAlias));

        if (TryParseDate(tanggal, out var day))
            query = query.Where(t => t.DepartureTime.Date == day);

        var rows = await query.OrderBy(t => t.BasePrice).Take(MaxRows).ToListAsync();
        if (rows.Count == 0) return "Tidak ada jadwal kereta yang cocok dengan kriteria itu.";

        var table = new StringBuilder("| Kereta | Kelas | Rute | Berangkat | Durasi | Harga | Sisa kursi |\n");
        table.AppendLine("|---|---|---|---|---|---|---|");

        foreach (var t in rows)
        {
            table.AppendLine($"| {t.Train?.Name} | {t.Train?.Class} " +
                $"| {t.DepartureStation?.Code}→{t.ArrivalStation?.Code} " +
                $"| {t.DepartureTime:dd MMM HH:mm} | {Duration(t.DurationMinutes)} " +
                $"| {Rupiah(t.BasePrice)} | {t.AvailableSeats} |");
        }

        return table.ToString();
    }

    // ----------------------------------------------------------------
    // Buses & shuttles
    // ----------------------------------------------------------------
    [KernelFunction("cari_bus")]
    [Description("Cari tiket bus antar kota dan shuttle door-to-door di Joka berdasarkan kota asal dan tujuan.")]
    public async Task<string> SearchBusesAsync(
        [Description("Kota atau kode terminal asal, misalnya 'Jakarta' atau 'PLG'.")] string? asal = null,
        [Description("Kota atau kode terminal tujuan, misalnya 'Bandung' atau 'LBA'.")] string? tujuan = null,
        [Description("Jenis armada: 'Bus' atau 'Shuttle'. Kosongkan untuk keduanya.")] string? jenis = null,
        [Description("Tanggal berangkat format yyyy-MM-dd.")] string? tanggal = null)
    {
        var query = _db.BusSchedules.AsNoTracking()
            .Include(s => s.BusService!).ThenInclude(b => b.Operator)
            .Include(s => s.DepartureTerminal)
            .Include(s => s.ArrivalTerminal)
            .Where(s => s.IsActive && s.AvailableSeats > 0);

        var (from, fromAlias) = CityTerms(asal);
        var (to, toAlias) = CityTerms(tujuan);

        if (from is not null)
            query = query.Where(s => s.DepartureTerminal!.Code == from
                || s.DepartureTerminal!.City.Contains(from)
                || s.DepartureTerminal!.Name.Contains(from)
                || s.DepartureTerminal!.City.Contains(fromAlias));

        if (to is not null)
            query = query.Where(s => s.ArrivalTerminal!.Code == to
                || s.ArrivalTerminal!.City.Contains(to)
                || s.ArrivalTerminal!.Name.Contains(to)
                || s.ArrivalTerminal!.City.Contains(toAlias));

        if (!string.IsNullOrWhiteSpace(jenis))
            query = query.Where(s => s.BusService!.ServiceType == jenis);

        if (TryParseDate(tanggal, out var day))
            query = query.Where(s => s.DepartureTime.Date == day);

        var rows = await query.OrderBy(s => s.BasePrice).Take(MaxRows).ToListAsync();
        if (rows.Count == 0) return "Tidak ada keberangkatan bus atau shuttle yang cocok dengan kriteria itu.";

        var table = new StringBuilder("| Operator | Jenis | Kelas | Rute | Berangkat | Durasi | Harga | Sisa kursi |\n");
        table.AppendLine("|---|---|---|---|---|---|---|---|");

        foreach (var s in rows)
        {
            table.AppendLine($"| {s.BusService?.Operator?.Name} | {s.BusService?.ServiceType} | {s.BusService?.Class} " +
                $"| {s.DepartureTerminal?.City}→{s.ArrivalTerminal?.City} " +
                $"| {s.DepartureTime:dd MMM HH:mm} | {Duration(s.DurationMinutes)} " +
                $"| {Rupiah(s.BasePrice)} | {s.AvailableSeats} |");
        }

        return table.ToString();
    }

    // ----------------------------------------------------------------
    // Hotels
    // ----------------------------------------------------------------
    [KernelFunction("cari_hotel")]
    [Description("Cari hotel, villa, resor, atau apartemen di Joka berdasarkan kota, bintang minimum, dan batas harga per malam.")]
    public async Task<string> SearchHotelsAsync(
        [Description("Nama kota, misalnya 'Bali' atau 'Yogyakarta'.")] string? kota = null,
        [Description("Rating bintang minimum 1-5. 0 berarti bebas.")] int bintangMinimum = 0,
        [Description("Harga maksimum per malam dalam Rupiah. 0 berarti tanpa batas.")] decimal hargaMaksimum = 0)
    {
        var query = _db.Hotels.AsNoTracking().Include(h => h.Rooms).AsQueryable();

        var (city, cityAlias) = CityTerms(kota);
        if (city is not null) query = query.Where(h => h.City.Contains(city) || h.City.Contains(cityAlias));
        if (bintangMinimum > 0) query = query.Where(h => h.StarRating >= bintangMinimum);

        var hotels = await query.OrderByDescending(h => h.AverageRating).Take(MaxRows * 2).ToListAsync();

        // Cheapest-room filtering happens here because it depends on the loaded rooms.
        var rows = hotels
            .Select(h => new { Hotel = h, MinPrice = h.Rooms.Count == 0 ? 0m : h.Rooms.Min(r => r.PricePerNight) })
            .Where(x => hargaMaksimum <= 0 || (x.MinPrice > 0 && x.MinPrice <= hargaMaksimum))
            .Take(MaxRows)
            .ToList();

        if (rows.Count == 0) return "Tidak ada hotel yang cocok dengan kriteria itu.";

        var table = new StringBuilder("| Hotel | Tipe | Kota | Bintang | Rating | Mulai dari |\n");
        table.AppendLine("|---|---|---|---|---|---|");

        foreach (var x in rows)
        {
            var price = x.MinPrice > 0 ? $"{Rupiah(x.MinPrice)}/malam" : "—";
            table.AppendLine($"| {x.Hotel.Name} | {x.Hotel.Type} | {x.Hotel.City} " +
                $"| {x.Hotel.StarRating}★ | {x.Hotel.AverageRating:F1} ({x.Hotel.ReviewCount} ulasan) | {price} |");
        }

        return table.ToString();
    }

    // ----------------------------------------------------------------
    // Activities, packages, promos, insurance
    // ----------------------------------------------------------------
    [KernelFunction("cari_aktivitas")]
    [Description("Cari tiket aktivitas, event, konser, atau atraksi wisata di Joka berdasarkan kota dan kategori.")]
    public async Task<string> SearchActivitiesAsync(
        [Description("Nama kota.")] string? kota = null,
        [Description("Kategori, misalnya 'Concert', 'Tour', 'Attraction'.")] string? kategori = null)
    {
        var query = _db.Activities.AsNoTracking().AsQueryable();

        var (city, cityAlias) = CityTerms(kota);
        if (city is not null) query = query.Where(a => a.City.Contains(city) || a.City.Contains(cityAlias));
        if (!string.IsNullOrWhiteSpace(kategori)) query = query.Where(a => a.Category == kategori);

        var rows = await query.Take(MaxRows).ToListAsync();
        if (rows.Count == 0) return "Tidak ada aktivitas yang cocok dengan kriteria itu.";

        var table = new StringBuilder("| Aktivitas | Kategori | Kota | Harga |\n|---|---|---|---|\n");
        foreach (var a in rows)
            table.AppendLine($"| {a.Name} | {a.Category} | {a.City} | {Rupiah(a.Price)} |");

        return table.ToString();
    }

    [KernelFunction("cari_paket_travel")]
    [Description("Lihat paket travel bundling (tiket + hotel + aktivitas) yang ditawarkan Joka.")]
    public async Task<string> SearchPackagesAsync(
        [Description("Nama destinasi, misalnya 'Bali'. Kosongkan untuk semua.")] string? destinasi = null)
    {
        var query = _db.TravelPackages.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(destinasi)) query = query.Where(p => p.Destination.Contains(destinasi));

        var rows = await query.OrderBy(p => p.Price).Take(MaxRows).ToListAsync();
        if (rows.Count == 0) return "Tidak ada paket travel yang cocok.";

        var table = new StringBuilder("| Paket | Destinasi | Durasi | Harga |\n|---|---|---|---|\n");
        foreach (var p in rows)
            table.AppendLine($"| {p.Name} | {p.Destination} | {p.DurationDays} hari | {Rupiah(p.Price)} |");

        return table.ToString();
    }

    [KernelFunction("lihat_promo")]
    [Description("Lihat kode voucher dan promo Joka yang masih aktif dan kuotanya belum habis.")]
    public async Task<string> ListPromosAsync()
    {
        var now = DateTime.UtcNow;
        var rows = await _db.PromoVouchers.AsNoTracking()
            .Where(v => v.IsActive && v.ValidFrom <= now && v.ValidUntil >= now && v.UsedCount < v.TotalQuota)
            .Take(MaxRows).ToListAsync();

        if (rows.Count == 0) return "Sedang tidak ada promo aktif.";

        var table = new StringBuilder("| Kode | Promo | Potongan | Min. transaksi | Berlaku sampai |\n|---|---|---|---|---|\n");
        foreach (var v in rows)
        {
            var value = v.Type == "Percentage" ? $"{v.Value:0.#}%" : Rupiah(v.Value);
            table.AppendLine($"| `{v.Code}` | {v.Name} | {value} | {Rupiah(v.MinPurchase)} | {v.ValidUntil:dd MMM yyyy} |");
        }

        return table.ToString();
    }

    [KernelFunction("lihat_asuransi")]
    [Description("Lihat pilihan paket asuransi perjalanan yang tersedia di Joka beserta harganya.")]
    public async Task<string> ListInsuranceAsync()
    {
        var rows = await _db.TravelInsurances.AsNoTracking().Where(i => i.IsActive).Take(MaxRows).ToListAsync();
        if (rows.Count == 0) return "Belum ada produk asuransi yang aktif.";

        var table = new StringBuilder("| Paket | Penyedia | Cakupan | Harga |\n|---|---|---|---|\n");
        foreach (var i in rows)
            table.AppendLine($"| {i.Name} | {i.Provider} | {i.Coverage} | {Rupiah(i.Price)} |");

        return table.ToString();
    }

    [KernelFunction("cari_transportasi_lokal")]
    [Description("Cari ojek, mobil online, atau antar-jemput bandara di Joka. Tarif per kilometer dihitung dari jarak yang diberikan; antar-jemput bandara memakai harga rute tetap.")]
    public async Task<string> SearchTransportAsync(
        [Description("Nama kota, misalnya 'Jakarta' atau 'Bali'.")] string? kota = null,
        [Description("Jenis layanan: 'RideHailing' untuk ojek/mobil, 'AirportTransfer' untuk antar-jemput bandara. Kosongkan untuk keduanya.")] string? jenis = null,
        [Description("Jarak tempuh dalam kilometer, dipakai untuk menghitung tarif per-km. Abaikan untuk antar-jemput bandara.")] double jarakKm = 8)
    {
        var query = _db.TransportOptions.AsNoTracking()
            .Include(o => o.Provider)
            .Where(o => o.IsActive);

        if (!string.IsNullOrWhiteSpace(kota))
        {
            // Alias kota yang sama dengan fungsi lain: seed memakai "Denpasar"
            // untuk bandara tapi "Bali" untuk katalog.
            var (term, alias) = CityTerms(kota);
            query = query.Where(o => o.City.Contains(term!) || o.City.Contains(alias));
        }

        if (!string.IsNullOrWhiteSpace(jenis))
            query = query.Where(o => o.ServiceType == jenis);

        var options = await query.Take(MaxRows).ToListAsync();

        if (options.Count == 0)
            return "Belum ada layanan transportasi lokal yang cocok. Coba kota lain.";

        if (jarakKm <= 0) jarakKm = 8;

        var table = new StringBuilder("| Layanan | Penyedia | Kota | Jenis | Kapasitas | Tarif |\n|---|---|---|---|---|---|\n");

        // Tarif diambil dari TransportService supaya angkanya persis sama
        // dengan yang dilihat pelanggan di halaman /transport.
        foreach (var o in options.OrderBy(o => TransportService.FareFor(o, jarakKm)))
        {
            var fare = TransportService.FareFor(o, jarakKm);
            var note = o.PricingMode == "Flat"
                ? $"{Rupiah(fare)} (rute tetap{(o.RouteArea is null ? "" : $" ke {o.RouteArea}")})"
                : $"{Rupiah(fare)} untuk {jarakKm:0.#} km";

            table.AppendLine($"| {o.Name} | {o.Provider?.Name} | {o.City} | " +
                             $"{(o.ServiceType == "AirportTransfer" ? "Antar-jemput bandara" : "Ojek/mobil")} | " +
                             $"{o.Capacity} orang | {note} |");
        }

        return table.ToString();
    }

    // ----------------------------------------------------------------
    // Bookings
    // ----------------------------------------------------------------
    [KernelFunction("cek_booking")]
    [Description("Cek status dan detail pesanan pelanggan menggunakan kode booking, untuk semua jenis (pesawat, kereta, bus, hotel, transportasi lokal).")]
    public async Task<string> CheckBookingAsync(
        [Description("Kode booking, misalnya 'JKA-260727-1234'.")] string kodeBooking)
    {
        if (string.IsNullOrWhiteSpace(kodeBooking)) return "Kode booking wajib diisi.";

        var code = kodeBooking.Trim();

        var flight = await _db.FlightBookings.AsNoTracking()
            .Include(b => b.Flight!).ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight!).ThenInclude(f => f.ArrivalAirport)
            .FirstOrDefaultAsync(b => b.BookingCode == code);

        if (flight is not null)
            return $"**Tiket pesawat {code}** — status {flight.Status}, " +
                   $"{flight.Flight?.DepartureAirport?.Code}→{flight.Flight?.ArrivalAirport?.Code}, " +
                   $"berangkat {flight.Flight?.DepartureTime:dd MMM yyyy HH:mm}, " +
                   $"{flight.PassengerCount} penumpang, total {Rupiah(flight.TotalPrice)}.";

        var train = await _db.TrainBookings.AsNoTracking()
            .Include(b => b.TrainSchedule!).ThenInclude(s => s.Train)
            .FirstOrDefaultAsync(b => b.BookingCode == code);

        if (train is not null)
            return $"**Tiket kereta {code}** — status {train.Status}, {train.TrainSchedule?.Train?.Name}, " +
                   $"berangkat {train.TrainSchedule?.DepartureTime:dd MMM yyyy HH:mm}, total {Rupiah(train.TotalPrice)}.";

        var bus = await _db.BusBookings.AsNoTracking()
            .Include(b => b.BusSchedule!).ThenInclude(s => s.BusService!).ThenInclude(v => v.Operator)
            .FirstOrDefaultAsync(b => b.BookingCode == code);

        if (bus is not null)
            return $"**Tiket bus {code}** — status {bus.Status}, {bus.BusSchedule?.BusService?.Operator?.Name}, " +
                   $"berangkat {bus.BusSchedule?.DepartureTime:dd MMM yyyy HH:mm}, " +
                   $"{bus.PassengerCount} penumpang, total {Rupiah(bus.TotalPrice)}.";

        var hotel = await _db.HotelBookings.AsNoTracking()
            .Include(b => b.Room!).ThenInclude(r => r.Hotel)
            .FirstOrDefaultAsync(b => b.BookingCode == code);

        if (hotel is not null)
            return $"**Voucher hotel {code}** — status {hotel.Status}, {hotel.Room?.Hotel?.Name}, " +
                   $"check-in {hotel.CheckInDate:dd MMM yyyy}, {hotel.Nights} malam, total {Rupiah(hotel.TotalPrice)}.";

        var ride = await _db.TransportBookings.AsNoTracking()
            .Include(b => b.Option!).ThenInclude(o => o.Provider)
            .FirstOrDefaultAsync(b => b.BookingCode == code);

        if (ride is not null)
            return $"**Penjemputan {code}** — status {ride.Status}, " +
                   $"{ride.Option?.Provider?.Name} {ride.Option?.Name}, " +
                   $"{ride.PickupAddress} → {ride.DropoffAddress}, " +
                   $"dijemput {ride.PickupTime:dd MMM yyyy HH:mm}, total {Rupiah(ride.TotalPrice)}.";

        return $"Kode booking {code} tidak ditemukan. Pastikan kodenya benar.";
    }

    [KernelFunction("ringkasan_inventori")]
    [Description("Ringkasan jumlah inventori Joka saat ini: penerbangan, kereta, bus, hotel, aktivitas, dan paket travel.")]
    public async Task<string> InventorySummaryAsync()
    {
        return $"Inventori Joka saat ini: " +
               $"{await _db.Flights.CountAsync()} penerbangan, " +
               $"{await _db.TrainSchedules.CountAsync()} jadwal kereta, " +
               $"{await _db.BusSchedules.CountAsync()} keberangkatan bus/shuttle, " +
               $"{await _db.Hotels.CountAsync()} hotel, " +
               $"{await _db.Activities.CountAsync()} aktivitas, " +
               $"{await _db.TravelPackages.CountAsync()} paket travel, " +
               $"{await _db.CarRentals.CountAsync()} pilihan rental mobil, " +
               $"{await _db.TransportOptions.CountAsync()} layanan transportasi lokal.";
    }

    // ----------------------------------------------------------------
    private static bool TryParseDate(string? input, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(input)) return false;

        if (DateTime.TryParse(input, Id, DateTimeStyles.None, out var parsed))
        {
            date = parsed.Date;
            return true;
        }

        return false;
    }

    private static string Rupiah(decimal value) => $"Rp{value.ToString("N0", Id)}";

    private static string Duration(int minutes) =>
        minutes >= 60 ? $"{minutes / 60}j {minutes % 60:00}m" : $"{minutes}m";
}
