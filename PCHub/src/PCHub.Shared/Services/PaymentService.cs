using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using PCHub.Shared.DTOs;
using PCHub.Shared.Enums;
using PCHub.Shared.Interfaces;

namespace PCHub.Shared.Services;

public class PaymentService : IPaymentService
{
    private readonly IConfiguration? _config;
    private readonly HttpClient _http = new();
    private readonly string _midtransKey, _xenditKey;
    private readonly bool _isProd;

    public PaymentService(IConfiguration? config = null)
    {
        _config = config;
        _midtransKey = config?["Payment:Midtrans:ServerKey"] ?? "";
        _xenditKey = config?["Payment:Xendit:ApiKey"] ?? "";
        _isProd = bool.Parse(config?["Payment:Midtrans:IsProduction"] ?? "false");
    }

    public async Task<PaymentResponse> CreatePaymentAsync(CreatePaymentRequest request)
    {
        if (!string.IsNullOrEmpty(_midtransKey) && request.Method != PaymentMethod.Xendit)
            return await MidtransPay(request);
        if (!string.IsNullOrEmpty(_xenditKey))
            return await XenditPay(request);
        return SimulatePay(request);
    }

    private async Task<PaymentResponse> MidtransPay(CreatePaymentRequest req)
    {
        try
        {
            var baseUrl = _isProd ? "https://app.midtrans.com/snap/v1/transactions" : "https://app.sandbox.midtrans.com/snap/v1/transactions";
            var orderId = $"PCHUB-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..10];
            var payload = new { transaction_details = new { order_id = orderId, gross_amount = (int)req.Amount } };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(_midtransKey + ":")));
            var resp = await _http.PostAsync(baseUrl, content);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(body);
                return new PaymentResponse(Guid.NewGuid(), doc.RootElement.GetProperty("token").GetString() ?? "", "Pending", doc.RootElement.GetProperty("redirect_url").GetString() ?? "", req.Amount);
            }
        }
        catch { }
        return SimulatePay(req);
    }

    private async Task<PaymentResponse> XenditPay(CreatePaymentRequest req)
    {
        try
        {
            var payload = new { external_id = $"PCHUB-{Guid.NewGuid():N}"[..10], amount = (int)req.Amount, currency = "IDR" };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(_xenditKey + ":")));
            var resp = await _http.PostAsync("https://api.xendit.co/v2/invoices", content);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(body);
                return new PaymentResponse(Guid.NewGuid(), doc.RootElement.GetProperty("id").GetString() ?? "", "Pending", doc.RootElement.GetProperty("invoice_url").GetString() ?? "", req.Amount);
            }
        }
        catch { }
        return SimulatePay(req);
    }

    private static PaymentResponse SimulatePay(CreatePaymentRequest req) => new(Guid.NewGuid(), $"SIM-{DateTime.UtcNow.Ticks}", "Completed", null, req.Amount);

    public Task<PaymentResponse> CheckPaymentStatusAsync(string transactionId) => Task.FromResult(new PaymentResponse(Guid.NewGuid(), transactionId, "Completed", null, 0));
    public Task<bool> ProcessRefundAsync(string transactionId, decimal amount) => Task.FromResult(true);
}
