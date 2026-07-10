using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using EstateHub.Data;
using EstateHub.Models;

namespace EstateHub.Services;

public class KprSimulatorService
{
    public KprResult Calculate(decimal propertyPrice, decimal downPayment, double annualInterestRate, int tenorMonths)
    {
        var loanAmount = propertyPrice - downPayment;
        var monthlyRate = annualInterestRate / 12.0 / 100.0;
        var totalMonths = tenorMonths;

        decimal monthlyPayment;
        if (monthlyRate > 0)
        {
            var factor = Math.Pow(1 + monthlyRate, totalMonths);
            monthlyPayment = loanAmount * (decimal)(monthlyRate * factor / (factor - 1));
        }
        else
        {
            monthlyPayment = loanAmount / totalMonths;
        }

        var totalPayment = monthlyPayment * totalMonths;
        var totalInterest = totalPayment - loanAmount;

        var schedule = new List<AmortizationRow>();
        var remainingBalance = loanAmount;

        for (int month = 1; month <= Math.Min(totalMonths, 12); month++)
        {
            var interestPayment = remainingBalance * (decimal)monthlyRate;
            var principalPayment = monthlyPayment - interestPayment;
            remainingBalance -= principalPayment;
            if (remainingBalance < 0) remainingBalance = 0;

            schedule.Add(new AmortizationRow
            {
                Month = month,
                Principal = Math.Round(principalPayment),
                Interest = Math.Round(interestPayment),
                Total = Math.Round(monthlyPayment),
                RemainingBalance = Math.Round(remainingBalance)
            });
        }

        return new KprResult
        {
            PropertyPrice = propertyPrice,
            DownPayment = downPayment,
            DownPaymentPercentage = (double)Math.Round(downPayment / propertyPrice * 100, 1),
            LoanAmount = loanAmount,
            AnnualInterestRate = annualInterestRate,
            TenorYears = totalMonths / 12,
            TenorMonths = totalMonths,
            MonthlyPayment = Math.Round(monthlyPayment),
            TotalPayment = Math.Round(totalPayment),
            TotalInterest = Math.Round(totalInterest),
            AmortizationSchedule = schedule
        };
    }

    public LoanEligibility CheckEligibility(decimal monthlyIncome, decimal monthlyPayment)
    {
        var ratio = monthlyPayment / monthlyIncome * 100;
        return new LoanEligibility
        {
            MonthlyIncome = monthlyIncome,
            MonthlyPayment = monthlyPayment,
            DebtServiceRatio = (double)Math.Round(ratio, 1),
            IsEligible = ratio <= 30,
            Recommendation = ratio <= 30
                ? "Selamat! Anda memenuhi syarat untuk pengajuan KPR."
                : $"Cicilan {ratio:0.0}% dari penghasilan, melebihi batas 30%."
        };
    }
}

public class KprResult
{
    public decimal PropertyPrice { get; set; }
    public decimal DownPayment { get; set; }
    public double DownPaymentPercentage { get; set; }
    public decimal LoanAmount { get; set; }
    public double AnnualInterestRate { get; set; }
    public int TenorYears { get; set; }
    public int TenorMonths { get; set; }
    public decimal MonthlyPayment { get; set; }
    public decimal TotalPayment { get; set; }
    public decimal TotalInterest { get; set; }
    public List<AmortizationRow> AmortizationSchedule { get; set; } = new();
}

public class AmortizationRow
{
    public int Month { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public decimal Total { get; set; }
    public decimal RemainingBalance { get; set; }
}

public class LoanEligibility
{
    public decimal MonthlyIncome { get; set; }
    public decimal MonthlyPayment { get; set; }
    public double DebtServiceRatio { get; set; }
    public bool IsEligible { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}

public class StorageService
{
    private readonly IConfiguration _config;
    private readonly string _basePath;

    public StorageService(IConfiguration config)
    {
        _config = config;
        _basePath = config.GetValue<string>("Storage:FileSystem:BasePath") ?? "wwwroot/uploads";
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveFileAsync(IFormFile file, string subfolder = "general")
    {
        var folder = Path.Combine(_basePath, subfolder);
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(folder, fileName);
        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);
        return $"/uploads/{subfolder}/{fileName}";
    }

