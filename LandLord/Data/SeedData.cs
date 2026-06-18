using LandLord.Models;
using Microsoft.EntityFrameworkCore;

namespace LandLord.Data;

/// <summary>
/// Seeder untuk mengisi database dengan data sampel — polygon & titik lengkap
/// </summary>
public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext context)
    {
        if (await context.Users.AnyAsync()) return;

        // ================================================================
        // USERS
        // ================================================================
        var users = new List<User>
        {
            new() { Username = "admin", Email = "admin@landlord.id", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"), FullName = "Administrator LandLord", Role = "Admin", PhoneNumber = "081234567890", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Username = "budi_santoso", Email = "budi@email.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("User123!"), FullName = "Budi Santoso", Role = "User", PhoneNumber = "081234567891", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Username = "siti_nurhaliza", Email = "siti@email.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("User123!"), FullName = "Siti Nurhaliza", Role = "User", PhoneNumber = "081234567892", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Username = "ahmad_ramadhan", Email = "ahmad@email.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("User123!"), FullName = "Ahmad Ramadhan", Role = "User", PhoneNumber = "081234567893", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Username = "dewi_lestari", Email = "dewi@email.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("User123!"), FullName = "Dewi Lestari", Role = "Viewer", PhoneNumber = "081234567894", IsActive = true, CreatedAt = DateTime.UtcNow }
        };
        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();

        // ================================================================
        // TANAH — Semua pakai polygon GeoJSON nyata
        // ================================================================
        var tanahList = new List<Tanah>
        {
            // 1. Jakarta Pusat — Bidang persegi di Sudirman
            new()
            {
                NomorSertifikat = "HM-001-2020-JKT", JenisHak = "Hak Milik", Luas = 250.00m,
                Lokasi = "Jl. Sudirman No. 123, RT 01 RW 02", NIB = "31710400010001",
                Kelurahan = "Bendungan Hilir", Kecamatan = "Tanah Abang", KotaKabupaten = "Jakarta Pusat",
                Provinsi = "DKI Jakarta", KodePos = "10210",
                Latitude = -6.211800, Longitude = 106.817800,
                PolygonGeoJson = @"{""type"":""Polygon"",""coordinates"":[[[106.8176,-6.2116],[106.8180,-6.2116],[106.8180,-6.2120],[106.8176,-6.2120],[106.8176,-6.2116]]]}",
                NilaiNjopPerMeter = 15000000, TotalNjop = 3750000000, PajakTahunan = 37500000,
                StatusPajak = "Lunas", Pemilik = "Budi Santoso", NikPemilik = "3171041234560001",
                AlamatPemilik = "Jl. Sudirman No. 123, Jakarta Pusat",
                TanggalSertifikat = new DateTime(2020, 3, 15), CreatedBy = "admin"
            },
            // 2. Bandung — Bidang berbentuk L di Braga
            new()
            {
                NomorSertifikat = "HGB-045-2019-BDG", JenisHak = "Hak Guna Bangunan", Luas = 500.00m,
                Lokasi = "Jl. Asia Afrika No. 45", NIB = "32730200020001",
                Kelurahan = "Braga", Kecamatan = "Sumur Bandung", KotaKabupaten = "Bandung",
                Provinsi = "Jawa Barat", KodePos = "40111",
                Latitude = -6.917500, Longitude = 107.609000,
                PolygonGeoJson = @"{""type"":""Polygon"",""coordinates"":[[[107.6088,-6.9173],[107.6094,-6.9173],[107.6094,-6.9176],[107.6092,-6.9176],[107.6092,-6.9179],[107.6088,-6.9179],[107.6088,-6.9173]]]}",
                NilaiNjopPerMeter = 8000000, TotalNjop = 4000000000, PajakTahunan = 40000000,
                StatusPajak = "Lunas", Pemilik = "PT Maju Jaya Properti", NikPemilik = "3273029876540001",
                TanggalSertifikat = new DateTime(2019, 7, 20), CreatedBy = "admin"
            },
            // 3. Surabaya — Bidang segitiga di Wonokromo
            new()
            {
                NomorSertifikat = "HM-089-2018-SBY", JenisHak = "Hak Milik", Luas = 180.00m,
                Lokasi = "Jl. Raya Darmo No. 67", NIB = "35780100030001",
                Kelurahan = "Wonokromo", Kecamatan = "Wonokromo", KotaKabupaten = "Surabaya",
                Provinsi = "Jawa Timur", KodePos = "60241",
                Latitude = -7.275500, Longitude = 112.733300,
                PolygonGeoJson = @"{""type"":""Polygon"",""coordinates"":[[[112.7331,-7.2754],[112.7337,-7.2754],[112.7334,-7.2759],[112.7331,-7.2759],[112.7331,-7.2754]]]}",
                NilaiNjopPerMeter = 10000000, TotalNjop = 1800000000, PajakTahunan = 18000000,
                StatusPajak = "Lunas", Pemilik = "Siti Nurhaliza", NikPemilik = "3578012345670001",
                TanggalSertifikat = new DateTime(2018, 11, 8), CreatedBy = "admin"
            },
            // 4. Deli Serdang — Bidang besar persegi panjang (HGU)
            new()
            {
                NomorSertifikat = "HGU-012-2021-MDN", JenisHak = "Hak Guna Usaha", Luas = 5000.00m,
                Lokasi = "Jl. Medan Industrial Park Blok A", NIB = "12750100040001",
                Kelurahan = "Medan Estate", Kecamatan = "Percut Sei Tuan", KotaKabupaten = "Deli Serdang",
                Provinsi = "Sumatera Utara", KodePos = "20371",
                Latitude = 3.595200, Longitude = 98.670200,
                PolygonGeoJson = @"{""type"":""Polygon"",""coordinates"":[[[98.6698,3.5948],[98.6706,3.5948],[98.6706,3.5956],[98.6698,3.5956],[98.6698,3.5948]]]}",
                NilaiNjopPerMeter = 2000000, TotalNjop = 10000000000, PajakTahunan = 100000000,
                StatusPajak = "Menunggak", Pemilik = "PT Perkebunan Nusantara Sejahtera",
                TanggalSertifikat = new DateTime(2021, 1, 25), CreatedBy = "admin"
            },
            // 5. Yogyakarta — Bidang kecil di Malioboro
            new()
            {
                NomorSertifikat = "HM-156-2017-YGY", JenisHak = "Hak Milik", Luas = 300.00m,
                Lokasi = "Jl. Malioboro No. 12", NIB = "34710100050001",
                Kelurahan = "Sosromenduran", Kecamatan = "Gedong Tengen", KotaKabupaten = "Yogyakarta",
                Provinsi = "DI Yogyakarta", KodePos = "55271",
                Latitude = -7.795600, Longitude = 110.369500,
                PolygonGeoJson = @"{""type"":""Polygon"",""coordinates"":[[[110.3693,-7.7954],[110.3697,-7.7954],[110.3697,-7.7958],[110.3693,-7.7958],[110.3693,-7.7954]]]}",
                NilaiNjopPerMeter = 12000000, TotalNjop = 3600000000, PajakTahunan = 36000000,
                StatusPajak = "Lunas", Pemilik = "Ahmad Ramadhan", NikPemilik = "3471013456780001",
                TanggalSertifikat = new DateTime(2017, 5, 10), CreatedBy = "admin"
            },
            // 6. Bali — Villa di Seminyak
            new()
            {
                NomorSertifikat = "HM-203-2022-DPS", JenisHak = "Hak Milik", Luas = 150.00m,
                Lokasi = "Jl. Sunset Road No. 88", NIB = "51710100060001",
                Kelurahan = "Seminyak", Kecamatan = "Kuta", KotaKabupaten = "Badung",
                Provinsi = "Bali", KodePos = "80361",
                Latitude = -8.691400, Longitude = 115.164700,
                PolygonGeoJson = @"{""type"":""Polygon"",""coordinates"":[[[115.1645,-8.6912],[115.1649,-8.6912],[115.1649,-8.6916],[115.1645,-8.6916],[115.1645,-8.6912]]]}",
                NilaiNjopPerMeter = 25000000, TotalNjop = 3750000000, PajakTahunan = 37500000,
                StatusPajak = "Lunas", Pemilik = "Dewi Lestari", NikPemilik = "5171014567890001",
                TanggalSertifikat = new DateTime(2022, 9, 1), CreatedBy = "admin"
            },
            // 7. Makassar — Gudang besar
            new()
            {
                NomorSertifikat = "HGB-078-2020-MKS", JenisHak = "Hak Guna Bangunan", Luas = 750.00m,
                Lokasi = "Jl. Pettarani No. 55", NIB = "73710100070001",
                Kelurahan = "Panakkukang", Kecamatan = "Panakkukang", KotaKabupaten = "Makassar",
                Provinsi = "Sulawesi Selatan", KodePos = "90231",
                Latitude = -5.147700, Longitude = 119.432300,
                PolygonGeoJson = @"{""type"":""Polygon"",""coordinates"":[[[119.4320,-5.1475],[119.4326,-5.1475],[119.4326,-5.1479],[119.4320,-5.1479],[119.4320,-5.1475]]]}",
                NilaiNjopPerMeter = 5000000, TotalNjop = 3750000000, PajakTahunan = 37500000,
                StatusPajak = "Lunas", Pemilik = "PT Sulawesi Makmur Abadi",
                TanggalSertifikat = new DateTime(2020, 12, 3), CreatedBy = "admin"
            },
            // 8. Batam — Kantor segi lima
            new()
            {
                NomorSertifikat = "HM-310-2016-BTM", JenisHak = "Hak Milik", Luas = 200.00m,
                Lokasi = "Jl. Hang Tuah No. 10", NIB = "21710100080001",
                Kelurahan = "Batu Ampar", Kecamatan = "Batu Ampar", KotaKabupaten = "Batam",
                Provinsi = "Kepulauan Riau", KodePos = "29432",
                Latitude = 1.128300, Longitude = 104.059800,
                PolygonGeoJson = @"{""type"":""Polygon"",""coordinates"":[[[104.0596,1.1281],[104.0600,1.1281],[104.0601,1.1283],[104.0600,1.1285],[104.0596,1.1285],[104.0595,1.1283],[104.0596,1.1281]]]}",
                NilaiNjopPerMeter = 6000000, TotalNjop = 1200000000, PajakTahunan = 12000000,
                StatusPajak = "Lunas", Pemilik = "Budi Santoso", NikPemilik = "3171041234560001",
                TanggalSertifikat = new DateTime(2016, 8, 18), CreatedBy = "admin"
            }
        };
        await context.Tanah.AddRangeAsync(tanahList);
        await context.SaveChangesAsync();

        // ================================================================
        // BANGUNAN — Polygon footprint + data lengkap
        // ================================================================
        var bangunanList = new List<Bangunan>
        {
            new()
            {
                NomorIimbPbg = "IMB-001-2020-JKT", NomorSertifikatTanah = "HM-001-2020-JKT",
                JenisBangunan = "Rumah Tinggal", JumlahLantai = 2, LuasBangunan = 200.00m,
                MaterialUtama = "Bata & Beton", TahunPembangunan = 2020, FungsiBangunan = "Hunian",
                Kepemilikan = "Pribadi",
                Lokasi = "Jl. Sudirman No. 123, RT 01 RW 02",
                Kelurahan = "Bendungan Hilir", Kecamatan = "Tanah Abang", KotaKabupaten = "Jakarta Pusat",
                Provinsi = "DKI Jakarta", Latitude = -6.211800, Longitude = 106.817800,
                PolygonGeoJson = @"{""type"":""Polygon"",""coordinates"":[[[106.8177,-6.2117],[106.8179,-6.2117],[106.8179,-6.2119],[106.8177,-6.2119],[106.8177,-6.2117]]]}",
                NamaPemilik = "Budi Santoso", NikPemilik = "3171041234560001",
                Status = "Aktif", TanggalIimbPbg = new DateTime(2020, 4, 10),
                NilaiBangunan = 1500000000, CreatedBy = "admin"
            },
            new()
            {
                NomorIimbPbg = "PBG-045-2019-BDG", NomorSertifikatTanah = "HGB-045-2019-BDG",
                JenisBangunan = "Ruko", JumlahLantai = 3, LuasBangunan = 450.00m,
                MaterialUtama = "Baja & Beton", TahunPembangunan = 2019, FungsiBangunan = "Komersial",
                Kepemilikan = "PT",
                Lokasi = "Jl. Asia Afrika No. 45",
                Kelurahan = "Braga", Kecamatan = "Sumur Bandung", KotaKabupaten = "Bandung",
                Provinsi = "Jawa Barat", Latitude = -6.917500, Longitude = 107.609000,
                PolygonGeoJson = @"{""type"":""Polygon"",""coordinates"":[[[107.6089,-6.9174],[107.6093,-6.9174],[107.6093,-6.9178],[107.6091,-6.9178],[107.6091,-6.9175],[107.6089,-6.9175],[107.6089,-6.9174]]]}",
                NamaPemilik = "PT Maju Jaya Properti",
                Status = "Aktif", TanggalIimbPbg = new DateTime(2019, 8, 15),
                NilaiBangunan = 3000000000, CreatedBy = "admin"
            },
            new()
            {
                NomorIimbPbg = "IMB-089-2018-SBY", NomorSertifikatTanah = "HM-089-2018-SBY",
                JenisBangunan = "Apartemen", JumlahLantai = 12, LuasBangunan = 8000.00m,
                MaterialUtama = "Beton Bertulang", TahunPembangunan = 2018, FungsiBangunan = "Hunian",
                Kepemilikan = "PT",
                Lokasi = "Jl. Raya Darmo No. 67",
                Kelurahan = "Wonokromo", Kecamatan = "Wonokromo", KotaKabupaten = "Surabaya",
                Provinsi = "Jawa Timur", Latitude = -7.275500, Longitude = 112.733300,
                PolygonGeoJson = @"{""type"":""Polygon"",""coordinates"":[[[112.7332,-7.2753],[112.7336,-7.2753],[112.7336,-7.2759],[112.7332,-7.2759],[112.7332,-7.2753]]]}",
                NamaPemilik = "PT Graha Surabaya Indah",
                Status = "Aktif", TanggalIimbPbg = new DateTime(2018, 12, 1),
                NilaiBangunan = 50000000000, CreatedBy = "admin"
            },
            new()
            {
                NomorIimbPbg = "IMB-156-2017-YGY", NomorSertifikatTanah = "HM-156-2017-YGY",
                JenisBangunan = "Hotel", JumlahLantai = 5, LuasBangunan = 2500.00m,
                MaterialUtama = "Beton & Kaca", TahunPembangunan = 2017, FungsiBangunan = "Komersial",
                Kepemilikan = "PT",
                Lokasi = "Jl. Malioboro No. 12",
                Kelurahan = "Sosromenduran", Kecamatan = "Gedong Tengen", KotaKabupaten = "Yogyakarta",
                Provinsi = "DI Yogyakarta", Latitude = -7.795600, Longitude = 110.369500,
                PolygonGeoJson = @"{""type"":""Polygon"",""coordinates"":[[[110.3694,-7.7955],[110.3696,-7.7955],[110.3696,-7.7957],[110.3694,-7.7957],[110.3694,-7.7955]]]}",
                NamaPemilik = "PT Malioboro Hospitality",
                Status = "Aktif", TanggalIimbPbg = new DateTime(2017, 6, 20),
                NilaiBangunan = 15000000000, CreatedBy = "admin"
            },
            new()
            {
                NomorIimbPbg = "PBG-203-2022-DPS", NomorSertifikatTanah = "HM-203-2022-DPS",
                JenisBangunan = "Villa", JumlahLantai = 1, LuasBangunan = 120.00m,
                MaterialUtama = "Kayu & Batu Alam", TahunPembangunan = 2022, FungsiBangunan = "Hunian",
                Kepemilikan = "Pribadi",
                Lokasi = "Jl. Sunset Road No. 88",
                Kelurahan = "Seminyak", Kecamatan = "Kuta", KotaKabupaten = "Badung",
                Provinsi = "Bali", Latitude = -8.691400, Longitude = 115.164700,
                PolygonGeoJson = @"{""type"":""Polygon"",""coordinates"":[[[115.1646,-8.6913],[115.1648,-8.6913],[115.1648,-8.6915],[115.1646,-8.6915],[115.1646,-8.6913]]]}",
                NamaPemilik = "Dewi Lestari", NikPemilik = "5171014567890001",
                Status = "Aktif", TanggalIimbPbg = new DateTime(2022, 10, 5),
                NilaiBangunan = 800000000, CreatedBy = "admin"
            },
            new()
            {
                NomorIimbPbg = "IMB-078-2020-MKS", NomorSertifikatTanah = "HGB-078-2020-MKS",
                JenisBangunan = "Gudang", JumlahLantai = 1, LuasBangunan = 600.00m,
                MaterialUtama = "Baja Ringan & Beton", TahunPembangunan = 2020, FungsiBangunan = "Industri",
                Kepemilikan = "PT",
                Lokasi = "Jl. Pettarani No. 55",
                Kelurahan = "Panakkukang", Kecamatan = "Panakkukang", KotaKabupaten = "Makassar",
                Provinsi = "Sulawesi Selatan", Latitude = -5.147700, Longitude = 119.432300,
                PolygonGeoJson = @"{""type"":""Polygon"",""coordinates"":[[[119.4321,-5.1476],[119.4325,-5.1476],[119.4325,-5.1478],[119.4321,-5.1478],[119.4321,-5.1476]]]}",
                NamaPemilik = "PT Sulawesi Makmur Abadi",
                Status = "Aktif", TanggalIimbPbg = new DateTime(2021, 2, 15),
                NilaiBangunan = 2500000000, CreatedBy = "admin"
            },
            new()
            {
                NomorIimbPbg = "IMB-310-2016-BTM", NomorSertifikatTanah = "HM-310-2016-BTM",
                JenisBangunan = "Kantor", JumlahLantai = 3, LuasBangunan = 350.00m,
                MaterialUtama = "Beton & Kaca", TahunPembangunan = 2016, FungsiBangunan = "Komersial",
                Kepemilikan = "Pribadi",
                Lokasi = "Jl. Hang Tuah No. 10",
                Kelurahan = "Batu Ampar", Kecamatan = "Batu Ampar", KotaKabupaten = "Batam",
                Provinsi = "Kepulauan Riau", Latitude = 1.128300, Longitude = 104.059800,
                PolygonGeoJson = @"{""type"":""Polygon"",""coordinates"":[[[104.0597,1.1282],[104.0599,1.1282],[104.0600,1.1283],[104.0599,1.1284],[104.0597,1.1284],[104.0596,1.1283],[104.0597,1.1282]]]}",
                NamaPemilik = "Budi Santoso", NikPemilik = "3171041234560001",
                Status = "Dalam Perbaikan", TanggalIimbPbg = new DateTime(2016, 9, 20),
                NilaiBangunan = 1800000000, CreatedBy = "admin"
            }
        };
        await context.Bangunan.AddRangeAsync(bangunanList);
        await context.SaveChangesAsync();
    }
}
