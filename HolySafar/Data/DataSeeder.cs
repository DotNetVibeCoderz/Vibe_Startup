using HolySafar.Models;
using Microsoft.EntityFrameworkCore;

namespace HolySafar.Data;

/// <summary>
/// Seeder untuk sample data HolySafar
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        // Cek apakah sudah ada data
        if (await db.Users.AnyAsync()) return;

        // ===== USERS =====
        var users = new List<ApplicationUser>
        {
            new() { Username = "admin", PasswordHash = HashPassword("admin123"), FullName = "Admin HolySafar", Email = "admin@holysafar.com", Phone = "081111111111", Role = UserRole.Admin, IsActive = true },
            new() { Username = "agen1", PasswordHash = HashPassword("agen123"), FullName = "Ahmad Fauzi", Email = "ahmad@holysafar.com", Phone = "082222222222", Role = UserRole.Agen, IsActive = true },
            new() { Username = "agen2", PasswordHash = HashPassword("agen123"), FullName = "Siti Nurhaliza", Email = "siti@holysafar.com", Phone = "082222222223", Role = UserRole.Agen, IsActive = true },
            new() { Username = "jamaah1", PasswordHash = HashPassword("jamaah123"), FullName = "Budi Santoso", Email = "budi@gmail.com", Phone = "083333333331", Role = UserRole.Jamaah, IsActive = true },
            new() { Username = "jamaah2", PasswordHash = HashPassword("jamaah123"), FullName = "Rina Wijaya", Email = "rina@gmail.com", Phone = "083333333332", Role = UserRole.Jamaah, IsActive = true },
            new() { Username = "jamaah3", PasswordHash = HashPassword("jamaah123"), FullName = "Hendra Gunawan", Email = "hendra@gmail.com", Phone = "083333333333", Role = UserRole.Jamaah, IsActive = true },
            new() { Username = "jamaah4", PasswordHash = HashPassword("jamaah123"), FullName = "Dewi Lestari", Email = "dewi@gmail.com", Phone = "083333333334", Role = UserRole.Jamaah, IsActive = true },
            new() { Username = "jamaah5", PasswordHash = HashPassword("jamaah123"), FullName = "Agus Prasetyo", Email = "agus@gmail.com", Phone = "083333333335", Role = UserRole.Jamaah, IsActive = true },
            new() { Username = "jamaah6", PasswordHash = HashPassword("jamaah123"), FullName = "Mega Sari", Email = "mega@gmail.com", Phone = "083333333336", Role = UserRole.Jamaah, IsActive = true },
            new() { Username = "jamaah7", PasswordHash = HashPassword("jamaah123"), FullName = "Yusuf Hidayat", Email = "yusuf@gmail.com", Phone = "083333333337", Role = UserRole.Jamaah, IsActive = true },
        };
        db.Users.AddRange(users);
        await db.SaveChangesAsync();

        // ===== PAKET =====
        var pakets = new List<Paket>
        {
            new()
            {
                NamaPaket = "Umroh Reguler 9 Hari",
                JenisPaket = "Umroh",
                Deskripsi = "Paket Umroh reguler 9 hari dengan hotel bintang 4 dekat Masjidil Haram. Cocok untuk keluarga.",
                Harga = 28500000,
                Fasilitas = "Hotel Bintang 4, Makan 3x sehari, Transportasi Bus AC, Visa, Asuransi, Manasik",
                NamaHotelMekkah = "Swissotel Al Maqam Makkah",
                NamaHotelMadinah = "Dar Al Taqwa Hotel Madinah",
                Maskapai = "Saudi Airlines",
                RutePenerbangan = "CGK - JED - MED - CGK",
                DurasiHari = 9,
                TanggalBerangkat = new DateTime(2025, 6, 15),
                TanggalPulang = new DateTime(2025, 6, 23),
                Kuota = 45,
                Terisi = 23,
                IsActive = true,
                IsPublished = true,
                BrosurUrl = "/images/brosur/umroh-reguler.jpg",
                ItineraryJson = "[{\"hari\":1,\"kegiatan\":\"Berangkat dari Jakarta menuju Jeddah\"},{\"hari\":2,\"kegiatan\":\"Tiba di Jeddah, perjalanan ke Makkah, Umroh\"},{\"hari\":3,\"kegiatan\":\"Ziarah Makkah\"},{\"hari\":4,\"kegiatan\":\"Ibadah bebas di Masjidil Haram\"},{\"hari\":5,\"kegiatan\":\"Perjalanan ke Madinah\"},{\"hari\":6,\"kegiatan\":\"Ziarah Madinah & Masjid Nabawi\"},{\"hari\":7,\"kegiatan\":\"Raudhah & ibadah bebas\"},{\"hari\":8,\"kegiatan\":\"Persiapan pulang\"},{\"hari\":9,\"kegiatan\":\"Tiba di Jakarta\"}]"
            },
            new()
            {
                NamaPaket = "Umroh Plus 12 Hari",
                JenisPaket = "Umroh",
                Deskripsi = "Umroh plus dengan tambahan wisata ke Thaif dan tour ziarah lebih lengkap.",
                Harga = 35000000,
                Fasilitas = "Hotel Bintang 5, Makan 3x, Transportasi VIP, Visa, Asuransi, Manasik, Tour Thaif",
                NamaHotelMekkah = "Makkah Clock Royal Tower - A Fairmont Hotel",
                NamaHotelMadinah = "Anwar Al Madinah Movenpick",
                Maskapai = "Garuda Indonesia",
                RutePenerbangan = "CGK - JED - MED - CGK",
                DurasiHari = 12,
                TanggalBerangkat = new DateTime(2025, 7, 1),
                TanggalPulang = new DateTime(2025, 7, 12),
                Kuota = 40,
                Terisi = 15,
                IsActive = true,
                IsPublished = true,
                ItineraryJson = "[{\"hari\":1,\"kegiatan\":\"Berangkat Jakarta - Jeddah\"},{\"hari\":2,\"kegiatan\":\"Tiba Jeddah, perjalanan Makkah, Umroh\"},{\"hari\":3,\"kegiatan\":\"Ziarah Makkah lengkap\"},{\"hari\":4,\"kegiatan\":\"Ibadah bebas\"},{\"hari\":5,\"kegiatan\":\"Tour ke Thaif\"},{\"hari\":6,\"kegiatan\":\"Ibadah & persiapan Madinah\"},{\"hari\":7,\"kegiatan\":\"Perjalanan ke Madinah\"},{\"hari\":8,\"kegiatan\":\"Ziarah Madinah\"},{\"hari\":9,\"kegiatan\":\"Raudhah & ibadah\"},{\"hari\":10,\"kegiatan\":\"Ziarah sekitar Madinah\"},{\"hari\":11,\"kegiatan\":\"Persiapan pulang\"},{\"hari\":12,\"kegiatan\":\"Tiba di Jakarta\"}]"
            },
            new()
            {
                NamaPaket = "Haji Plus 2025",
                JenisPaket = "HajiPlus",
                Deskripsi = "Program Haji Plus dengan fasilitas premium dan pelayanan eksklusif. Tanpa antrian panjang.",
                Harga = 250000000,
                Fasilitas = "Hotel Bintang 5 depan Masjidil Haram, Katering premium, Transportasi VIP, Pendampingan 24 jam",
                NamaHotelMekkah = "Makkah Hilton Hotel & Towers",
                NamaHotelMadinah = "Oberoi Madinah",
                Maskapai = "Saudi Airlines",
                RutePenerbangan = "CGK - JED - MED - CGK",
                DurasiHari = 40,
                TanggalBerangkat = new DateTime(2025, 5, 20),
                TanggalPulang = new DateTime(2025, 6, 29),
                Kuota = 30,
                Terisi = 8,
                IsActive = true,
                IsPublished = true,
                ItineraryJson = "[{\"hari\":1,\"kegiatan\":\"Berangkat ke Tanah Suci\"},{\"hari\":2,\"kegiatan\":\"Tiba di Jeddah, Umroh\"},{\"hari\":3,\"kegiatan\":\"Ibadah & persiapan Haji\"},{\"hari\":8,\"kegiatan\":\"Tarwiyah - Mina\"},{\"hari\":9,\"kegiatan\":\"Wukuf Arafah\"},{\"hari\":10,\"kegiatan\":\"Mabit Muzdalifah & Mina\"},{\"hari\":11,\"kegiatan\":\"Lempar Jumrah\"},{\"hari\":12,\"kegiatan\":\"Tahallul, Tawaf Ifadah\"},{\"hari\":13,\"kegiatan\":\"Mabit Mina\"},{\"hari\":14,\"kegiatan\":\"Pulang ke Makkah\"},{\"hari\":15,\"kegiatan\":\"Ziarah Makkah\"},{\"hari\":17,\"kegiatan\":\"Perjalanan ke Madinah\"},{\"hari\":18,\"kegiatan\":\"Ziarah Madinah\"},{\"hari\":38,\"kegiatan\":\"Persiapan pulang\"},{\"hari\":40,\"kegiatan\":\"Tiba Jakarta\"}]"
            }
        };
        db.Paket.AddRange(pakets);
        await db.SaveChangesAsync();

        // ===== JAMAAH =====
        var jamaahList = new List<Jamaah>
        {
            new() { NamaLengkap = "Budi Santoso", Nik = "3174010101800001", NoPaspor = "X1234567", NoKK = "3174010101800001", TempatLahir = "Jakarta", TanggalLahir = new DateTime(1980, 1, 1), JenisKelamin = "Laki-laki", Alamat = "Jl. Merdeka No. 1", Kota = "Jakarta Pusat", Provinsi = "DKI Jakarta", KodePos = "10110", NoTelepon = "083333333331", Email = "budi@gmail.com", GolonganDarah = "A", NamaAyahKandung = "Sukarno Santoso", AhliWaris = "Rina Santoso", HubunganAhliWaris = "Istri", StatusDokumen = DocumentStatus.Verified, StatusKeberangkatan = DepartureStatus.Scheduled, UserId = 4, PaketId = 1, Latitude = 21.4225, Longitude = 39.8262 },
            new() { NamaLengkap = "Rina Wijaya", Nik = "3174020202820002", NoPaspor = "X2345678", NoKK = "3174020202820002", TempatLahir = "Bandung", TanggalLahir = new DateTime(1982, 2, 2), JenisKelamin = "Perempuan", Alamat = "Jl. Asia Afrika No. 2", Kota = "Bandung", Provinsi = "Jawa Barat", KodePos = "40261", NoTelepon = "083333333332", Email = "rina@gmail.com", GolonganDarah = "B", NamaAyahKandung = "Rahmat Wijaya", AhliWaris = "Budi Wijaya", HubunganAhliWaris = "Suami", StatusDokumen = DocumentStatus.Verified, StatusKeberangkatan = DepartureStatus.InTransit, UserId = 5, PaketId = 1, Latitude = 21.4230, Longitude = 39.8250 },
            new() { NamaLengkap = "Hendra Gunawan", Nik = "3174030303850003", NoPaspor = "X3456789", NoKK = "3174030303850003", TempatLahir = "Surabaya", TanggalLahir = new DateTime(1985, 3, 3), JenisKelamin = "Laki-laki", Alamat = "Jl. Tunjungan No. 3", Kota = "Surabaya", Provinsi = "Jawa Timur", KodePos = "60271", NoTelepon = "083333333333", Email = "hendra@gmail.com", GolonganDarah = "O", NamaAyahKandung = "Sugeng Gunawan", AhliWaris = "Dian Gunawan", HubunganAhliWaris = "Istri", StatusDokumen = DocumentStatus.Submitted, StatusKeberangkatan = DepartureStatus.Scheduled, UserId = 6, PaketId = 2, Latitude = 21.4200, Longitude = 39.8270 },
            new() { NamaLengkap = "Dewi Lestari", Nik = "3174040404880004", NoPaspor = "X4567890", NoKK = "3174040404880004", TempatLahir = "Yogyakarta", TanggalLahir = new DateTime(1988, 4, 4), JenisKelamin = "Perempuan", Alamat = "Jl. Malioboro No. 4", Kota = "Yogyakarta", Provinsi = "DIY", KodePos = "55271", NoTelepon = "083333333334", Email = "dewi@gmail.com", GolonganDarah = "AB", NamaAyahKandung = "Suparno Lestari", AhliWaris = "Ahmad Lestari", HubunganAhliWaris = "Suami", StatusDokumen = DocumentStatus.Pending, StatusKeberangkatan = DepartureStatus.CheckIn, UserId = 7, PaketId = 2, Latitude = 21.4215, Longitude = 39.8240 },
            new() { NamaLengkap = "Agus Prasetyo", Nik = "3175050505900005", NoPaspor = "X5678901", NoKK = "3175050505900005", TempatLahir = "Semarang", TanggalLahir = new DateTime(1990, 5, 5), JenisKelamin = "Laki-laki", Alamat = "Jl. Pemuda No. 5", Kota = "Semarang", Provinsi = "Jawa Tengah", KodePos = "50131", NoTelepon = "083333333335", Email = "agus@gmail.com", GolonganDarah = "A", StatusDokumen = DocumentStatus.Verified, StatusKeberangkatan = DepartureStatus.Scheduled, UserId = 8, PaketId = 3, Latitude = 21.4240, Longitude = 39.8280 },
            new() { NamaLengkap = "Mega Sari", Nik = "3176060606920006", NoPaspor = "X6789012", NoKK = "3176060606920006", TempatLahir = "Medan", TanggalLahir = new DateTime(1992, 6, 6), JenisKelamin = "Perempuan", Alamat = "Jl. Gatot Subroto No. 6", Kota = "Medan", Provinsi = "Sumatera Utara", KodePos = "20117", NoTelepon = "083333333336", Email = "mega@gmail.com", GolonganDarah = "B", StatusDokumen = DocumentStatus.Submitted, StatusKeberangkatan = DepartureStatus.Scheduled, UserId = 9, PaketId = 1, Latitude = 21.4195, Longitude = 39.8235 },
            new() { NamaLengkap = "Yusuf Hidayat", Nik = "3177070707940007", NoPaspor = "X7890123", NoKK = "3177070707940007", TempatLahir = "Makassar", TanggalLahir = new DateTime(1994, 7, 7), JenisKelamin = "Laki-laki", Alamat = "Jl. Pettarani No. 7", Kota = "Makassar", Provinsi = "Sulawesi Selatan", KodePos = "90221", NoTelepon = "083333333337", Email = "yusuf@gmail.com", GolonganDarah = "O", StatusDokumen = DocumentStatus.Verified, StatusKeberangkatan = DepartureStatus.Arrived, UserId = 10, PaketId = 2, Latitude = 21.4228, Longitude = 39.8255 },
        };
        db.Jamaah.AddRange(jamaahList);
        await db.SaveChangesAsync();

        // ===== PEMBAYARAN =====
        var pembayaranList = new List<Pembayaran>
        {
            new() { JamaahId = 1, PaketId = 1, TotalBiaya = 28500000, TotalDibayar = 15000000, Status = PaymentStatus.Partial, MetodePembayaran = "Transfer Bank", TanggalJatuhTempo = new DateTime(2025, 5, 15) },
            new() { JamaahId = 2, PaketId = 1, TotalBiaya = 28500000, TotalDibayar = 28500000, Status = PaymentStatus.Paid, MetodePembayaran = "Transfer Bank" },
            new() { JamaahId = 3, PaketId = 2, TotalBiaya = 35000000, TotalDibayar = 10000000, Status = PaymentStatus.Partial, MetodePembayaran = "Cash", TanggalJatuhTempo = new DateTime(2025, 6, 1) },
            new() { JamaahId = 4, PaketId = 2, TotalBiaya = 35000000, TotalDibayar = 0, Status = PaymentStatus.Pending, MetodePembayaran = "Transfer Bank", TanggalJatuhTempo = new DateTime(2025, 6, 1) },
            new() { JamaahId = 5, PaketId = 3, TotalBiaya = 250000000, TotalDibayar = 100000000, Status = PaymentStatus.Partial, MetodePembayaran = "Transfer Bank", TanggalJatuhTempo = new DateTime(2025, 4, 20) },
        };
        db.Pembayaran.AddRange(pembayaranList);
        await db.SaveChangesAsync();

        // ===== CICILAN =====
        var cicilanList = new List<Cicilan>
        {
            new() { PembayaranId = 1, Jumlah = 10000000, TanggalBayar = new DateTime(2025, 2, 1), MetodePembayaran = "Transfer BCA", Dikonfirmasi = true },
            new() { PembayaranId = 1, Jumlah = 5000000, TanggalBayar = new DateTime(2025, 3, 1), MetodePembayaran = "Transfer BCA", Dikonfirmasi = true },
            new() { PembayaranId = 2, Jumlah = 20000000, TanggalBayar = new DateTime(2025, 1, 15), MetodePembayaran = "Transfer Mandiri", Dikonfirmasi = true },
            new() { PembayaranId = 2, Jumlah = 8500000, TanggalBayar = new DateTime(2025, 2, 20), MetodePembayaran = "Transfer Mandiri", Dikonfirmasi = true },
            new() { PembayaranId = 3, Jumlah = 10000000, TanggalBayar = new DateTime(2025, 3, 10), MetodePembayaran = "Cash", Dikonfirmasi = true },
            new() { PembayaranId = 5, Jumlah = 100000000, TanggalBayar = new DateTime(2025, 1, 10), MetodePembayaran = "Transfer BNI", Dikonfirmasi = true },
        };
        db.Cicilan.AddRange(cicilanList);
        await db.SaveChangesAsync();

        // ===== KEBERANGKATAN =====
        var keberangkatanList = new List<Keberangkatan>
        {
            new() { PaketId = 1, KodeKeberangkatan = "UMR-2025-001", Maskapai = "Saudi Airlines", NoPenerbangan = "SV-817", BandaraAsal = "Soekarno-Hatta (CGK)", BandaraTujuan = "King Abdulaziz (JED)", TanggalBerangkat = new DateTime(2025, 6, 15, 8, 0, 0), TanggalTiba = new DateTime(2025, 6, 15, 14, 0, 0), Status = DepartureStatus.Scheduled },
            new() { PaketId = 2, KodeKeberangkatan = "UMR-2025-002", Maskapai = "Garuda Indonesia", NoPenerbangan = "GA-990", BandaraAsal = "Soekarno-Hatta (CGK)", BandaraTujuan = "King Abdulaziz (JED)", TanggalBerangkat = new DateTime(2025, 7, 1, 9, 0, 0), TanggalTiba = new DateTime(2025, 7, 1, 15, 0, 0), Status = DepartureStatus.Scheduled },
            new() { PaketId = 3, KodeKeberangkatan = "HJ-2025-001", Maskapai = "Saudi Airlines", NoPenerbangan = "SV-819", BandaraAsal = "Soekarno-Hatta (CGK)", BandaraTujuan = "King Abdulaziz (JED)", TanggalBerangkat = new DateTime(2025, 5, 20, 7, 0, 0), TanggalTiba = new DateTime(2025, 5, 20, 13, 0, 0), Status = DepartureStatus.Scheduled },
        };
        db.Keberangkatan.AddRange(keberangkatanList);
        await db.SaveChangesAsync();

        // ===== MATERI MANASIK =====
        var materiList = new List<MateriManasik>
        {
            new() { Judul = "Niat Ihram dan Tata Caranya", Konten = "Ihram merupakan niat memasuki ibadah haji atau umroh. Niat ihram dilakukan di miqat...", Kategori = "Ihram", Urutan = 1, IsPublished = true },
            new() { Judul = "Tawaf: Macam-Macam dan Doanya", Konten = "Tawaf adalah mengelilingi Ka'bah sebanyak 7 kali putaran. Ada beberapa macam tawaf...", Kategori = "Tawaf", Urutan = 2, IsPublished = true },
            new() { Judul = "Sa'i: Perjalanan Shafa - Marwah", Konten = "Sa'i adalah berjalan bolak-balik antara bukit Shafa dan Marwah sebanyak 7 kali...", Kategori = "Sa'i", Urutan = 3, IsPublished = true },
            new() { Judul = "Tahallul: Mencukur Rambut", Konten = "Tahallul adalah mencukur atau memotong rambut sebagai tanda selesainya ibadah...", Kategori = "Tahallul", Urutan = 4, IsPublished = true },
            new() { Judul = "Wukuf di Arafah", Konten = "Wukuf di Arafah adalah rukun haji yang paling utama. Dilakukan pada tanggal 9 Dzulhijjah...", Kategori = "Haji", Urutan = 5, IsPublished = true },
            new() { Judul = "Doa-Doa Penting dalam Umroh", Konten = "Kumpulan doa-doa penting yang dibaca selama ibadah umroh, mulai dari niat ihram...", Kategori = "Doa", Urutan = 6, IsPublished = true },
        };
        db.MateriManasik.AddRange(materiList);

        // ===== KUIS =====
        var kuisList = new List<Kuis>
        {
            new() { Pertanyaan = "Berapa kali putaran Tawaf mengelilingi Ka'bah?", PilihanA = "3 kali", PilihanB = "5 kali", PilihanC = "7 kali", PilihanD = "9 kali", JawabanBenar = "C", Penjelasan = "Tawaf dilakukan sebanyak 7 kali putaran mengelilingi Ka'bah.", Kategori = "Tawaf" },
            new() { Pertanyaan = "Di manakah lokasi Wukuf dilaksanakan?", PilihanA = "Mina", PilihanB = "Arafah", PilihanC = "Muzdalifah", PilihanD = "Makkah", JawabanBenar = "B", Penjelasan = "Wukuf dilaksanakan di Padang Arafah pada tanggal 9 Dzulhijjah.", Kategori = "Haji" },
            new() { Pertanyaan = "Apakah yang dimaksud dengan Ihram?", PilihanA = "Niat memulai ibadah haji/umroh", PilihanB = "Pakaian putih", PilihanC = "Mengelilingi Ka'bah", PilihanD = "Berlari-lari kecil", JawabanBenar = "A", Penjelasan = "Ihram adalah niat untuk memulai ibadah haji atau umroh.", Kategori = "Ihram" },
            new() { Pertanyaan = "Berapa jarak antara bukit Shafa dan Marwah?", PilihanA = "100 meter", PilihanB = "250 meter", PilihanC = "450 meter", PilihanD = "1 km", JawabanBenar = "C", Penjelasan = "Jarak antara Shafa dan Marwah sekitar 450 meter.", Kategori = "Sa'i" },
        };
        db.Kuis.AddRange(kuisList);

        // ===== KONTAK DARURAT =====
        var kontakList = new List<KontakDarurat>
        {
            new() { Nama = "KJRI Jeddah", Telepon = "+966-12-6711271", Alamat = "Jeddah, Saudi Arabia", Peran = "Kedutaan", Catatan = "Konsulat Jenderal RI di Jeddah", IsActive = true },
            new() { Nama = "Rumah Sakit An Nur Makkah", Telepon = "+966-12-5665000", Alamat = "Makkah, Saudi Arabia", Peran = "Rumah Sakit", Catatan = "RS terdekat dari Masjidil Haram", IsActive = true },
            new() { Nama = "Polisi Saudi", Telepon = "999", Alamat = "Saudi Arabia", Peran = "Polisi", Catatan = "Nomor darurat kepolisian", IsActive = true },
            new() { Nama = "Ambulans", Telepon = "997", Alamat = "Saudi Arabia", Peran = "Ambulans", Catatan = "Layanan ambulans darurat", IsActive = true },
            new() { Nama = "Muthowif (Pembimbing)", Telepon = "+966-55-1234567", Alamat = "Makkah", Peran = "Pembimbing Ibadah", Catatan = "Hubungi untuk pertanyaan seputar ibadah", IsActive = true },
        };
        db.KontakDarurat.AddRange(kontakList);

        // ===== PRODUK MARKETPLACE =====
        var produkList = new List<Produk>
        {
            new() { NamaProduk = "Koper Umroh Premium 24 inch", Kategori = "Koper", Deskripsi = "Koper berkualitas dengan roda 4, ringan, dan kuat. Dilengkapi kunci TSA.", Harga = 850000, Stok = 50, IsActive = true },
            new() { NamaProduk = "Mukena Travel Exclusive", Kategori = "Mukena", Deskripsi = "Mukena bahan katun premium, ringan, tidak mudah kusut, dilengkapi tas.", Harga = 350000, Stok = 100, IsActive = true },
            new() { NamaProduk = "Sajadah Travel Lipat", Kategori = "Sajadah", Deskripsi = "Sajadah travel dengan bahan lembut, mudah dilipat, ringan dibawa.", Harga = 150000, Stok = 200, IsActive = true },
            new() { NamaProduk = "Tasbih Digital", Kategori = "Aksesoris", Deskripsi = "Tasbih digital dengan penghitung otomatis, ringan dan mudah digunakan.", Harga = 75000, Stok = 300, IsActive = true },
            new() { NamaProduk = "Ihram Pria Premium", Kategori = "Pakaian", Deskripsi = "Kain ihram berkualitas tinggi, lembut, menyerap keringat, 2 pcs.", Harga = 250000, Stok = 80, IsActive = true },
            new() { NamaProduk = "Sandal Travel Anti Selip", Kategori = "Alas Kaki", Deskripsi = "Sandal travel ringan dan anti selip, cocok untuk ibadah.", Harga = 120000, Stok = 150, IsActive = true },
        };
        db.Produk.AddRange(produkList);

        // ===== PENGUMUMAN =====
        var pengumumanList = new List<Pengumuman>
        {
            new() { Judul = "Jadwal Manasik Umroh Bulan Juni 2025", Isi = "Diberitahukan kepada seluruh jamaah bahwa manasik umroh untuk keberangkatan Juni akan dilaksanakan pada tanggal 1 Juni 2025 di Masjid Agung. Mohon hadir tepat waktu.", IsActive = true },
            new() { Judul = "Update Persyaratan Vaksin Meningitis", Isi = "Sesuai regulasi terbaru Kemenkes, vaksin meningitis wajib dilakukan minimal 10 hari sebelum keberangkatan. Sertifikat vaksin harus asli.", IsActive = true },
            new() { Judul = "Pembukaan Pendaftaran Haji Plus 2026", Isi = "Kami membuka pendaftaran Haji Plus untuk tahun 2026. Kuota terbatas! Silakan hubungi agen travel terdekat.", IsActive = true },
        };
        db.Pengumuman.AddRange(pengumumanList);

        // ===== KOORDINAT HOTEL (untuk peta di halaman Perjalanan) =====
        pakets[0].LatHotelMekkah = 21.4181; pakets[0].LngHotelMekkah = 39.8256;
        pakets[0].LatHotelMadinah = 24.4694; pakets[0].LngHotelMadinah = 39.6108;
        pakets[0].Transportasi = "Bus AC 45 seat, transfer bandara-hotel, city tour Makkah & Madinah";
        pakets[1].LatHotelMekkah = 21.4225; pakets[1].LngHotelMekkah = 39.8262;
        pakets[1].LatHotelMadinah = 24.4672; pakets[1].LngHotelMadinah = 39.6112;
        pakets[1].Transportasi = "Bus AC eksekutif + kereta cepat Haramain Makkah-Madinah";
        pakets[2].LatHotelMekkah = 21.4190; pakets[2].LngHotelMekkah = 39.8244;
        pakets[2].LatHotelMadinah = 24.4686; pakets[2].LngHotelMadinah = 39.6142;
        pakets[2].Transportasi = "Bus VIP, kereta cepat Haramain, transfer Arafah-Mina-Muzdalifah";

        // ===== ITINERARY / ZIARAH / TRANSPORTASI =====
        var itinerary = new List<ItineraryItem>
        {
            new() { PaketId = pakets[0].Id, Hari = 1, Waktu = "14:00", Judul = "Kumpul di Bandara Soekarno-Hatta", Jenis = "Transportasi", Lokasi = "Terminal 3 CGK", Latitude = -6.1256, Longitude = 106.6559, Urutan = 1, Deskripsi = "Check-in kolektif, pembagian koper dan kartu identitas jamaah." },
            new() { PaketId = pakets[0].Id, Hari = 1, Waktu = "18:30", Judul = "Penerbangan CGK - JED", Jenis = "Transportasi", Lokasi = "Saudi Airlines SV817", Urutan = 2, Deskripsi = "Lama penerbangan sekitar 9 jam. Niat ihram dilakukan di pesawat." },
            new() { PaketId = pakets[0].Id, Hari = 2, Waktu = "06:00", Judul = "Tiba di Jeddah, perjalanan ke Makkah", Jenis = "Transportasi", Lokasi = "King Abdulaziz Intl Airport", Latitude = 21.6796, Longitude = 39.1565, Urutan = 1 },
            new() { PaketId = pakets[0].Id, Hari = 2, Waktu = "13:00", Judul = "Umroh pertama: thawaf, sai, tahallul", Jenis = "Ibadah", Lokasi = "Masjidil Haram", Latitude = 21.4225, Longitude = 39.8262, Urutan = 2, Deskripsi = "Dibimbing muthawif. Bawa air minum dan sandal dalam tas kecil." },
            new() { PaketId = pakets[0].Id, Hari = 3, Waktu = "08:00", Judul = "Ziarah Jabal Rahmah & Padang Arafah", Jenis = "Ziarah", Lokasi = "Arafah", Latitude = 21.3549, Longitude = 39.9843, Urutan = 1 },
            new() { PaketId = pakets[0].Id, Hari = 3, Waktu = "10:30", Judul = "Ziarah Jabal Tsur dan Mina", Jenis = "Ziarah", Lokasi = "Mina", Latitude = 21.4133, Longitude = 39.8933, Urutan = 2 },
            new() { PaketId = pakets[0].Id, Hari = 4, Waktu = "05:00", Judul = "Ibadah bebas & thawaf sunnah", Jenis = "Ibadah", Lokasi = "Masjidil Haram", Latitude = 21.4225, Longitude = 39.8262, Urutan = 1 },
            new() { PaketId = pakets[0].Id, Hari = 5, Waktu = "07:00", Judul = "Perjalanan Makkah - Madinah", Jenis = "Transportasi", Lokasi = "Bus AC / Haramain", Urutan = 1, Deskripsi = "Perjalanan sekitar 5 jam, transit di Bir Ali untuk miqat." },
            new() { PaketId = pakets[0].Id, Hari = 5, Waktu = "16:00", Judul = "Check-in hotel Madinah", Jenis = "Hotel", Lokasi = "Dar Al Taqwa Hotel", Latitude = 24.4694, Longitude = 39.6108, Urutan = 2 },
            new() { PaketId = pakets[0].Id, Hari = 6, Waktu = "08:00", Judul = "Ziarah Masjid Quba & Kebun Kurma", Jenis = "Ziarah", Lokasi = "Masjid Quba", Latitude = 24.4394, Longitude = 39.6172, Urutan = 1 },
            new() { PaketId = pakets[0].Id, Hari = 6, Waktu = "10:00", Judul = "Ziarah Jabal Uhud & Makam Syuhada", Jenis = "Ziarah", Lokasi = "Jabal Uhud", Latitude = 24.5008, Longitude = 39.6142, Urutan = 2 },
            new() { PaketId = pakets[0].Id, Hari = 7, Waktu = "02:00", Judul = "Raudhah dan ziarah Makam Rasulullah", Jenis = "Ibadah", Lokasi = "Masjid Nabawi", Latitude = 24.4672, Longitude = 39.6112, Urutan = 1, Deskripsi = "Masuk sesuai jadwal tasreh, bawa paspor/identitas." },
            new() { PaketId = pakets[0].Id, Hari = 8, Waktu = "09:00", Judul = "Belanja oleh-oleh & persiapan pulang", Jenis = "Lainnya", Lokasi = "Pasar Kurma Madinah", Latitude = 24.4790, Longitude = 39.6110, Urutan = 1 },
            new() { PaketId = pakets[0].Id, Hari = 9, Waktu = "12:00", Judul = "Tiba kembali di Jakarta", Jenis = "Transportasi", Lokasi = "Terminal 3 CGK", Latitude = -6.1256, Longitude = 106.6559, Urutan = 1 },
            new() { PaketId = pakets[0].Id, Hari = 1, Waktu = "09:00", Judul = "Manasik akhir sebelum berangkat", Jenis = "Manasik", Lokasi = "Aula HolySafar Jakarta", Latitude = -6.2088, Longitude = 106.8456, Urutan = 0, Deskripsi = "Gladi bersih thawaf dan sai, pembagian buku doa." },

            new() { PaketId = pakets[1].Id, Hari = 1, Waktu = "10:00", Judul = "Keberangkatan CGK - JED", Jenis = "Transportasi", Lokasi = "Terminal 3 CGK", Latitude = -6.1256, Longitude = 106.6559, Urutan = 1 },
            new() { PaketId = pakets[1].Id, Hari = 2, Waktu = "14:00", Judul = "Umroh pertama", Jenis = "Ibadah", Lokasi = "Masjidil Haram", Latitude = 21.4225, Longitude = 39.8262, Urutan = 1 },
            new() { PaketId = pakets[1].Id, Hari = 6, Waktu = "08:00", Judul = "City tour Thaif", Jenis = "Ziarah", Lokasi = "Thaif", Latitude = 21.2854, Longitude = 40.4158, Urutan = 1 },
            new() { PaketId = pakets[1].Id, Hari = 10, Waktu = "09:00", Judul = "Ziarah Aqabah & Museum Nabawi", Jenis = "Ziarah", Lokasi = "Madinah", Latitude = 24.4672, Longitude = 39.6112, Urutan = 1 },

            new() { PaketId = pakets[2].Id, Hari = 1, Waktu = "08:00", Judul = "Manasik haji lengkap", Jenis = "Manasik", Lokasi = "Asrama Haji Pondok Gede", Latitude = -6.2833, Longitude = 106.9000, Urutan = 1 },
            new() { PaketId = pakets[2].Id, Hari = 5, Waktu = "05:00", Judul = "Wukuf di Arafah", Jenis = "Ibadah", Lokasi = "Padang Arafah", Latitude = 21.3549, Longitude = 39.9843, Urutan = 1, Deskripsi = "Puncak ibadah haji. Perbanyak doa dari zuhur hingga maghrib." },
            new() { PaketId = pakets[2].Id, Hari = 6, Waktu = "00:00", Judul = "Mabit di Muzdalifah & lempar jumrah", Jenis = "Ibadah", Lokasi = "Muzdalifah - Mina", Latitude = 21.3833, Longitude = 39.9370, Urutan = 1 },
        };
        db.ItineraryItems.AddRange(itinerary);

        // ===== ASURANSI PERJALANAN =====
        db.Asuransi.AddRange(new List<Asuransi>
        {
            new()
            {
                NamaProduk = "HolySafar Travel Protect",
                Penyedia = "PT Asuransi Jiwa Amanah",
                NilaiPertanggungan = 250000000,
                Premi = 350000,
                KontakKlaim = "claim@holysafar.com / 0800-1-472672",
                IsActive = true,
                Cakupan = @"Asuransi kesehatan selama di Tanah Suci
Asuransi kecelakaan diri 24 jam
Penggantian bagasi hilang & keterlambatan penerbangan
Evakuasi medis darurat
Repatriasi jenazah"
            },
            new()
            {
                NamaProduk = "Haji Plus Premium Cover",
                Penyedia = "PT Asuransi Takaful Umum",
                NilaiPertanggungan = 500000000,
                Premi = 750000,
                KontakKlaim = "klaim.haji@holysafar.com / 021-5000-472",
                IsActive = true,
                Cakupan = @"Seluruh manfaat Travel Protect
Santunan rawat inap hingga 30 hari
Perlindungan pembatalan perjalanan
Pendamping keluarga saat rawat inap"
            }
        });

        // ===== FORUM JAMAAH =====
        var topik1 = new ForumTopik { Judul = "Tips packing koper untuk umroh 9 hari", Kategori = "Perlengkapan", UserId = 4, CreatedAt = DateTime.UtcNow.AddDays(-6), JumlahDilihat = 42, Isi = "Assalamualaikum, ini pengalaman saya umroh tahun lalu: bawa koper 20kg saja, isi baju ihram 2 set, sandal jepit, obat pribadi, dan kantong kain untuk sandal saat masuk masjid. Sisakan ruang untuk oleh-oleh kurma." };
        var topik2 = new ForumTopik { Judul = "Bagaimana cara menjaga stamina saat thawaf?", Kategori = "Kesehatan", UserId = 5, CreatedAt = DateTime.UtcNow.AddDays(-3), JumlahDilihat = 27, Isi = "Saya sering kelelahan setelah 3 putaran. Ada yang punya tips latihan sebelum berangkat?" };
        var topik3 = new ForumTopik { Judul = "Pengalaman pertama masuk Raudhah", Kategori = "Pengalaman", UserId = 6, CreatedAt = DateTime.UtcNow.AddDays(-1), JumlahDilihat = 15, Isi = "Alhamdulillah dapat tasreh jam 2 pagi. Saran saya datang 45 menit lebih awal dan bawa botol minum kecil." };
        db.ForumTopik.AddRange(topik1, topik2, topik3);
        await db.SaveChangesAsync();

        db.ForumBalasan.AddRange(
            new ForumBalasan { TopikId = topik1.Id, UserId = 5, Isi = "Setuju! Tambahan: bawa power bank dan colokan universal, stop kontak di hotel tipe G.", CreatedAt = DateTime.UtcNow.AddDays(-5) },
            new ForumBalasan { TopikId = topik1.Id, UserId = 2, Isi = "Dari travel: pastikan obat pribadi disertai resep dokter ya, memudahkan pemeriksaan bandara.", CreatedAt = DateTime.UtcNow.AddDays(-5) },
            new ForumBalasan { TopikId = topik2.Id, UserId = 6, Isi = "Saya rutin jalan kaki 5 km tiap pagi selama 2 bulan sebelum berangkat, sangat membantu.", CreatedAt = DateTime.UtcNow.AddDays(-2) }
        );

        // ===== STATUS VISA & SISKOHAT CONTOH =====
        var jamaah1 = jamaahList[0];
        jamaah1.StatusVisa = VisaStatus.Terbit; jamaah1.NoVisa = "SA-2025-004471"; jamaah1.TanggalVisa = DateTime.UtcNow.AddDays(-20);
        jamaah1.SiskohatStatus = "Valid"; jamaah1.NoPorsi = "1000247881"; jamaah1.SiskohatSyncedAt = DateTime.UtcNow.AddDays(-18);
        jamaahList[1].StatusVisa = VisaStatus.Terbit; jamaahList[1].NoVisa = "SA-2025-004472"; jamaahList[1].TanggalVisa = DateTime.UtcNow.AddDays(-19);
        jamaahList[2].StatusVisa = VisaStatus.Diproses;
        jamaahList[3].StatusVisa = VisaStatus.Diajukan;

        // ===== CONTOH DOKUMEN TERUNGGAH =====
        db.DokumenJamaah.AddRange(
            new DokumenJamaah { JamaahId = jamaah1.Id, NamaDokumen = "KTP", TipeDokumen = "KTP", FileUrl = "/uploads/dokumen/contoh-ktp.jpg", FilePath = "/uploads/dokumen/contoh-ktp.jpg", FileSize = 184320, ContentType = "image/jpeg", Status = DocumentStatus.Verified, UploadedAt = DateTime.UtcNow.AddDays(-30) },
            new DokumenJamaah { JamaahId = jamaah1.Id, NamaDokumen = "Paspor", TipeDokumen = "Paspor", FileUrl = "/uploads/dokumen/contoh-paspor.jpg", FilePath = "/uploads/dokumen/contoh-paspor.jpg", FileSize = 220160, ContentType = "image/jpeg", Status = DocumentStatus.Verified, UploadedAt = DateTime.UtcNow.AddDays(-30) },
            new DokumenJamaah { JamaahId = jamaah1.Id, NamaDokumen = "Kartu Keluarga", TipeDokumen = "KK", FileUrl = "/uploads/dokumen/contoh-kk.pdf", FilePath = "/uploads/dokumen/contoh-kk.pdf", FileSize = 310272, ContentType = "application/pdf", Status = DocumentStatus.Verified, UploadedAt = DateTime.UtcNow.AddDays(-29) },
            new DokumenJamaah { JamaahId = jamaah1.Id, NamaDokumen = "Sertifikat Vaksin Meningitis", TipeDokumen = "Vaksin", FileUrl = "/uploads/dokumen/contoh-vaksin.pdf", FilePath = "/uploads/dokumen/contoh-vaksin.pdf", FileSize = 156672, ContentType = "application/pdf", Status = DocumentStatus.Verified, UploadedAt = DateTime.UtcNow.AddDays(-25) },
            new DokumenJamaah { JamaahId = jamaahList[2].Id, NamaDokumen = "KTP", TipeDokumen = "KTP", FileUrl = "/uploads/dokumen/contoh-ktp2.jpg", FilePath = "/uploads/dokumen/contoh-ktp2.jpg", FileSize = 172032, ContentType = "image/jpeg", Status = DocumentStatus.Submitted, UploadedAt = DateTime.UtcNow.AddDays(-4) }
        );

        await db.SaveChangesAsync();
    }

    /// <summary>Hash PBKDF2 yang sama dengan AuthService — jangan pakai SHA256 polos lagi.</summary>
    private static string HashPassword(string password) => HolySafar.Services.AuthService.HashPassword(password);
}
