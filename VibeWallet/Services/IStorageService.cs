namespace VibeWallet.Services;

/// <summary>
/// Service for file storage with multiple provider support
/// Providers: FileSystem, AzureBlob, S3, MinIO
/// </summary>
public interface IStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string folder = "");
    Task<Stream?> DownloadFileAsync(string fileUrl);
    Task<bool> DeleteFileAsync(string fileUrl);
    Task<string> GetFileUrlAsync(string fileName, string folder = "");
    Task<bool> FileExistsAsync(string fileUrl);
    Task<long> GetFileSizeAsync(string fileUrl);
}

/// <summary>
/// Service for bank integration
/// </summary>
public interface IBankService
{
    Task<List<Models.SupportedBank>> GetSupportedBanksAsync();
    Task<Models.SupportedBank?> GetBankByCodeAsync(string bankCode);
    Task<List<Models.BankAccount>> GetUserBankAccountsAsync(Guid userId);
    Task<Models.BankAccount> AddBankAccountAsync(Guid userId, string bankName, string bankCode, string accountNumber, string accountHolderName);
    Task<bool> RemoveBankAccountAsync(Guid userId, Guid bankAccountId);
    Task<bool> SetPrimaryBankAccountAsync(Guid userId, Guid bankAccountId);
    Task<Models.BankTransfer> TransferToBankAsync(Guid userId, string destinationBankCode, string destinationAccountNumber, decimal amount, string? notes = null);
}

/// <summary>
/// Service for KYC verification
/// </summary>
public interface IKycService
{
    Task<Models.KycDocument> UploadKycDocumentAsync(Guid userId, Stream fileStream, string fileName, string contentType);
    Task<Models.KycSelfie> UploadSelfieAsync(Guid userId, Stream fileStream, string fileName, string contentType);
    Task<bool> SubmitKycAsync(Guid userId);
    Task<Models.KycStatus> GetKycStatusAsync(Guid userId);
    Task<bool> VerifyKycAsync(Guid userId, string verifiedBy);
    Task<bool> RejectKycAsync(Guid userId, string reason, string rejectedBy);
    Task<List<Models.KycDocument>> GetUserKycDocumentsAsync(Guid userId);
}

/// <summary>
/// Service for investment products
/// </summary>
public interface IInvestmentService
{
    // Savings
    Task<Models.SavingsAccount> CreateSavingsAccountAsync(Guid userId, string accountName, decimal initialDeposit);
    Task<List<Models.SavingsAccount>> GetUserSavingsAccountsAsync(Guid userId);
    Task<Models.SavingsTransaction> DepositToSavingsAsync(Guid savingsAccountId, decimal amount);
    Task<Models.SavingsTransaction> WithdrawFromSavingsAsync(Guid savingsAccountId, decimal amount);
    Task CalculateMonthlyInterestAsync();

    // Investments
    Task<List<Models.Investment>> GetAvailableInvestmentsAsync();
    Task<Models.Investment> InvestAsync(Guid userId, string productCode, decimal amount);
    Task<List<Models.Investment>> GetUserInvestmentsAsync(Guid userId);

    // Insurance
    Task<List<Models.InsuranceProduct>> GetInsuranceProductsAsync();
    Task<Models.UserInsurance> EnrollInsuranceAsync(Guid userId, Guid productId);
    Task<List<Models.UserInsurance>> GetUserInsurancesAsync(Guid userId);
}

/// <summary>
/// Service for notifications
/// </summary>
public interface INotificationService
{
    Task SendTransactionNotificationAsync(Guid userId, string title, string message);
    Task SendPromoNotificationAsync(Guid userId, string title, string message);
    Task SendSecurityAlertAsync(Guid userId, string title, string message);
}
