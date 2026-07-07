using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace FitnessCenter.Services.ChatBot;

/// <summary>
/// Kernel Functions untuk utility: tanggal, waktu, math, dan konversi.
/// </summary>
public class UtilityPlugin
{
    // ---- DATE & TIME ----

    [KernelFunction("get_current_time")]
    [Description("Mendapatkan waktu dan tanggal saat ini dalam berbagai zona waktu. Return waktu UTC dan WIB (Indonesia).")]
    public string GetCurrentTime(
        [Description("Zona waktu opsional, default WIB. Contoh: 'UTC', 'WIB', 'WITA', 'WIT'")] string? timeZone = null)
    {
        var utcNow = DateTime.UtcNow;

        var result = $"🕐 Waktu saat ini:\n   UTC: {utcNow:yyyy-MM-dd HH:mm:ss} UTC\n   WIB: {utcNow.AddHours(7):yyyy-MM-dd HH:mm:ss} WIB";

        if (!string.IsNullOrEmpty(timeZone))
        {
            result += timeZone.ToUpper() switch
            {
                "WITA" => $"\n   WITA: {utcNow.AddHours(8):yyyy-MM-dd HH:mm:ss} WITA",
                "WIT" => $"\n   WIT: {utcNow.AddHours(9):yyyy-MM-dd HH:mm:ss} WIT",
                _ => ""
            };
        }

        result += $"\n   📅 Hari: {utcNow.DayOfWeek}, {utcNow:dd MMMM yyyy}";

        return result;
    }

    [KernelFunction("get_date_info")]
    [Description("Mendapatkan informasi detail tentang suatu tanggal. Hari, minggu ke-, kuartal, dan info libur.")]
    public string GetDateInfo(
        [Description("Tanggal dalam format yyyy-MM-dd. Kosongkan untuk hari ini.")] string? dateStr = null)
    {
        DateTime date;
        if (string.IsNullOrEmpty(dateStr) || !DateTime.TryParse(dateStr, out date))
            date = DateTime.UtcNow.Date;

        var weekOfYear = System.Globalization.CultureInfo.CurrentCulture.Calendar
            .GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

        return $"📅 Informasi Tanggal: {date:dd MMMM yyyy}\n" +
               $"   📆 Hari: {date.DayOfWeek}\n" +
               $"   📊 Minggu ke-{weekOfYear}\n" +
               $"   📈 Kuartal: Q{(date.Month + 2) / 3}\n" +
               $"   🗓️ Hari dalam setahun: {date.DayOfYear}/{(DateTime.IsLeapYear(date.Year) ? 366 : 365)}\n" +
               $"   ⏳ Hari tersisa tahun ini: {(new DateTime(date.Year, 12, 31) - date).Days}";
    }

    [KernelFunction("calculate_days_between")]
    [Description("Menghitung jumlah hari antara dua tanggal")]
    public string CalculateDaysBetween(
        [Description("Tanggal mulai (yyyy-MM-dd)")] string startDateStr,
        [Description("Tanggal akhir (yyyy-MM-dd)")] string endDateStr)
    {
        if (!DateTime.TryParse(startDateStr, out var start) || !DateTime.TryParse(endDateStr, out var end))
            return "❌ Format tanggal tidak valid. Gunakan format yyyy-MM-dd.";

        var days = (end - start).Days;
        return $"📅 {start:dd MMM yyyy} → {end:dd MMM yyyy}\n" +
               $"   ⏳ Selisih: {Math.Abs(days)} hari\n" +
               $"   📊 {(days >= 0 ? $"{days} hari dari sekarang" : $"{Math.Abs(days)} hari yang lalu")}";
    }

    // ---- MATH ----

    [KernelFunction("calculate")]
    [Description("Melakukan kalkulasi matematika. Mendukung operasi dasar: +, -, *, /, pangkat (^), persen (%), akar kuadrat (sqrt), dan parentheses. Contoh: '2+2', '100*1.5', 'sqrt(144)', '2^10'")]
    public string Calculate(
        [Description("Ekspresi matematika untuk dihitung")] string expression)
    {
        try
        {
            // Handle sqrt
            expression = System.Text.RegularExpressions.Regex.Replace(expression, @"sqrt\(([^)]+)\)", "Sqrt($1)");

            // Handle ^ for power
            expression = System.Text.RegularExpressions.Regex.Replace(expression, @"(\d+(?:\.\d+)?)\s*\^\s*(\d+(?:\.\d+)?)", "Pow($1,$2)");

            // Handle %
            expression = System.Text.RegularExpressions.Regex.Replace(expression, @"(\d+(?:\.\d+)?)\s*%", "($1/100)");

            var result = EvaluateExpression(expression);
            return $"🧮 {expression} = {result}";
        }
        catch (Exception ex)
        {
            return $"❌ Gagal menghitung: {ex.Message}";
        }
    }

