namespace VibeWallet.Models;

/// <summary>
/// All enums used across the VibeWallet application
/// </summary>

public enum TransactionType
{
    TopUp,           // Isi saldo
    Withdraw,        // Tarik tunai
    Transfer,        // Kirim uang
    Payment,         // Pembayaran
    Refund,          // Pengembalian dana
    Cashback,        // Cashback
    Fee,             // Biaya admin
    Adjustment       // Penyesuaian
}

public enum TransactionStatus
{
    Pending,
    Completed,
    Failed,
    Cancelled,
    Reversed,
    UnderReview
}

public enum PaymentMethod
{
    WalletBalance,   // Saldo wallet
    BankTransfer,    // Transfer bank
    VirtualAccount,  // Virtual account
    CreditCard,      // Kartu kredit
    DebitCard,       // Kartu debit
    QRIS,            // QRIS
    ConvenienceStore // Gerai ritel
}

public enum KycStatus
{
    NotSubmitted,
    Submitted,
    Verified,
    Rejected,
    Expired
}

public enum IdentityType
{
    KTP,             // Kartu Tanda Penduduk
    SIM,             // Surat Izin Mengemudi
    Passport,        // Paspor
    KITAS            // Kartu Izin Tinggal Terbatas
}

public enum Gender
{
    Male,
    Female,
    Other
}

public enum BillType
{
    Electricity,     // Listrik (PLN)
    Water,           // Air (PDAM)
    Internet,        // Internet
    TVCable,         // TV Kabel
    BPJS,            // BPJS Kesehatan/Ketenagakerjaan
    Education,       // Pendidikan
    Insurance        // Asuransi
}

public enum TopUpType
{
    Pulsa,           // Pulsa reguler
    DataPackage,     // Paket data
    ElectricityToken // Token listrik
}

public enum ProviderType
{
    Telkomsel,
    Indosat,
    XL,
    Tri,
    Smartfren,
    Axis,
    PLN
}

public enum RewardType
{
    Cashback,
    Points,
    Voucher,
    Discount
}

public enum PromoType
{
    Percentage,
    FixedAmount,
    FreeShipping,
    BuyOneGetOne
}

public enum InvestmentType
{
    Savings,         // Tabungan
    Deposit,         // Deposito
    MutualFund,      // Reksa dana
    Stock,           // Saham
    Gold,            // Emas digital
    Crypto           // Cryptocurrency (simulated)
}

public enum InsuranceType
{
    Health,          // Kesehatan
    Travel,          // Perjalanan
    Gadget,          // Gadget
    Vehicle,         // Kendaraan
    Life             // Jiwa
}

public enum FraudAlertLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum ChatProvider
{
    OpenAI,
    Anthropic,
    Gemini,
    Ollama
}

public enum AttachmentType
{
    Image,
    Document,
    Audio,
    Video,
    Other
}
