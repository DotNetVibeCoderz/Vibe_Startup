# 📋 METADATA.md - Definisi Metadata Tanah & Bangunan

## 🏞️ Metadata Tanah (Land Metadata)

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| NomorSertifikat | string(100) | ✅ | Nomor sertifikat hak atas tanah |
| JenisHak | string(50) | ✅ | Hak Milik, HGB, HGU, Hak Pakai, Hak Sewa |
| Luas | decimal | ✅ | Luas tanah dalam m² |
| Lokasi | string(500) | ✅ | Alamat lengkap lokasi tanah |
| NIB | string(50) | ❌ | Nomor Identifikasi Bidang |
| Kelurahan | string(100) | ❌ | Nama kelurahan |
| Kecamatan | string(100) | ❌ | Nama kecamatan |
| KotaKabupaten | string(100) | ❌ | Nama kota/kabupaten |
| Provinsi | string(50) | ❌ | Nama provinsi |
| KodePos | string(10) | ❌ | Kode pos |
| Latitude | double | ❌ | Koordinat latitude |
| Longitude | double | ❌ | Koordinat longitude |
| PolygonGeoJson | string | ❌ | Koordinat polygon (GeoJSON) |
| NilaiNjopPerMeter | decimal | ❌ | Nilai NJOP per m² |
| TotalNjop | decimal | ❌ | Total NJOP |
| PajakTahunan | decimal | ❌ | Pajak tahunan |
| StatusPajak | string(20) | ❌ | Lunas / Menunggak / Bebas |
| Pemilik | string(200) | ✅ | Nama pemilik |
| NikPemilik | string(50) | ❌ | NIK pemilik |
| AlamatPemilik | string(200) | ❌ | Alamat pemilik |
| Keterangan | string(500) | ❌ | Keterangan tambahan |
| TanggalSertifikat | datetime | ❌ | Tanggal terbit sertifikat |

---

## 🏗️ Metadata Bangunan (Building Metadata)

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| NomorIimbPbg | string(100) | ✅ | Nomor IMB/PBG |
| NomorSertifikatTanah | string(50) | ❌ | Referensi sertifikat tanah |
| JenisBangunan | string(100) | ✅ | Rumah Tinggal, Ruko, Gudang, Kantor, dll |
| JumlahLantai | int | ✅ | Jumlah lantai |
| LuasBangunan | decimal | ✅ | Luas bangunan dalam m² |
| MaterialUtama | string(200) | ❌ | Beton, Kayu, Baja, dll |
| TahunPembangunan | int | ✅ | Tahun selesai pembangunan |
| FungsiBangunan | string(200) | ❌ | Hunian, Komersial, Industri, dll |
| Kepemilikan | string(200) | ❌ | Pribadi, PT, CV, Yayasan, dll |
| Lokasi | string(500) | ✅ | Alamat lengkap |
| NamaPemilik | string(500) | ❌ | Nama pemilik bangunan |
| Status | string(50) | ❌ | Aktif / Dalam Perbaikan / Tidak Aktif |
| NilaiBangunan | decimal | ❌ | Estimasi nilai bangunan |
| TanggalIimbPbg | datetime | ❌ | Tanggal terbit IMB/PBG |