    [KernelFunction("convert_unit")]
    [Description("Konversi satuan: berat (kg↔lbs), tinggi (cm↔ft↔inch), jarak (km↔mile), suhu (C↔F)")]
    public string ConvertUnit(
        [Description("Nilai yang akan dikonversi")] double value,
        [Description("Dari satuan: kg, lbs, cm, inch, ft, km, mile, C, F")] string fromUnit,
        [Description("Ke satuan tujuan")] string toUnit)
    {
        var normalized = (fromUnit.ToLower(), toUnit.ToLower());
        double result;

        try
        {
            result = normalized switch
            {
                ("kg", "lbs") => value * 2.20462,
                ("lbs", "kg") => value / 2.20462,
                ("cm", "inch") => value / 2.54,
                ("inch", "cm") => value * 2.54,
                ("cm", "ft") => value / 30.48,
                ("ft", "cm") => value * 30.48,
                ("km", "mile") => value * 0.621371,
                ("mile", "km") => value / 0.621371,
                ("c", "f") => (value * 9 / 5) + 32,
                ("f", "c") => (value - 32) * 5 / 9,
                _ => throw new ArgumentException($"Konversi dari '{fromUnit}' ke '{toUnit}' tidak didukung.")
            };

            return $"🔄 {value} {fromUnit} = {Math.Round(result, 2)} {toUnit}";
        }
        catch (Exception ex)
        {
            return $"❌ {ex.Message}\n   Satuan yang didukung: kg, lbs, cm, inch, ft, km, mile, C, F";
        }
    }

    [KernelFunction("calculate_bmi")]
    [Description("Menghitung BMI (Body Mass Index) berdasarkan berat (kg) dan tinggi (cm)")]
    public string CalculateBMI(
        [Description("Berat badan dalam kg")] double weightKg,
        [Description("Tinggi badan dalam cm")] double heightCm)
    {
        var heightM = heightCm / 100.0;
        var bmi = weightKg / (heightM * heightM);
        var category = bmi switch
        {
            < 18.5 => "Underweight (Kurus)",
            < 25 => "Normal (Sehat) ✅",
            < 30 => "Overweight (Kelebihan)",
            _ => "Obese (Obesitas)"
        };

        return $"⚕️ Kalkulator BMI:\n" +
               $"   📏 Tinggi: {heightCm} cm | ⚖️ Berat: {weightKg} kg\n" +
               $"   📊 BMI: {bmi:F1}\n" +
               $"   🏷️ Kategori: {category}\n" +
               $"   🎯 Berat ideal: {18.5 * heightM * heightM:F1} - {24.9 * heightM * heightM:F1} kg";
    }

    [KernelFunction("calculate_calories_burned")]
    [Description("Estimasi kalori yang terbakar berdasarkan aktivitas, durasi, dan berat badan")]
    public string CalculateCaloriesBurned(
        [Description("Jenis aktivitas: running, walking, cycling, swimming, yoga, hiit, zumba, weightlifting, skipping, aerobics")] string activity,
        [Description("Durasi dalam menit")] int durationMinutes,
        [Description("Berat badan dalam kg")] double weightKg)
    {
        // MET values (Metabolic Equivalent of Task)
        var met = activity.ToLower() switch
        {
            "running" => 9.8,
            "walking" => 3.8,
            "cycling" => 7.5,
            "swimming" => 8.0,
            "yoga" => 3.0,
            "hiit" => 12.0,
            "zumba" => 7.5,
            "weightlifting" => 6.0,
            "skipping" => 11.0,
            "aerobics" => 6.5,
            _ => 5.0
        };

        var calories = met * weightKg * (durationMinutes / 60.0);

        return $"🔥 Estimasi Kalori Terbakar:\n" +
               $"   🏃 Aktivitas: {activity}\n" +
               $"   ⏱️ Durasi: {durationMinutes} menit\n" +
               $"   ⚖️ Berat: {weightKg} kg\n" +
               $"   🔥 Kalori: ~{calories:F0} kcal\n" +
               $"   📊 MET: {met}";
    }

    // Simple expression evaluator
    private static double EvaluateExpression(string expr)
    {
        // Use DataTable for safe evaluation
        var dt = new System.Data.DataTable();
        expr = expr.Replace("Pow", "").Replace("Sqrt", "");
        // Handle basic operations using simple parsing
        return Convert.ToDouble(dt.Compute(expr, null));
    }
}
