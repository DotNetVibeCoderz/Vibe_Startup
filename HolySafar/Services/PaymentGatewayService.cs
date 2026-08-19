using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HolySafar.Data;
using HolySafar.Models;
using Microsoft.EntityFrameworkCore;

namespace HolySafar.Services;

/// <summary>
/// Payment gateway multi-provider: Manual | Xendit | Midtrans | Stripe | QRIS.
/// Pola sama dengan StorageService/ChatbotService — provider dipilih dari konfigurasi
/// (appsettings.json bagian "Payment", bisa di-override lewat UI admin via SettingsService).
///
/// Alur: CreateTransactionAsync() -> jamaah membayar di halaman provider / scan QRIS ->
/// provider memanggil webhook /webhook/payment/{provider} -> MarkPaidAsync() menerapkan
/// efeknya ke Pembayaran/Cicilan atau Order marketplace.
/// </summary>
public class PaymentGatewayService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly SettingsService _settings;
    private readonly ILogger<PaymentGatewayService> _logger;

    public PaymentGatewayService(IServiceScopeFactory scopeFactory, IHttpClientFactory httpFactory,
        SettingsService settings, ILogger<PaymentGatewayService> logger)
    { _scopeFactory = scopeFactory; _httpFactory = httpFactory; _settings = settings; _logger = logger; }

    // ==================== KONFIGURASI ====================

    public PaymentProvider DefaultProvider =>
        Enum.TryParse<PaymentProvider>(_settings.Get("Payment:Provider", "Manual"), true, out var p) ? p : PaymentProvider.Manual;

    public bool IsSandbox => _settings.GetBool("Payment:Sandbox", true);
    public string Currency => _settings.Get("Payment:Currency", "IDR");

    /// <summary>Provider yang diaktifkan admin DAN kredensialnya sudah terisi.</summary>
    public List<PaymentProvider> EnabledProviders()
    {
        var raw = _settings.Get("Payment:EnabledProviders", "Manual,Xendit,Midtrans,Stripe,QRIS");
        var list = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse<PaymentProvider>(s, true, out var p) ? (PaymentProvider?)p : null)
            .Where(p => p != null).Select(p => p!.Value).Distinct().ToList();
        if (!list.Contains(PaymentProvider.Manual)) list.Insert(0, PaymentProvider.Manual);
        return list.Where(IsConfigured).ToList();
    }

    public bool IsConfigured(PaymentProvider p) => p switch
    {
        PaymentProvider.Manual => true,
        PaymentProvider.Xendit => !string.IsNullOrWhiteSpace(_settings.Get("Payment:Xendit:SecretKey")),
        PaymentProvider.Midtrans => !string.IsNullOrWhiteSpace(_settings.Get("Payment:Midtrans:ServerKey")),
        PaymentProvider.Stripe => !string.IsNullOrWhiteSpace(_settings.Get("Payment:Stripe:SecretKey")),
        PaymentProvider.QRIS => !string.IsNullOrWhiteSpace(_settings.Get("Payment:QRIS:StaticPayload"))
                                || !string.IsNullOrWhiteSpace(_settings.Get("Payment:QRIS:MerchantName")),
        _ => false
    };

    public static string Label(PaymentProvider p) => p switch
    {
        PaymentProvider.Manual => "Transfer Manual (upload bukti)",
        PaymentProvider.Xendit => "Xendit (VA / e-Wallet / Kartu)",
        PaymentProvider.Midtrans => "Midtrans Snap",
        PaymentProvider.Stripe => "Stripe Checkout (kartu internasional)",
        PaymentProvider.QRIS => "QRIS (scan semua e-wallet)",
        _ => p.ToString()
    };

    public static string Icon(PaymentProvider p) => p switch
    {
        PaymentProvider.Manual => "\U0001F3E6",
        PaymentProvider.Xendit => "\U0001F4B3",
        PaymentProvider.Midtrans => "\U0001F7E2",
        PaymentProvider.Stripe => "\U0001F535",
        PaymentProvider.QRIS => "\U0001F4F1",
        _ => "\U0001F4B0"
    };

    // ==================== MEMBUAT TRANSAKSI ====================

    public async Task<PaymentTransaction> CreateTransactionAsync(
        PaymentProvider provider, decimal jumlah, string deskripsi,
        string referenceType, int referenceId, ApplicationUser? user, string baseUrl)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var trx = new PaymentTransaction
        {
            KodeTransaksi = $"HS-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            Provider = provider,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            UserId = user?.Id,
            Jumlah = jumlah,
            Deskripsi = deskripsi,
            Status = TransactionStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ExpiredAt = DateTime.UtcNow.AddHours(_settings.GetInt("Payment:ExpiryHours", 24))
        };

        try
        {
            switch (provider)
            {
                case PaymentProvider.Xendit: await CreateXenditAsync(trx, user, baseUrl); break;
                case PaymentProvider.Midtrans: await CreateMidtransAsync(trx, user, baseUrl); break;
                case PaymentProvider.Stripe: await CreateStripeAsync(trx, user, baseUrl); break;
                case PaymentProvider.QRIS: CreateQris(trx); break;
                default: trx.Catatan = "Transfer manual — unggah bukti pembayaran setelah transfer."; break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal membuat transaksi {Provider}", provider);
            trx.Status = TransactionStatus.Failed;
            trx.Catatan = $"Gagal menghubungi {provider}: {ex.Message}";
        }

        db.PaymentTransactions.Add(trx);
        await db.SaveChangesAsync();
        return trx;
    }

    private HttpClient Http() => _httpFactory.CreateClient();

    private static string BasicAuth(string key) => Convert.ToBase64String(Encoding.UTF8.GetBytes(key + ":"));

    // ---------- XENDIT ----------
    private async Task CreateXenditAsync(PaymentTransaction trx, ApplicationUser? user, string baseUrl)
    {
        var secret = _settings.Get("Payment:Xendit:SecretKey");
        var http = Http();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", BasicAuth(secret));

        var body = new
        {
            external_id = trx.KodeTransaksi,
            amount = (long)decimal.Round(trx.Jumlah, 0),
            description = trx.Deskripsi,
            payer_email = string.IsNullOrWhiteSpace(user?.Email) ? "jamaah@holysafar.com" : user!.Email,
            invoice_duration = _settings.GetInt("Payment:ExpiryHours", 24) * 3600,
            success_redirect_url = $"{baseUrl}/pembayaran/selesai?kode={trx.KodeTransaksi}",
            failure_redirect_url = $"{baseUrl}/pembayaran/gagal?kode={trx.KodeTransaksi}",
            currency = Currency
        };

        var res = await http.PostAsJsonAsync("https://api.xendit.co/v2/invoices", body);
        var json = await res.Content.ReadAsStringAsync();
        trx.RawResponse = Truncate(json);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(json);
        trx.ExternalId = Str(doc.RootElement, "id");
        trx.PaymentUrl = Str(doc.RootElement, "invoice_url");
    }

    // ---------- MIDTRANS ----------
    private async Task CreateMidtransAsync(PaymentTransaction trx, ApplicationUser? user, string baseUrl)
    {
        var serverKey = _settings.Get("Payment:Midtrans:ServerKey");
        var endpoint = IsSandbox
            ? "https://app.sandbox.midtrans.com/snap/v1/transactions"
            : "https://app.midtrans.com/snap/v1/transactions";

        var http = Http();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", BasicAuth(serverKey));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var body = new
        {
            transaction_details = new { order_id = trx.KodeTransaksi, gross_amount = (long)decimal.Round(trx.Jumlah, 0) },
            customer_details = new
            {
                first_name = user?.FullName ?? "Jamaah",
                email = string.IsNullOrWhiteSpace(user?.Email) ? "jamaah@holysafar.com" : user!.Email,
                phone = user?.Phone ?? ""
            },
            item_details = new[]
            {
                new { id = trx.ReferenceType + trx.ReferenceId, price = (long)decimal.Round(trx.Jumlah, 0), quantity = 1, name = Trim(trx.Deskripsi, 50) }
            },
            callbacks = new { finish = $"{baseUrl}/pembayaran/selesai?kode={trx.KodeTransaksi}" }
        };

        var res = await http.PostAsJsonAsync(endpoint, body);
        var json = await res.Content.ReadAsStringAsync();
        trx.RawResponse = Truncate(json);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(json);
        trx.ExternalId = Str(doc.RootElement, "token");
        trx.PaymentUrl = Str(doc.RootElement, "redirect_url");
    }

    // ---------- STRIPE ----------
    private async Task CreateStripeAsync(PaymentTransaction trx, ApplicationUser? user, string baseUrl)
    {
        var secret = _settings.Get("Payment:Stripe:SecretKey");
        var currency = _settings.Get("Payment:Stripe:Currency", "idr").ToLowerInvariant();
        // IDR/JPY/KRW/VND adalah zero-decimal di Stripe; mata uang lain dikali 100.
        var zeroDecimal = currency is "idr" or "jpy" or "krw" or "vnd";
        var unitAmount = zeroDecimal ? (long)decimal.Round(trx.Jumlah, 0) : (long)decimal.Round(trx.Jumlah * 100, 0);

        var http = Http();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);

        var form = new Dictionary<string, string>
        {
            ["mode"] = "payment",
            ["client_reference_id"] = trx.KodeTransaksi,
            ["success_url"] = $"{baseUrl}/pembayaran/selesai?kode={trx.KodeTransaksi}",
            ["cancel_url"] = $"{baseUrl}/pembayaran/gagal?kode={trx.KodeTransaksi}",
            ["line_items[0][quantity]"] = "1",
            ["line_items[0][price_data][currency]"] = currency,
            ["line_items[0][price_data][unit_amount]"] = unitAmount.ToString(CultureInfo.InvariantCulture),
            ["line_items[0][price_data][product_data][name]"] = Trim(trx.Deskripsi, 100),
            ["metadata[kode_transaksi]"] = trx.KodeTransaksi,
            ["metadata[reference]"] = $"{trx.ReferenceType}:{trx.ReferenceId}"
        };

        var res = await http.PostAsync("https://api.stripe.com/v1/checkout/sessions", new FormUrlEncodedContent(form));
        var json = await res.Content.ReadAsStringAsync();
        trx.RawResponse = Truncate(json);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(json);
        trx.ExternalId = Str(doc.RootElement, "id");
        trx.PaymentUrl = Str(doc.RootElement, "url");
    }

    // ---------- QRIS ----------
    /// <summary>
    /// Membuat payload QRIS dinamis (EMVCo). Bila admin mengisi payload QRIS statis dari
    /// penyedia, payload itu diubah jadi dinamis + nominal; kalau tidak, payload dibangun
    /// dari data merchant. CRC16 selalu dihitung ulang.
    /// </summary>
    private void CreateQris(PaymentTransaction trx)
    {
        var statis = _settings.Get("Payment:QRIS:StaticPayload");
        var amount = decimal.Round(trx.Jumlah, 2).ToString("0.##", CultureInfo.InvariantCulture);

        trx.QrString = !string.IsNullOrWhiteSpace(statis)
            ? QrisFromStatic(statis.Trim(), amount)
            : QrisFromMerchant(
                _settings.Get("Payment:QRIS:MerchantName", "HOLYSAFAR TRAVEL"),
                _settings.Get("Payment:QRIS:MerchantCity", "JAKARTA"),
                _settings.Get("Payment:QRIS:NMID", "ID1234567890123"),
                _settings.Get("Payment:QRIS:MerchantId", "936000091234567890"),
                _settings.Get("Payment:QRIS:PostalCode", "10110"),
                amount);

        trx.ExternalId = trx.KodeTransaksi;
        trx.Catatan = "Scan QRIS dengan aplikasi e-wallet atau mobile banking apa pun.";
    }

    /// <summary>URL gambar QR untuk ditampilkan di UI.</summary>
    public string QrImageUrl(string payload, int size = 280) =>
        $"https://api.qrserver.com/v1/create-qr-code/?size={size}x{size}&data={Uri.EscapeDataString(payload)}";

    /// <summary>
    /// Ubah payload QRIS statis menjadi dinamis bernominal. Payload di-parse sebagai TLV
    /// EMVCo (bukan sekadar cari-ganti string) supaya tag 54 lama tidak terduplikasi dan
    /// urutan tag tetap sah bila penyedia memakai susunan yang berbeda.
    /// </summary>
    private static string QrisFromStatic(string statis, string amount)
    {
        var tlv = ParseTlv(statis);
        if (tlv.Count == 0) return statis;   // bukan TLV yang bisa dibaca, biarkan apa adanya

        tlv.RemoveAll(t => t.Tag == "63");   // CRC dihitung ulang
        tlv.RemoveAll(t => t.Tag == "54");   // nominal lama (kalau ada) dibuang

        var pfi = tlv.FindIndex(t => t.Tag == "01");
        if (pfi >= 0) tlv[pfi] = ("01", "12"); else tlv.Insert(1, ("01", "12"));

        // tag 54 harus berada sebelum tag 58 (country code)
        var posisi = tlv.FindIndex(t => t.Tag == "58");
        if (posisi < 0) posisi = tlv.Count;
        tlv.Insert(posisi, ("54", amount));

        var body = string.Concat(tlv.Select(t => Emv(t.Tag, t.Value))) + "6304";
        return body + Crc16(body);
    }

    /// <summary>Parse TLV level teratas: 2 digit tag + 2 digit panjang + nilai.</summary>
    private static List<(string Tag, string Value)> ParseTlv(string payload)
    {
        var hasil = new List<(string, string)>();
        var i = 0;
        while (i + 4 <= payload.Length)
        {
            var tag = payload.Substring(i, 2);
            if (!int.TryParse(payload.AsSpan(i + 2, 2), out var len)) return new();
            if (i + 4 + len > payload.Length) return new();
            hasil.Add((tag, payload.Substring(i + 4, len)));
            i += 4 + len;
        }
        return i == payload.Length ? hasil : new();
    }

    private static string QrisFromMerchant(string name, string city, string nmid, string merchantId, string postal, string amount)
    {
        var acc = Emv("00", "ID.CO.QRIS.WWW") + Emv("01", merchantId) + Emv("02", nmid) + Emv("03", "UMI");
        var sb = new StringBuilder();
        sb.Append(Emv("00", "01"));                        // payload format indicator
        sb.Append(Emv("01", "12"));                        // dinamis
        sb.Append(Emv("26", acc));                         // merchant account information
        sb.Append(Emv("52", "5499"));                      // merchant category code
        sb.Append(Emv("53", "360"));                       // currency IDR
        sb.Append(Emv("54", amount));                      // nominal
        sb.Append(Emv("58", "ID"));
        sb.Append(Emv("59", Trim(name.ToUpperInvariant(), 25)));
        sb.Append(Emv("60", Trim(city.ToUpperInvariant(), 15)));
        sb.Append(Emv("61", Trim(postal, 10)));
        var body = sb + "6304";
        return body + Crc16(body);
    }

    private static string Emv(string tag, string value) => $"{tag}{value.Length:D2}{value}";

    /// <summary>CRC-16/CCITT-FALSE sesuai spesifikasi EMVCo QR.</summary>
    private static string Crc16(string input)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.UTF8.GetBytes(input))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc.ToString("X4");
    }

    // ==================== STATUS & EFEK PEMBAYARAN ====================

    public async Task<PaymentTransaction?> GetByKodeAsync(string kode)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PaymentTransactions.AsNoTracking().FirstOrDefaultAsync(t => t.KodeTransaksi == kode);
    }

    /// <summary>
    /// Tandai transaksi lunas dan terapkan efeknya: menambah Cicilan + memutakhirkan
    /// Pembayaran, atau menandai Order marketplace sebagai dibayar. Idempotent.
    /// </summary>
    public async Task<bool> MarkPaidAsync(string kodeTransaksi, string? catatan = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trx = await db.PaymentTransactions.FirstOrDefaultAsync(t => t.KodeTransaksi == kodeTransaksi);
        if (trx == null) return false;
        if (trx.Status == TransactionStatus.Paid) return true;   // idempotent

        trx.Status = TransactionStatus.Paid;
        trx.PaidAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(catatan)) trx.Catatan = catatan;

        if (trx.ReferenceType == "Cicilan")
        {
            var pembayaran = await db.Pembayaran.FirstOrDefaultAsync(p => p.Id == trx.ReferenceId);
            if (pembayaran != null)
            {
                db.Cicilan.Add(new Cicilan
                {
                    PembayaranId = pembayaran.Id,
                    Jumlah = trx.Jumlah,
                    TanggalBayar = DateTime.UtcNow,
                    MetodePembayaran = trx.Provider.ToString(),
                    Catatan = $"Otomatis dari {trx.Provider} ({trx.KodeTransaksi})",
                    Dikonfirmasi = true
                });
                pembayaran.TotalDibayar += trx.Jumlah;
                pembayaran.MetodePembayaran = trx.Provider.ToString();
                pembayaran.Status = pembayaran.TotalDibayar >= pembayaran.TotalBiaya
                    ? PaymentStatus.Paid : PaymentStatus.Partial;

                db.Notifikasi.Add(new Notifikasi
                {
                    UserId = trx.UserId,
                    Judul = "Pembayaran diterima",
                    Pesan = $"Pembayaran {trx.Jumlah:C0} via {trx.Provider} telah kami terima. Sisa tagihan: {(pembayaran.TotalBiaya - pembayaran.TotalDibayar):C0}.",
                    Tipe = "success"
                });
            }
        }
        else if (trx.ReferenceType == "Order")
        {
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == trx.ReferenceId);
            if (order != null)
            {
                order.StatusOrder = "Paid";
                order.PaidAt = DateTime.UtcNow;
                order.MetodePembayaran = trx.Provider.ToString();
                db.Notifikasi.Add(new Notifikasi
                {
                    UserId = order.UserId,
                    Judul = "Pesanan dibayar",
                    Pesan = $"Pembayaran pesanan {order.NoOrder} sebesar {order.Total:C0} berhasil. Pesanan segera diproses.",
                    Tipe = "success"
                });
            }
        }

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkStatusAsync(string kodeTransaksi, TransactionStatus status, string? catatan = null)
    {
        if (status == TransactionStatus.Paid) return await MarkPaidAsync(kodeTransaksi, catatan);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trx = await db.PaymentTransactions.FirstOrDefaultAsync(t => t.KodeTransaksi == kodeTransaksi);
        if (trx == null || trx.Status == TransactionStatus.Paid) return false;
        trx.Status = status;
        if (!string.IsNullOrEmpty(catatan)) trx.Catatan = catatan;
        await db.SaveChangesAsync();
        return true;
    }

    // ==================== WEBHOOK ====================

    /// <summary>Verifikasi + proses callback provider. Mengembalikan (ok, pesan).</summary>
    public async Task<(bool Ok, string Message)> HandleWebhookAsync(string provider, string rawBody, IHeaderDictionary headers)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(rawBody) ? "{}" : rawBody);
            var root = doc.RootElement;

            switch (provider.ToLowerInvariant())
            {
                case "xendit":
                {
                    // Tanpa callback token, siapa pun bisa mengaku sebagai Xendit dan
                    // menandai tagihan lunas — jadi webhook ditolak sampai token diisi admin.
                    var expected = _settings.Get("Payment:Xendit:CallbackToken");
                    if (string.IsNullOrEmpty(expected))
                        return (false, "Payment:Xendit:CallbackToken belum diisi — webhook ditolak demi keamanan.");
                    if (!headers.TryGetValue("x-callback-token", out var token) ||
                        !AmanSama(token.ToString(), expected))
                        return (false, "Callback token tidak valid.");

                    var kode = Str(root, "external_id");
                    if (kode == null) return (false, "external_id kosong.");
                    var status = (Str(root, "status") ?? "").ToUpperInvariant();
                    return status switch
                    {
                        "PAID" or "SETTLED" => await Terapkan(kode, TransactionStatus.Paid, "Webhook Xendit"),
                        "EXPIRED" => await Terapkan(kode, TransactionStatus.Expired, "Webhook Xendit"),
                        _ => (true, $"status {status} diabaikan")
                    };
                }
                case "midtrans":
                {
                    var serverKey = _settings.Get("Payment:Midtrans:ServerKey");
                    if (string.IsNullOrEmpty(serverKey))
                        return (false, "Payment:Midtrans:ServerKey belum diisi — signature tidak bisa diverifikasi.");

                    var kode = Str(root, "order_id");
                    if (kode == null) return (false, "order_id kosong.");
                    var statusCode = Str(root, "status_code") ?? "";
                    var gross = Str(root, "gross_amount") ?? "";
                    var signature = Str(root, "signature_key") ?? "";

                    var expected = Sha512Hex(kode + statusCode + gross + serverKey);
                    if (!AmanSama(signature, expected))
                        return (false, "signature_key tidak valid.");

                    var trxStatus = (Str(root, "transaction_status") ?? "").ToLowerInvariant();
                    var fraud = (Str(root, "fraud_status") ?? "accept").ToLowerInvariant();
                    return trxStatus switch
                    {
                        "capture" or "settlement" when fraud != "deny" => await Terapkan(kode, TransactionStatus.Paid, "Webhook Midtrans"),
                        "expire" => await Terapkan(kode, TransactionStatus.Expired, "Webhook Midtrans"),
                        "cancel" or "deny" => await Terapkan(kode, TransactionStatus.Failed, "Webhook Midtrans"),
                        _ => (true, $"status {trxStatus} diabaikan")
                    };
                }
                case "stripe":
                {
                    var secret = _settings.Get("Payment:Stripe:WebhookSecret");
                    if (string.IsNullOrEmpty(secret))
                        return (false, "Payment:Stripe:WebhookSecret belum diisi — webhook ditolak demi keamanan.");
                    if (!headers.TryGetValue("Stripe-Signature", out var sig) ||
                        !VerifyStripeSignature(rawBody, sig.ToString(), secret))
                        return (false, "Stripe-Signature tidak valid.");

                    var type = Str(root, "type") ?? "";
                    if (!type.StartsWith("checkout.session")) return (true, $"event {type} diabaikan");
                    if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("object", out var obj))
                        return (false, "payload tanpa data.object");

                    var kode = Str(obj, "client_reference_id");
                    if (kode == null && obj.TryGetProperty("metadata", out var meta)) kode = Str(meta, "kode_transaksi");
                    if (kode == null) return (false, "client_reference_id kosong.");

                    return type == "checkout.session.completed"
                        ? await Terapkan(kode, TransactionStatus.Paid, "Webhook Stripe")
                        : await Terapkan(kode, TransactionStatus.Expired, "Webhook Stripe");
                }
                case "qris":
                {
                    // Format penyedia QRIS berbeda-beda; dipakai bentuk umum:
                    // { "kode_transaksi": "...", "status": "PAID" }
                    var expected = _settings.Get("Payment:QRIS:CallbackToken");
                    if (string.IsNullOrEmpty(expected))
                        return (false, "Payment:QRIS:CallbackToken belum diisi — konfirmasi QRIS harus lewat admin.");
                    if (!headers.TryGetValue("x-callback-token", out var token) ||
                        !AmanSama(token.ToString(), expected))
                        return (false, "Callback token tidak valid.");

                    var kode = Str(root, "kode_transaksi") ?? Str(root, "external_id") ?? Str(root, "order_id");
                    if (kode == null) return (false, "kode_transaksi kosong.");
                    var status = (Str(root, "status") ?? "PAID").ToUpperInvariant();
                    return status is "PAID" or "SUCCESS" or "SETTLED"
                        ? await Terapkan(kode, TransactionStatus.Paid, "Webhook QRIS")
                        : await Terapkan(kode, TransactionStatus.Failed, "Webhook QRIS");
                }
                default:
                    return (false, $"Provider '{provider}' tidak dikenal.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook {Provider} gagal diproses", provider);
            return (false, ex.Message);
        }
    }

    /// <summary>Terapkan status dari webhook dan kembalikan pesan yang informatif untuk log provider.</summary>
    private async Task<(bool Ok, string Message)> Terapkan(string kode, TransactionStatus status, string catatan)
    {
        var ok = await MarkStatusAsync(kode, status, catatan);
        if (ok) return (true, $"{kode} -> {status}");

        var trx = await GetByKodeAsync(kode);
        return trx == null
            ? (false, $"Transaksi {kode} tidak ditemukan.")
            : (true, $"Transaksi {kode} sudah berstatus {trx.Status}, callback diabaikan.");
    }

    /// <summary>Perbandingan string tahan timing-attack untuk token/signature.</summary>
    private static bool AmanSama(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        var ba = Encoding.UTF8.GetBytes(a.ToLowerInvariant());
        var bb = Encoding.UTF8.GetBytes(b.ToLowerInvariant());
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private static bool VerifyStripeSignature(string payload, string header, string secret)
    {
        // Format header: t=timestamp,v1=signature[,v1=...]
        string? t = null; var sigs = new List<string>();
        foreach (var part in header.Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0].Trim() == "t") t = kv[1].Trim();
            else if (kv[0].Trim() == "v1") sigs.Add(kv[1].Trim().ToLowerInvariant());
        }
        if (t == null || sigs.Count == 0) return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{t}.{payload}"))).ToLowerInvariant();
        var computedBytes = Encoding.UTF8.GetBytes(computed);
        return sigs.Any(s =>
        {
            var sb = Encoding.UTF8.GetBytes(s);
            return sb.Length == computedBytes.Length && CryptographicOperations.FixedTimeEquals(sb, computedBytes);
        });
    }

    private static string Sha512Hex(string input) =>
        Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    private static string? Str(JsonElement el, string prop) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v)
            ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString())
            : null;

    private static string Trim(string s, int max) => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);
    private static string Truncate(string s) => Trim(s, 3900);
}
