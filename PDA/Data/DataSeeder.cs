using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PDA.Models;

namespace PDA.Data;

/// <summary>
/// Seeds the database with sample data for development
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        // Create roles
        string[] roles = { "Admin", "User", "Analyst" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Create sample users
        var adminUser = await CreateUserIfNotExists(userManager, "admin@pda.com", "Admin@123", "Admin PDA", "Admin");
        var analystUser = await CreateUserIfNotExists(userManager, "analyst@pda.com", "Analyst@123", "Data Analyst", "Analyst");
        var normalUser = await CreateUserIfNotExists(userManager, "user@pda.com", "User@1234", "Regular User", "User");

        // Create sample database connections (only if none exist)
        if (!await db.DatabaseConnections.AnyAsync())
        {
            // 1. Default SQLite connection (sample data)
            db.DatabaseConnections.Add(new DatabaseConnection
            {
                Name = "Sample SQLite Database",
                Description = "Default sample database with sales, customers, and products data for testing PDA features.",
                DatabaseType = "SQLite",
                ConnectionString = "Data Source=SampleData.db",
                FilePath = "SampleData.db",
                UserId = adminUser!.Id,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

            // 2. Sample CSV connection
            db.DatabaseConnections.Add(new DatabaseConnection
            {
                Name = "Sample CSV Data",
                Description = "Sample CSV file connection for import testing",
                DatabaseType = "CSV",
                ConnectionString = "",
                FilePath = "SampleData.csv",
                UserId = adminUser!.Id,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

            await db.SaveChangesAsync();
        }

        // Create sample chat session
        if (!await db.ChatSessions.AnyAsync())
        {
            var dbConn = await db.DatabaseConnections.FirstOrDefaultAsync(d => d.DatabaseType == "SQLite");
            if (dbConn != null)
            {
                var session = new ChatSession
                {
                    Title = "Getting Started - Chat with Your Data",
                    UserId = adminUser!.Id,
                    DatabaseConnectionId = dbConn.Id,
                    ModelProvider = "OpenAI",
                    ModelName = "gpt-4o",
                    Temperature = 0.3,
                    SystemPrompt = "You are a helpful data analyst assistant. You have access to a database with the following schema:\n" +
                        "- Customers (Id, Name, Email, Phone, City, Country, RegistrationDate)\n" +
                        "- Products (Id, Name, Category, Price, Stock, Supplier)\n" +
                        "- Orders (Id, CustomerId, OrderDate, TotalAmount, Status)\n" +
                        "- OrderItems (Id, OrderId, ProductId, Quantity, UnitPrice)\n" +
                        "Always provide accurate, data-driven answers with SQL queries when needed.",
                    CreatedAt = DateTime.UtcNow
                };
                db.ChatSessions.Add(session);
                await db.SaveChangesAsync();

                // Add a welcome message
                db.ChatMessages.Add(new ChatMessage
                {
                    ChatSessionId = session.Id,
                    Role = "assistant",
                    Content = "👋 **Selamat datang di PDA - Personal Data Analyst!**\n\n" +
                        "Saya adalah asisten AI yang dapat membantu Anda menganalisis data dari database yang terkoneksi.\n\n" +
                        "**Yang bisa saya bantu:**\n" +
                        "- 📊 Menjawab pertanyaan tentang data Anda dengan SQL\n" +
                        "- 📈 Membuat dashboard dan report\n" +
                        "- 🔍 Mencari insight dari data\n" +
                        "- 📝 Membuat visualisasi chart\n\n" +
                        "Silakan tanyakan apa saja tentang data Anda! Coba:\n" +
                        "- \"Tampilkan 10 pelanggan teratas berdasarkan total pembelian\"\n" +
                        "- \"Berapa total penjualan bulan ini?\"\n" +
                        "- \"Buatkan dashboard penjualan per kategori\"",
                    Timestamp = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
        }

        // Create sample RAG index entries
        if (!await db.RagIndexedDocuments.AnyAsync())
        {
            db.RagIndexedDocuments.Add(new RagIndexedDocument
            {
                FileName = "Sample_Report_Q4.pdf",
                FilePath = "KnowledgeBase/Sample_Report_Q4.pdf",
                FileType = "pdf",
                FileSize = 1024 * 1024, // 1 MB
                IndexedAt = DateTime.UtcNow,
                ChunkCount = 24,
                VectorProvider = "InMemory",
                Status = "Indexed",
                Keywords = "quarterly report, Q4, sales, financial"
            });
            await db.SaveChangesAsync();
        }

        // Create sample audit logs
        if (!await db.AuditLogs.AnyAsync())
        {
            var logs = new[]
            {
                new AuditLog { Category = "Auth", Action = "Login", Description = "User logged in", UserId = adminUser!.Id, IpAddress = "127.0.0.1", IsSuccess = true, Timestamp = DateTime.UtcNow.AddHours(-2) },
                new AuditLog { Category = "Chat", Action = "QueryExecuted", Description = "SQL query executed", UserId = adminUser!.Id, DurationMs = 150, IsSuccess = true, Timestamp = DateTime.UtcNow.AddHours(-1) },
                new AuditLog { Category = "Database", Action = "ConnectionTested", Description = "Database connection tested", UserId = adminUser!.Id, IsSuccess = true, Timestamp = DateTime.UtcNow.AddMinutes(-30) },
                new AuditLog { Category = "RAG", Action = "DocumentIndexed", Description = "Document indexed to vector store", UserId = adminUser!.Id, DurationMs = 3500, IsSuccess = true, Timestamp = DateTime.UtcNow.AddMinutes(-15) },
            };
            db.AuditLogs.AddRange(logs);
            await db.SaveChangesAsync();
        }
    }

    private static async Task<ApplicationUser?> CreateUserIfNotExists(
        UserManager<ApplicationUser> userManager,
        string email, string password, string fullName, string role)
    {
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser == null)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                ThemePreference = "dark"
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
                return user;
            }
        }
        return existingUser;
    }
}
