using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Service for payment operations: QRIS, bills, top-up, e-commerce
/// </summary>
public interface IPaymentService
{
    // QRIS
    Task<QrisPayment> ProcessQrisPaymentAsync(Guid userId, string qrContent, decimal amount, string? notes = null);
    Task<QrisPayment?> GetQrisPaymentAsync(string paymentRef);
    Task<string> GenerateQrCodeAsync(string content);

    // Bill Payment
    Task<BillPayment> PayBillAsync(Guid userId, BillType billType, string providerName, string customerId, decimal amount, string billPeriod);
    Task<List<BillPayment>> GetBillHistoryAsync(Guid userId, int page = 1, int pageSize = 20);
    Task<decimal> CheckBillAmountAsync(BillType billType, string customerId);

    // Mobile Top-up
    Task<MobileTopUp> ProcessTopUpAsync(Guid userId, TopUpType type, ProviderType provider, string phoneNumber, string productCode, decimal amount);
    Task<List<MobileTopUp>> GetTopUpHistoryAsync(Guid userId, int page = 1, int pageSize = 20);
    Task<List<(string Code, string Name, decimal Price)>> GetAvailableProductsAsync(ProviderType provider, TopUpType type);

    // E-commerce
    Task<EcommercePayment> ProcessEcommercePaymentAsync(Guid userId, string platform, string orderId, decimal amount, string? details = null);
}
