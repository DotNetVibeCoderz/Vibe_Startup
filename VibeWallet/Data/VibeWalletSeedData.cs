using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VibeWallet.Models;

namespace VibeWallet.Data;

/// <summary>
/// Seeds the database with sample data for development and testing
/// </summary>
public static class VibeWalletSeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VibeWalletDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<VibeUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Create roles
        await EnsureRolesAsync(roleManager);

        // Create sample users
        var users = await CreateSampleUsersAsync(userManager, context);

        // Only seed if no data exists
        if (await context.Wallets.AnyAsync()) return;

        // Create wallets for users
        await CreateSampleWalletsAsync(context, users);

        // Create supported banks
        await CreateSupportedBanksAsync(context);

        // Create bank accounts for users
        await CreateBankAccountsAsync(context, users);

        // Create sample transactions
        await CreateSampleTransactionsAsync(context, users);

        // Create sample vouchers and promos
        await CreateSampleVouchersAndPromosAsync(context);

        // Create sample chat sessions
        await CreateSampleChatSessionsAsync(context, users);

        // Create sample insurance products
        await CreateSampleInsuranceProductsAsync(context);

        await context.SaveChangesAsync();
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        string[] roles = { "Admin", "User", "Merchant", "Premium" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid> { Name = role });
            }
        }
    }

    private static async Task<List<VibeUser>> CreateSampleUsersAsync(UserManager<VibeUser> userManager, VibeWalletDbContext context)
    {
        var users = new List<VibeUser>();

        var sampleUsers = new List<(VibeUser user, string password, string role)>
        {
            (new VibeUser
            {
                UserName = "admin@vibewallet.id",
                Email = "admin@vibewallet.id",
                EmailConfirmed = true,
                PhoneNumber = "+6281200000001",
                PhoneNumberConfirmed = true,
                FullName = "Admin VibeWallet",
                KycStatus = KycStatus.Verified,
                IdentityNumber = "3174010101900001",
                IdentityType = IdentityType.KTP,
                DateOfBirth = new DateTime(1990, 1, 1),
                Gender = Gender.Male,
                Address = "Jl. Sudirman No. 1, Jakarta Pusat",
                City = "Jakarta Pusat",
                Province = "DKI Jakarta",
                PostalCode = "10220",
                ThemePreference = "dark",
                TransactionPin = BCrypt.Net.BCrypt.HashPassword("123456"),
                KycSubmittedAt = DateTime.UtcNow.AddDays(-30),
                KycVerifiedAt = DateTime.UtcNow.AddDays(-29)
            }, "Admin123!", "Admin"),

            (new VibeUser
            {
                UserName = "budi@email.com",
                Email = "budi@email.com",
                EmailConfirmed = true,
                PhoneNumber = "+6281200000002",
                PhoneNumberConfirmed = true,
                FullName = "Budi Santoso",
                KycStatus = KycStatus.Verified,
                IdentityNumber = "3174020202900002",
                IdentityType = IdentityType.KTP,
                DateOfBirth = new DateTime(1990, 2, 2),
                Gender = Gender.Male,
                Address = "Jl. Melati No. 5, Bandung",
                City = "Bandung",
                Province = "Jawa Barat",
                PostalCode = "40111",
                ThemePreference = "light",
                TransactionPin = BCrypt.Net.BCrypt.HashPassword("123456"),
                KycSubmittedAt = DateTime.UtcNow.AddDays(-20),
                KycVerifiedAt = DateTime.UtcNow.AddDays(-19)
            }, "User123!", "User"),

            (new VibeUser
            {
                UserName = "siti@email.com",
                Email = "siti@email.com",
                EmailConfirmed = true,
                PhoneNumber = "+6281200000003",
                PhoneNumberConfirmed = true,
                FullName = "Siti Nurhaliza",
                KycStatus = KycStatus.Verified,
                IdentityNumber = "3174030303900003",
                IdentityType = IdentityType.KTP,
                DateOfBirth = new DateTime(1993, 3, 3),
                Gender = Gender.Female,
                Address = "Jl. Mawar No. 10, Surabaya",
                City = "Surabaya",
                Province = "Jawa Timur",
                PostalCode = "60211",
                ThemePreference = "light",
                TransactionPin = BCrypt.Net.BCrypt.HashPassword("123456"),
                KycSubmittedAt = DateTime.UtcNow.AddDays(-15),
                KycVerifiedAt = DateTime.UtcNow.AddDays(-14)
            }, "User123!", "User"),

            (new VibeUser
            {
                UserName = "andi@email.com",
                Email = "andi@email.com",
                EmailConfirmed = true,
                PhoneNumber = "+6281200000004",
                PhoneNumberConfirmed = true,
                FullName = "Andi Pratama",
                KycStatus = KycStatus.NotSubmitted,
                IdentityNumber = null,
                DateOfBirth = new DateTime(1995, 6, 15),
                Gender = Gender.Male,
                Address = "Jl. Kenanga No. 8, Yogyakarta",
                City = "Yogyakarta",
                Province = "DIY Yogyakarta",
                PostalCode = "55111",
                ThemePreference = "dark",
                TransactionPin = BCrypt.Net.BCrypt.HashPassword("123456")
            }, "User123!", "User"),

            (new VibeUser
            {
                UserName = "merchant@toko.id",
                Email = "merchant@toko.id",
                EmailConfirmed = true,
                PhoneNumber = "+6281200000005",
                PhoneNumberConfirmed = true,
                FullName = "Toko Berkah",
                KycStatus = KycStatus.Verified,
                IdentityNumber = "3174050505950005",
                IdentityType = IdentityType.KTP,
                DateOfBirth = new DateTime(1985, 5, 10),
                Gender = Gender.Other,
                Address = "Jl. Dagangan No. 3, Jakarta Selatan",
                City = "Jakarta Selatan",
                Province = "DKI Jakarta",
                PostalCode = "12110",
                ThemePreference = "light",
                TransactionPin = BCrypt.Net.BCrypt.HashPassword("123456"),
                KycSubmittedAt = DateTime.UtcNow.AddDays(-10),
                KycVerifiedAt = DateTime.UtcNow.AddDays(-9)
            }, "Merchant123!", "Merchant"),
        };

        foreach (var (user, password, role) in sampleUsers)
        {
            if (await userManager.FindByNameAsync(user.UserName!) == null)
            {
                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                    users.Add(user);
                }
            }
            else
            {
                var existing = await userManager.FindByNameAsync(user.UserName!);
                if (existing != null) users.Add(existing);
            }
        }

        return users;
    }

    private static async Task CreateSampleWalletsAsync(VibeWalletDbContext context, List<VibeUser> users)
    {
        var wallets = new List<Wallet>
        {
            new Wallet
            {
                UserId = users[0].Id, WalletNumber = "1000000000000001",
                Balance = 10000000, TotalTopUp = 25000000, TotalSpending = 15000000,
                LoyaltyPoints = 5000, IsActive = true
            },
            new Wallet
            {
                UserId = users[1].Id, WalletNumber = "1000000000000002",
                Balance = 5000000, TotalTopUp = 15000000, TotalSpending = 10000000,
                LoyaltyPoints = 2500, IsActive = true
            },
            new Wallet
            {
                UserId = users[2].Id, WalletNumber = "1000000000000003",
                Balance = 7500000, TotalTopUp = 20000000, TotalSpending = 12500000,
                LoyaltyPoints = 3500, IsActive = true
            },
            new Wallet
            {
                UserId = users[3].Id, WalletNumber = "1000000000000004",
                Balance = 2500000, TotalTopUp = 5000000, TotalSpending = 2500000,
                LoyaltyPoints = 500, IsActive = true
            },
            new Wallet
            {
                UserId = users[4].Id, WalletNumber = "1000000000000005",
                Balance = 15000000, TotalTopUp = 40000000, TotalSpending = 25000000,
                LoyaltyPoints = 8000, IsActive = true
            },
        };

        context.Wallets.AddRange(wallets);
        await context.SaveChangesAsync();
    }

    private static async Task CreateSupportedBanksAsync(VibeWalletDbContext context)
    {
        var banks = new List<SupportedBank>
        {
            new() { BankName = "Bank BCA", BankCode = "014", TransferFee = 0, AdminFee = 2500, SortOrder = 1, IsActive = true },
            new() { BankName = "Bank Mandiri", BankCode = "008", TransferFee = 0, AdminFee = 2500, SortOrder = 2, IsActive = true },
            new() { BankName = "Bank BNI", BankCode = "009", TransferFee = 0, AdminFee = 2500, SortOrder = 3, IsActive = true },
            new() { BankName = "Bank BRI", BankCode = "002", TransferFee = 0, AdminFee = 2500, SortOrder = 4, IsActive = true },
            new() { BankName = "Bank CIMB Niaga", BankCode = "022", TransferFee = 0, AdminFee = 2500, SortOrder = 5, IsActive = true },
            new() { BankName = "Bank Permata", BankCode = "013", TransferFee = 0, AdminFee = 2500, SortOrder = 6, IsActive = true },
            new() { BankName = "Bank Danamon", BankCode = "011", TransferFee = 0, AdminFee = 2500, SortOrder = 7, IsActive = true },
            new() { BankName = "Bank Mega", BankCode = "426", TransferFee = 0, AdminFee = 2500, SortOrder = 8, IsActive = true },
        };

        context.SupportedBanks.AddRange(banks);
        await context.SaveChangesAsync();
    }

    private static async Task CreateBankAccountsAsync(VibeWalletDbContext context, List<VibeUser> users)
    {
        var accounts = new List<BankAccount>
        {
            new() { UserId = users[0].Id, BankName = "Bank BCA", BankCode = "014", AccountNumber = "1234567890", AccountHolderName = "Admin VibeWallet", IsVerified = true, IsPrimary = true },
            new() { UserId = users[1].Id, BankName = "Bank Mandiri", BankCode = "008", AccountNumber = "9876543210", AccountHolderName = "Budi Santoso", IsVerified = true, IsPrimary = true },
            new() { UserId = users[2].Id, BankName = "Bank BNI", BankCode = "009", AccountNumber = "5678901234", AccountHolderName = "Siti Nurhaliza", IsVerified = true, IsPrimary = true },
        };

        context.BankAccounts.AddRange(accounts);
        await context.SaveChangesAsync();
    }

    private static async Task CreateSampleTransactionsAsync(VibeWalletDbContext context, List<VibeUser> users)
    {
        var transactions = new List<WalletTransaction>();
        var now = DateTime.UtcNow;

        // Top-up transactions for user 1 (Budi)
        transactions.Add(new WalletTransaction
        {
            WalletId = context.Wallets.First(w => w.UserId == users[1].Id).Id,
            UserId = users[1].Id,
            TransactionRef = "TRX-TOPUP-001",
            Type = TransactionType.TopUp, Status = TransactionStatus.Completed,
            Method = PaymentMethod.BankTransfer,
            Amount = 5000000, Fee = 0,
            BalanceBefore = 0, BalanceAfter = 5000000,
            Description = "Top up via Bank Mandiri",
            CounterpartyName = "Bank Mandiri",
            CompletedAt = now.AddDays(-7), CreatedAt = now.AddDays(-7)
        });

        transactions.Add(new WalletTransaction
        {
            WalletId = context.Wallets.First(w => w.UserId == users[1].Id).Id,
            UserId = users[1].Id,
            TransactionRef = "TRX-PAY-001",
            Type = TransactionType.Payment, Status = TransactionStatus.Completed,
            Method = PaymentMethod.QRIS,
            Amount = 150000, Fee = 0,
            BalanceBefore = 5000000, BalanceAfter = 4850000,
            Description = "Pembayaran QRIS - Warung Makan Sederhana",
            CounterpartyName = "Warung Makan Sederhana",
            CompletedAt = now.AddDays(-6), CreatedAt = now.AddDays(-6)
        });

        transactions.Add(new WalletTransaction
        {
            WalletId = context.Wallets.First(w => w.UserId == users[1].Id).Id,
            UserId = users[1].Id,
            TransactionRef = "TRX-TRF-001",
            Type = TransactionType.Transfer, Status = TransactionStatus.Completed,
            Method = PaymentMethod.WalletBalance,
            Amount = 500000, Fee = 0,
            BalanceBefore = 4850000, BalanceAfter = 4350000,
            Description = "Transfer ke Siti Nurhaliza",
            CounterpartyName = "Siti Nurhaliza",
            CounterpartyWallet = "1000000000000003",
            CompletedAt = now.AddDays(-5), CreatedAt = now.AddDays(-5)
        });

        // Transactions for user 2 (Siti) - receiving transfer
        transactions.Add(new WalletTransaction
        {
            WalletId = context.Wallets.First(w => w.UserId == users[2].Id).Id,
            UserId = users[2].Id,
            TransactionRef = "TRX-RCV-001",
            Type = TransactionType.Transfer, Status = TransactionStatus.Completed,
            Method = PaymentMethod.WalletBalance,
            Amount = 500000, Fee = 0,
            BalanceBefore = 7000000, BalanceAfter = 7500000,
            Description = "Transfer dari Budi Santoso",
            CounterpartyName = "Budi Santoso",
            CounterpartyWallet = "1000000000000002",
            CompletedAt = now.AddDays(-5), CreatedAt = now.AddDays(-5)
        });

        // More sample transactions
        transactions.Add(new WalletTransaction
        {
            WalletId = context.Wallets.First(w => w.UserId == users[0].Id).Id,
            UserId = users[0].Id,
            TransactionRef = "TRX-BILL-001",
            Type = TransactionType.Payment, Status = TransactionStatus.Completed,
            Method = PaymentMethod.WalletBalance,
            Amount = 3500000, Fee = 2500,
            BalanceBefore = 13500000, BalanceAfter = 10000000,
            Description = "Pembayaran Listrik PLN - 12345678901",
            CounterpartyName = "PLN",
            CompletedAt = now.AddDays(-3), CreatedAt = now.AddDays(-3)
        });

        transactions.Add(new WalletTransaction
        {
            WalletId = context.Wallets.First(w => w.UserId == users[0].Id).Id,
            UserId = users[0].Id,
            TransactionRef = "TRX-TOPUP-002",
            Type = TransactionType.TopUp, Status = TransactionStatus.Completed,
            Method = PaymentMethod.BankTransfer,
            Amount = 10000000, Fee = 0,
            BalanceBefore = 0, BalanceAfter = 10000000,
            Description = "Top up via Bank BCA",
            CounterpartyName = "Bank BCA",
            CompletedAt = now.AddDays(-10), CreatedAt = now.AddDays(-10)
        });

        // Cashback transaction
        transactions.Add(new WalletTransaction
        {
            WalletId = context.Wallets.First(w => w.UserId == users[1].Id).Id,
            UserId = users[1].Id,
            TransactionRef = "TRX-CASH-001",
            Type = TransactionType.Cashback, Status = TransactionStatus.Completed,
            Method = PaymentMethod.WalletBalance,
            Amount = 750, Fee = 0,
            BalanceBefore = 4349250, BalanceAfter = 4350000,
            Description = "Cashback 0.5% - Pembayaran QRIS Warung Makan Sederhana",
            CompletedAt = now.AddDays(-6), CreatedAt = now.AddDays(-6)
        });

        context.WalletTransactions.AddRange(transactions);
        await context.SaveChangesAsync();
    }

    private static async Task CreateSampleVouchersAndPromosAsync(VibeWalletDbContext context)
    {
        var vouchers = new List<Voucher>
        {
            new()
            {
                VoucherCode = "WELCOME50", Title = "Welcome Bonus 50%",
                Description = "Diskon 50% untuk transaksi pertama (maks. Rp 25.000)",
                VoucherType = PromoType.Percentage, Value = 50,
                MinimumTransaction = 10000, MaximumDiscount = 25000,
                TotalQuota = 1000, UsedQuota = 150,
                ValidFrom = DateTime.UtcNow.AddDays(-30), ValidUntil = DateTime.UtcNow.AddDays(60),
                IsActive = true, PointsRequired = 0
            },
            new()
            {
                VoucherCode = "FOOD10K", Title = "Diskon Makanan Rp 10.000",
                Description = "Potongan Rp 10.000 untuk pembelian makanan & minuman",
                VoucherType = PromoType.FixedAmount, Value = 10000,
                MinimumTransaction = 50000, MaximumDiscount = 10000,
                TotalQuota = 500, UsedQuota = 45,
                ValidFrom = DateTime.UtcNow.AddDays(-15), ValidUntil = DateTime.UtcNow.AddDays(45),
                IsActive = true, PointsRequired = 500
            },
            new()
            {
                VoucherCode = "TRANS50", Title = "Gratis Ongkir s/d 50rb",
                Description = "Bebas ongkos kirim untuk pembelian di marketplace",
                VoucherType = PromoType.FreeShipping, Value = 50000,
                MinimumTransaction = 100000,
                TotalQuota = 300, UsedQuota = 80,
                ValidFrom = DateTime.UtcNow.AddDays(-7), ValidUntil = DateTime.UtcNow.AddDays(30),
                IsActive = true, PointsRequired = 1000
            },
            new()
            {
                VoucherCode = "PULSA5K", Title = "Cashback Pulsa Rp 5.000",
                Description = "Cashback pembelian pulsa minimal Rp 50.000",
                VoucherType = PromoType.FixedAmount, Value = 5000,
                MinimumTransaction = 50000,
                TotalQuota = 2000, UsedQuota = 300,
                ValidFrom = DateTime.UtcNow, ValidUntil = DateTime.UtcNow.AddDays(90),
                IsActive = true, PointsRequired = 200
            },
        };

        context.Vouchers.AddRange(vouchers);

        var promos = new List<Promo>
        {
            new()
            {
                Title = "Diskon 30% GoFood!", Description = "Nikmati diskon 30% untuk pesanan GoFood pertama kamu hari ini",
                Type = PromoType.Percentage, Value = 30,
                MerchantName = "GoFood", Category = "food",
                ValidFrom = DateTime.UtcNow, ValidUntil = DateTime.UtcNow.AddDays(30),
                IsActive = true, Priority = 1
            },
            new()
            {
                Title = "Cashback 20% Belanja di Shopee", Description = "Dapatkan cashback 20% maksimal Rp 50.000 dengan VibeWallet",
                Type = PromoType.Percentage, Value = 20,
                MerchantName = "Shopee", Category = "shopping",
                ValidFrom = DateTime.UtcNow, ValidUntil = DateTime.UtcNow.AddDays(14),
                IsActive = true, Priority = 2
            },
            new()
            {
                Title = "Beli 1 Gratis 1 Kopi Kenangan", Description = "Promo BOGO untuk menu Kopi Kenangan pilihan",
                Type = PromoType.BuyOneGetOne, Value = 0,
                MerchantName = "Kopi Kenangan", Category = "food",
                ValidFrom = DateTime.UtcNow, ValidUntil = DateTime.UtcNow.AddDays(7),
                IsActive = true, Priority = 3
            },
            new()
            {
                Title = "Diskon Rp 15.000 Transport", Description = "Potongan untuk pembayaran Gojek/Grab",
                Type = PromoType.FixedAmount, Value = 15000,
                MerchantName = "Gojek & Grab", Category = "transport",
                ValidFrom = DateTime.UtcNow, ValidUntil = DateTime.UtcNow.AddDays(21),
                IsActive = true, Priority = 4
            },
        };

        context.Promos.AddRange(promos);
        await context.SaveChangesAsync();
    }

    private static async Task CreateSampleChatSessionsAsync(VibeWalletDbContext context, List<VibeUser> users)
    {
        var session = new ChatSession
        {
            UserId = users[1].Id,
            Title = "Tanya tentang saldo",
            Provider = ChatProvider.OpenAI,
            ModelId = "gpt-4o",
            Temperature = 0.7m,
            SystemPrompt = "Kamu adalah Mbak Selvi...",
            IsActive = true,
            MessageCount = 3,
            LastMessageAt = DateTime.UtcNow,
            Messages = new List<ChatMessage>
            {
                new()
                {
                    Role = "user",
                    Content = "Halo Mbak Selvi, saya mau cek saldo",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5)
                },
                new()
                {
                    Role = "assistant",
                    Content = "Halo kak Budi! 👋 Senang bisa bantu. Saldo VibeWallet kakak saat ini **Rp 5.000.000**. Ada yang bisa Mbak Selvi bantu lagi? 😊",
                    RenderedContent = "<p>Halo kak Budi! 👋 Senang bisa bantu. Saldo VibeWallet kakak saat ini <strong>Rp 5.000.000</strong>. Ada yang bisa Mbak Selvi bantu lagi? 😊</p>",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-4)
                },
                new()
                {
                    Role = "user",
                    Content = "Ada promo apa aja nih yang lagi aktif?",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-2)
                },
            }
        };

        context.ChatSessions.Add(session);
        await context.SaveChangesAsync();
    }

    private static async Task CreateSampleInsuranceProductsAsync(VibeWalletDbContext context)
    {
        var products = new List<InsuranceProduct>
        {
            new()
            {
                ProductName = "Asuransi Kesehatan Premium", Type = InsuranceType.Health,
                ProviderName = "VibeInsurance", Description = "Perlindungan kesehatan menyeluruh dengan rawat inap hingga Rp 500 juta",
                PremiumAmount = 150000, PremiumPeriod = "monthly", CoverageAmount = 500_000_000,
                DurationMonths = 12, IsActive = true
            },
            new()
            {
                ProductName = "Asuransi Perjalanan Domestik", Type = InsuranceType.Travel,
                ProviderName = "VibeTravel", Description = "Perlindungan perjalanan dalam negeri, termasuk keterlambatan & bagasi",
                PremiumAmount = 25000, PremiumPeriod = "one-time", CoverageAmount = 50_000_000,
                DurationMonths = 1, IsActive = true
            },
            new()
            {
                ProductName = "Proteksi Gadget", Type = InsuranceType.Gadget,
                ProviderName = "VibeGadget", Description = "Lindungi smartphone & laptop dari kerusakan dan pencurian",
                PremiumAmount = 50000, PremiumPeriod = "monthly", CoverageAmount = 15_000_000,
                DurationMonths = 12, IsActive = true
            },
            new()
            {
                ProductName = "Asuransi Jiwa Keluarga", Type = InsuranceType.Life,
                ProviderName = "VibeLife", Description = "Perlindungan jiwa untuk keluarga tercinta",
                PremiumAmount = 200000, PremiumPeriod = "monthly", CoverageAmount = 1_000_000_000,
                DurationMonths = 24, IsActive = true
            },
        };

        context.InsuranceProducts.AddRange(products);
        await context.SaveChangesAsync();
    }
}
