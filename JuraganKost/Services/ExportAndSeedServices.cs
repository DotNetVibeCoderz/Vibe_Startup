using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using JuraganKost.Data.Context;
using JuraganKost.Data.Models;
using Microsoft.AspNetCore.Identity;

namespace JuraganKost.Services;

public class ExportService
{
    private readonly AppDbContext _db;
    public ExportService(AppDbContext db) => _db = db;

    public async Task<byte[]> ExportKamarToCsvAsync(int? kostId = null)
    {
        var data = kostId.HasValue ? await _db.Kamar.Where(k => k.KostId == kostId.Value).ToListAsync() : await _db.Kamar.ToListAsync();
        using var ms = new MemoryStream(); using var w = new StreamWriter(ms); using var csv = new CsvWriter(w, new CsvConfiguration(CultureInfo.InvariantCulture)); await csv.WriteRecordsAsync(data); w.Flush(); return ms.ToArray();
    }
    public async Task<byte[]> ExportKamarToExcelAsync(int? kostId = null)
    {
        var data = kostId.HasValue ? await _db.Kamar.Where(k => k.KostId == kostId.Value).ToListAsync() : await _db.Kamar.ToListAsync();
        using var wb = new XLWorkbook(); var ws = wb.Worksheets.Add("Kamar"); ws.Cell(1, 1).InsertTable(data); using var ms = new MemoryStream(); wb.SaveAs(ms); return ms.ToArray();
    }
    public async Task<byte[]> ExportPenghuniToCsvAsync() { var data = await _db.Penghuni.ToListAsync(); using var ms = new MemoryStream(); using var w = new StreamWriter(ms); using var csv = new CsvWriter(w, new CsvConfiguration(CultureInfo.InvariantCulture)); await csv.WriteRecordsAsync(data); w.Flush(); return ms.ToArray(); }
    public async Task<byte[]> ExportPenghuniToExcelAsync() { var data = await _db.Penghuni.ToListAsync(); using var wb = new XLWorkbook(); var ws = wb.Worksheets.Add("Penghuni"); ws.Cell(1, 1).InsertTable(data); using var ms = new MemoryStream(); wb.SaveAs(ms); return ms.ToArray(); }
    public async Task<byte[]> ExportTagihanToCsvAsync() { var data = await _db.Tagihan.Include(t => t.Penghuni).ToListAsync(); using var ms = new MemoryStream(); using var w = new StreamWriter(ms); using var csv = new CsvWriter(w, new CsvConfiguration(CultureInfo.InvariantCulture)); await csv.WriteRecordsAsync(data); w.Flush(); return ms.ToArray(); }
    public async Task<byte[]> ExportTagihanToExcelAsync() { var data = await _db.Tagihan.Include(t => t.Penghuni).ToListAsync(); using var wb = new XLWorkbook(); var ws = wb.Worksheets.Add("Tagihan"); ws.Cell(1, 1).InsertTable(data); using var ms = new MemoryStream(); wb.SaveAs(ms); return ms.ToArray(); }

    public async Task<byte[]> ExportKostToCsvAsync() { var data = await _db.Kost.ToListAsync(); using var ms = new MemoryStream(); using var w = new StreamWriter(ms); using var csv = new CsvWriter(w, new CsvConfiguration(CultureInfo.InvariantCulture)); await csv.WriteRecordsAsync(data); w.Flush(); return ms.ToArray(); }
    public async Task<byte[]> ExportKostToExcelAsync() { var data = await _db.Kost.ToListAsync(); using var wb = new XLWorkbook(); var ws = wb.Worksheets.Add("Kost"); ws.Cell(1, 1).InsertTable(data); using var ms = new MemoryStream(); wb.SaveAs(ms); return ms.ToArray(); }
}