    public Task DeleteFileAsync(string relativePath)
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.TrimStart('/'));
        if (File.Exists(filePath)) File.Delete(filePath);
        return Task.CompletedTask;
    }
}

public class ExportService
{
    public string ExportToCsv<T>(IEnumerable<T> records)
    {
        using var writer = new StringWriter();
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(records);
        return writer.ToString();
    }

    public byte[] ExportToExcel<T>(IEnumerable<T> records) where T : class
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Data");
        var properties = typeof(T).GetProperties();
        for (int i = 0; i < properties.Length; i++)
            worksheet.Cell(1, i + 1).Value = properties[i].Name;
        int row = 2;
        foreach (var record in records)
        {
            for (int col = 0; col < properties.Length; col++)
            {
                var value = properties[col].GetValue(record);
                worksheet.Cell(row, col + 1).Value = value?.ToString() ?? "";
            }
            row++;
        }
        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

public class MLRecommendationService
{
    private readonly AppDbContext _db;
    public MLRecommendationService(AppDbContext db) => _db = db;

    public async Task<List<Property>> GetRecommendationsAsync(string userId, int count = 5)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return await _db.Properties.Where(p => p.IsVerified).Take(count).ToListAsync();

        IQueryable<Property> query = _db.Properties.Where(p => p.IsVerified && p.Status == "Available");
        if (!string.IsNullOrWhiteSpace(user.PreferredType))
            query = query.Where(p => p.PropertyType == user.PreferredType);
        if (!string.IsNullOrWhiteSpace(user.PreferredLocation))
            query = query.Where(p => p.City != null && p.City.Contains(user.PreferredLocation));
        if (user.MinBudget.HasValue)
            query = query.Where(p => p.Price >= user.MinBudget.Value);
        if (user.MaxBudget.HasValue)
            query = query.Where(p => p.Price <= user.MaxBudget.Value);

        var results = await query.OrderByDescending(p => p.ViewCount).Take(count * 2).ToListAsync();
        if (results.Count < count)
        {
            var existingIds = results.Select(r => r.Id).ToList();
            var popular = await _db.Properties
                .Where(p => p.IsVerified && p.Status == "Available" && !existingIds.Contains(p.Id))
                .OrderByDescending(p => p.ViewCount).Take(count - results.Count).ToListAsync();
            results.AddRange(popular);
        }
        return results.Take(count).ToList();
    }

    public async Task<List<Property>> GetSimilarPropertiesAsync(int propertyId, int count = 5)
    {
        var property = await _db.Properties.FindAsync(propertyId);
        if (property == null) return new List<Property>();
        return await _db.Properties
            .Where(p => p.Id != propertyId && p.IsVerified && p.PropertyType == property.PropertyType
                        && p.Price >= property.Price * 0.7m && p.Price <= property.Price * 1.3m)
            .OrderByDescending(p => p.ViewCount).Take(count).ToListAsync();
    }
}

public class PricePredictionService
{
    private readonly AppDbContext _db;
    public PricePredictionService(AppDbContext db) => _db = db;

    public async Task<PricePrediction> PredictPriceAsync(string propertyType, string city, double buildingArea)
    {
        var similar = await _db.Properties
            .Where(p => p.PropertyType == propertyType && p.City == city && p.IsVerified).ToListAsync();

        if (similar.Count < 3)
            similar = await _db.Properties.Where(p => p.PropertyType == propertyType && p.IsVerified).ToListAsync();

        if (similar.Count == 0)
            return new PricePrediction { EstimatedPrice = 0, Confidence = 0, Message = "Data tidak cukup." };

        var avgPricePerSqm = similar.Where(p => p.BuildingArea > 0)
            .Average(p => (double)(p.Price / (decimal)p.BuildingArea));
        var estimatedPrice = (decimal)(avgPricePerSqm * buildingArea);
        var confidence = Math.Min(similar.Count / 20.0 * 100, 90);

        return new PricePrediction
        {
            EstimatedPrice = estimatedPrice,
            Confidence = Math.Round(confidence, 1),
            SampleSize = similar.Count,
            Message = $"Estimasi berdasarkan {similar.Count} properti sejenis di {city}."
        };
    }
}

public class PricePrediction
{
    public decimal EstimatedPrice { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public double Confidence { get; set; }
    public int SampleSize { get; set; }
    public string Message { get; set; } = string.Empty;
}