public class SeedService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public SeedService(AppDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager) { _db = db; _userManager = userManager; _roleManager = roleManager; }

    public async Task SeedAsync()
    {
        foreach (var role in new[] { "SuperAdmin", "Pemilik", "Admin", "Penghuni", "Staff" })
            if (!await _roleManager.RoleExistsAsync(role)) await _roleManager.CreateAsync(new IdentityRole(role));
        if (_db.Kost.Any()) return;

        var superAdmin = new ApplicationUser { UserName = "superadmin@juragankost.com", Email = "superadmin@juragankost.com", NamaLengkap = "Super Admin", RoleExt = UserRoleExt.SuperAdmin, EmailConfirmed = true };
        await _userManager.CreateAsync(superAdmin, "Admin123!"); await _userManager.AddToRoleAsync(superAdmin, "SuperAdmin");

        var pemilik = new ApplicationUser { UserName = "pemilik@juragankost.com", Email = "pemilik@juragankost.com", NamaLengkap = "Budi Santoso", RoleExt = UserRoleExt.Pemilik, EmailConfirmed = true };
        await _userManager.CreateAsync(pemilik, "Pemilik123!"); await _userManager.AddToRoleAsync(pemilik, "Pemilik");

        var admin = new ApplicationUser { UserName = "admin@juragankost.com", Email = "admin@juragankost.com", NamaLengkap = "Rina Wijaya", RoleExt = UserRoleExt.Admin, EmailConfirmed = true };
        await _userManager.CreateAsync(admin, "Admin123!"); await _userManager.AddToRoleAsync(admin, "Admin");

        var kost1 = new Kost { Nama = "Kost Melati Indah", Alamat = "Jl. Melati No. 42, Kelurahan Cempaka Putih", Kota = "Jakarta Pusat", Provinsi = "DKI Jakarta", KodePos = "10510", Deskripsi = "Kost nyaman dan bersih dekat pusat kota. Fasilitas lengkap dengan keamanan 24 jam.", Telepon = "021-5551234", Email = "melati@juragankost.com", Jenis = JenisKost.Campuran, Status = KostStatus.Aktif, PemilikId = pemilik.Id };
        var kost2 = new Kost { Nama = "Kost Mawar Premium", Alamat = "Jl. Mawar Indah No. 15, Kebayoran Baru", Kota = "Jakarta Selatan", Provinsi = "DKI Jakarta", KodePos = "12190", Deskripsi = "Kost premium dengan fasilitas AC, WiFi cepat, dan kamar mandi dalam. Dekat dengan pusat bisnis.", Telepon = "021-7778899", Email = "mawar@juragankost.com", Jenis = JenisKost.Putri, Status = KostStatus.Aktif, PemilikId = pemilik.Id };
        _db.Kost.AddRange(kost1, kost2);
        await _db.SaveChangesAsync();

        var fasS = "[\"Kipas Angin\",\"Tempat Tidur\",\"Lemari\",\"Meja Belajar\"]";
        var fasP = "[\"AC\",\"Tempat Tidur\",\"Lemari\",\"Meja\",\"TV\",\"Kulkas Mini\"]";
        var fasV = "[\"AC\",\"Water Heater\",\"Tempat Tidur Queen\",\"Lemari Besar\",\"Meja Kerja\",\"TV 32\\\"\",\"Kulkas\",\"Sofa\"]";
        var kamarList = new List<Kamar>(); var rng = new Random(42);
        for (int i = 1; i <= 12; i++)
        {
            var jenis = i <= 6 ? JenisKamar.Standar : (i <= 10 ? JenisKamar.Premium : JenisKamar.VIP);
            var h = jenis switch { JenisKamar.Standar => 800_000m + rng.Next(0, 4) * 100_000m, JenisKamar.Premium => 1_500_000m + rng.Next(0, 5) * 200_000m, JenisKamar.VIP => 2_500_000m + rng.Next(0, 4) * 500_000m, _ => 1_000_000m };
            var f = jenis switch { JenisKamar.Standar => fasS, JenisKamar.Premium => fasP, JenisKamar.VIP => fasV, _ => fasS };
            kamarList.Add(new Kamar { NomorKamar = $"KM-{i:D3}", KostId = i <= 8 ? kost1.Id : kost2.Id, HargaSewa = h, Deposit = h, Luas = jenis switch { JenisKamar.Standar => 12, JenisKamar.Premium => 20, JenisKamar.VIP => 30, _ => 16 }, Status = i <= 9 ? StatusKamar.Terisi : (i <= 10 ? StatusKamar.Kosong : StatusKamar.Booking), Jenis = jenis, Kapasitas = 1, Fasilitas = f, Deskripsi = $"Kamar {jenis} dengan fasilitas lengkap dan nyaman.", IsTersedia = i > 9 });
        }
        _db.Kamar.AddRange(kamarList); await _db.SaveChangesAsync();

        var namaList = new[] { "Andi Prasetyo", "Siti Nurhaliza", "Rudi Hermawan", "Dewi Lestari", "Bambang Suharto", "Mega Putri", "Agus Wijaya", "Lina Marlina", "Dimas Ardian", "Putri Ayu", "Rizky Febrian", "Nadia Safira" };
        var pkjList = new[] { "Karyawan Swasta", "Mahasiswa", "Freelancer", "PNS", "Wiraswasta", "Dokter" };
        var penghuniList = new List<Penghuni>();
        for (int i = 0; i < 9; i++) penghuniList.Add(new Penghuni { NamaLengkap = namaList[i], NIK = $"3174{i:D2}0506{90000 + i:D4}", NoHP = $"0812{rng.Next(1000, 9999)}{rng.Next(1000, 9999)}", Email = $"{namaList[i].Split(' ')[0].ToLower()}@email.com", Pekerjaan = pkjList[rng.Next(pkjList.Length)], KontakDarurat = $"0813{rng.Next(1000, 9999)}{rng.Next(1000, 9999)}", HubunganKontakDarurat = rng.Next(2) == 0 ? "Orang Tua" : "Saudara", AlamatAsal = $"Alamat asal {namaList[i]}", Status = StatusPenghuni.Aktif, KamarId = kamarList[i].Id, TanggalMasuk = DateTime.UtcNow.AddMonths(-rng.Next(1, 12)) });
        _db.Penghuni.AddRange(penghuniList); await _db.SaveChangesAsync();

        for (int i = 0; i < 9; i++) { var pu = new ApplicationUser { UserName = $"penghuni{i + 1}@juragankost.com", Email = $"penghuni{i + 1}@juragankost.com", NamaLengkap = namaList[i], RoleExt = UserRoleExt.Penghuni, EmailConfirmed = true }; await _userManager.CreateAsync(pu, "Penghuni123!"); await _userManager.AddToRoleAsync(pu, "Penghuni"); penghuniList[i].UserId = pu.Id; }
        await _db.SaveChangesAsync();

        for (int i = 0; i < 9; i++) _db.Kontrak.Add(new Kontrak { NomorKontrak = $"KTR-{DateTime.Now:yyyyMM}-{i + 1:D3}", PenghuniId = penghuniList[i].Id, KamarId = kamarList[i].Id, TanggalMulai = penghuniList[i].TanggalMasuk, TanggalSelesai = penghuniList[i].TanggalMasuk.AddYears(1), HargaSewa = kamarList[i].HargaSewa, Deposit = kamarList[i].HargaSewa, Status = StatusKontrak.Aktif });
        await _db.SaveChangesAsync();

        for (int i = 0; i < 9; i++) _db.Tagihan.Add(new Tagihan { NomorTagihan = $"INV-{DateTime.Now:yyyyMM}-{i + 1:D3}", PenghuniId = penghuniList[i].Id, KamarId = kamarList[i].Id, Jenis = JenisTagihan.SewaKamar, Jumlah = kamarList[i].HargaSewa, JatuhTempo = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 10), Status = i < 6 ? StatusTagihan.Dibayar : StatusTagihan.BelumDibayar, Deskripsi = $"Tagihan sewa kamar {DateTime.Now:MMMM yyyy}" });
        await _db.SaveChangesAsync();

        _db.Staff.AddRange(new Staff { Nama = "Pak Joko", KostId = kost1.Id, Posisi = PosisiStaff.Penjaga, NoHP = "081111111", Gaji = 3_000_000 }, new Staff { Nama = "Bu Sumi", KostId = kost1.Id, Posisi = PosisiStaff.CleaningService, NoHP = "081111112", Gaji = 2_500_000 }, new Staff { Nama = "Pak Rahman", KostId = kost2.Id, Posisi = PosisiStaff.Penjaga, NoHP = "081111113", Gaji = 3_200_000 });
        _db.Inventaris.AddRange(new InventarisItem { Nama = "Kasur Spring Bed", Kode = "INV-001", KostId = kost1.Id, Kategori = KategoriInventaris.Furniture, Jumlah = 10, HargaBeli = 1_500_000 }, new InventarisItem { Nama = "AC 1/2 PK", Kode = "INV-002", KostId = kost2.Id, Kategori = KategoriInventaris.Elektronik, Jumlah = 5, HargaBeli = 3_500_000 }, new InventarisItem { Nama = "Lemari Kayu", Kode = "INV-003", KostId = kost1.Id, Kategori = KategoriInventaris.Furniture, Jumlah = 12, HargaBeli = 800_000 }, new InventarisItem { Nama = "Meja Belajar", Kode = "INV-004", KostId = kost1.Id, Kategori = KategoriInventaris.Furniture, Jumlah = 12, HargaBeli = 350_000 }, new InventarisItem { Nama = "Water Heater", Kode = "INV-005", KostId = kost2.Id, Kategori = KategoriInventaris.Elektronik, Jumlah = 3, HargaBeli = 500_000 });
        _db.Komplain.AddRange(new Komplain { NomorKomplain = "CMP-001", PenghuniId = penghuniList[0].Id, KamarId = kamarList[0].Id, Kategori = KategoriKomplain.Listrik, Judul = "Lampu kamar mati", Deskripsi = "Lampu utama di kamar mati sejak kemarin malam", Status = StatusKomplain.Selesai, Respon = "Sudah diganti oleh teknisi", SelesaiAt = DateTime.UtcNow.AddDays(-1) }, new Komplain { NomorKomplain = "CMP-002", PenghuniId = penghuniList[2].Id, KamarId = kamarList[2].Id, Kategori = KategoriKomplain.Air, Judul = "Air keran kecil", Deskripsi = "Aliran air di kamar mandi sangat kecil", Status = StatusKomplain.Diproses });
        _db.Review.AddRange(new Review { KostId = kost1.Id, PenghuniId = penghuniList[0].Id, Rating = 5, Komentar = "Kost nyaman dan bersih! Pelayanan ramah.", Emoji = "😍" }, new Review { KostId = kost1.Id, PenghuniId = penghuniList[2].Id, Rating = 4, Komentar = "Lumayan bagus, tapi parkiran agak sempit.", Emoji = "👍" }, new Review { KostId = kost2.Id, PenghuniId = penghuniList[7].Id, Rating = 5, Komentar = "Premium bangeet! Fasilitas lengkap.", Emoji = "🌟" });
        _db.MarketplaceListing.AddRange(new MarketplaceListing { KostId = kost1.Id, IsPublic = true, HighlightFitur = "AC, WiFi, Keamanan 24 Jam" }, new MarketplaceListing { KostId = kost2.Id, IsPublic = true, HighlightFitur = "Premium, AC, Water Heater, TV, Kulkas" });
        await _db.SaveChangesAsync();
    }
}
