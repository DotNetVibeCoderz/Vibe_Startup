using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;

namespace LandLord.Plugins;

/// <summary>
/// Semantic Kernel Plugin — Common utility functions
/// Date/Time, Math, Random, Text, Unit Conversion, Calculator
/// </summary>
public class CommonPlugin
{
    // ================================================================
    // DATE & TIME
    // ================================================================

    /// <summary>
    /// Dapatkan waktu saat ini dalam format yang bisa dibaca.
    /// </summary>
    [KernelFunction("get_current_time")]
    [Description("Get the current time. Use when user asks 'what time is it', 'jam berapa', or needs current time.")]
    [return: Description("Current time in readable format with timezone")]
    public string GetCurrentTime(
        [Description("Optional timezone offset like +7 (WIB), +8 (WITA), +9 (WIT), or leave empty for UTC")] string? timezone = null)
    {
        var now = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(timezone) && double.TryParse(timezone.Replace("+", ""), out var offset))
        {
            now = now.AddHours(offset);
            var tzName = offset switch
            {
                7 => "WIB",
                8 => "WITA",
                9 => "WIT",
                _ => $"UTC+{offset}"
            };
            return $"🕐 **{now:HH:mm:ss}** | 📅 {now:dddd, dd MMMM yyyy} | 🌍 {tzName}";
        }

        return $"🕐 **{now:HH:mm:ss} UTC** | 📅 {now:dddd, dd MMMM yyyy}";
    }

    /// <summary>
    /// Dapatkan tanggal hari ini.
    /// </summary>
    [KernelFunction("get_current_date")]
    [Description("Get the current date. Use when user asks 'tanggal berapa hari ini', 'what date is today'.")]
    [return: Description("Current date with day name")]
    public string GetCurrentDate()
    {
        var now = DateTime.UtcNow;
        var utc = now.ToString("dddd, dd MMMM yyyy", new CultureInfo("id-ID"));

        // Juga return WIB
        var wib = now.AddHours(7).ToString("dddd, dd MMMM yyyy", new CultureInfo("id-ID"));

        return $"📅 **Hari ini:**\n" +
               $"• UTC: {utc}\n" +
               $"• WIB: {wib}";
    }

    /// <summary>
    /// Dapatkan nama hari dari tanggal tertentu.
    /// </summary>
    [KernelFunction("get_day_of_week")]
    [Description("Get the day of week for a specific date or today.")]
    [return: Description("Day name and additional info")]
    public string GetDayOfWeek(
        [Description("Date in yyyy-MM-dd format, or leave empty for today")] string? date = null)
    {
        DateTime target;

        if (string.IsNullOrEmpty(date))
            target = DateTime.UtcNow;
        else if (!DateTime.TryParse(date, out target))
            return "❌ Format tanggal tidak valid. Gunakan yyyy-MM-dd (contoh: 2024-12-25).";

        var culture = new CultureInfo("id-ID");
        var dayName = target.ToString("dddd", culture);
        var fullDate = target.ToString("dd MMMM yyyy", culture);

        var isWeekend = target.DayOfWeek == DayOfWeek.Saturday || target.DayOfWeek == DayOfWeek.Sunday;

        return $"📅 **{fullDate}**\n" +
               $"• Hari: **{dayName}**\n" +
               $"• Weekend: {(isWeekend ? "✅ Ya" : "❌ Bukan (hari kerja)")}";
    }

    /// <summary>
    /// Hitung selisih antara dua tanggal.
    /// </summary>
    [KernelFunction("calculate_date_diff")]
    [Description("Calculate the difference between two dates in days, months, and years.")]
    [return: Description("Date difference breakdown")]
    public string CalculateDateDiff(
        [Description("Start date (yyyy-MM-dd)")] string startDate,
        [Description("End date (yyyy-MM-dd), or 'today'")] string? endDate = null)
    {
        if (!DateTime.TryParse(startDate, out var start))
            return "❌ Format tanggal mulai tidak valid (yyyy-MM-dd).";

        var end = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(endDate) && endDate != "today")
        {
            if (!DateTime.TryParse(endDate, out end))
                return "❌ Format tanggal akhir tidak valid (yyyy-MM-dd).";
        }

        if (start > end)
            (start, end) = (end, start); // swap

        var diff = end - start;
        var years = end.Year - start.Year;
        var months = end.Month - start.Month;
        if (months < 0) { years--; months += 12; }

        var sb = new StringBuilder();
        sb.AppendLine($"📊 **Selisih Tanggal**");
        sb.AppendLine($"• Dari: {start:dd MMM yyyy}");
        sb.AppendLine($"• Sampai: {end:dd MMM yyyy}");
        sb.AppendLine($"• **{diff.Days:N0} hari**");
        sb.AppendLine($"• **{Math.Round(diff.TotalDays / 7, 1)} minggu**");
        sb.AppendLine($"• **{years} tahun, {months} bulan**");
        sb.AppendLine($"• **{Math.Round(diff.TotalDays / 30.44, 1)} bulan** (rata-rata)");

        return sb.ToString();
    }

    /// <summary>
    /// Format tanggal ke berbagai format.
    /// </summary>
    [KernelFunction("format_date")]
    [Description("Format a date string into various readable formats.")]
    [return: Description("Formatted date in multiple styles")]
    public string FormatDate(
        [Description("Date string to format")] string date)
    {
        if (!DateTime.TryParse(date, out var dt))
            return "❌ Format tanggal tidak valid.";

        var id = new CultureInfo("id-ID");
        var en = new CultureInfo("en-US");

        return $"📅 **Format Tanggal: {dt:yyyy-MM-dd}**\n\n" +
               $"🇮🇩 **Indonesia:**\n" +
               $"• {dt.ToString("dddd, dd MMMM yyyy", id)}\n" +
               $"• {dt.ToString("dd/MM/yyyy", id)}\n" +
               $"• {dt.ToString("d MMMM yyyy", id)}\n\n" +
               $"🇬🇧 **English:**\n" +
               $"• {dt.ToString("dddd, dd MMMM yyyy", en)}\n" +
               $"• {dt.ToString("MM/dd/yyyy")}\n\n" +
               $"📐 **ISO 8601:** {dt:yyyy-MM-ddTHH:mm:ss.fffZ}";
    }

    // ================================================================
    // MATH & CALCULATOR
    // ================================================================

    /// <summary>
    /// Kalkulator ekspresi matematika dasar.
    /// </summary>
    [KernelFunction("calculate_math")]
    [Description("Evaluate a mathematical expression. Supports + - * / % ^ sqrt() abs() round() floor() ceil() sin() cos() tan() log() and parentheses.")]
    [return: Description("Calculation result with steps")]
    public string CalculateMath(
        [Description("Mathematical expression to evaluate, e.g. '2 + 3 * 4' or 'sqrt(144)'")] string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return "❌ Ekspresi kosong. Contoh: \"2 + 3 * 4\" atau \"sqrt(144)\"";

        try
        {
            // Pre-process: ganti koma desimal
            expression = expression.Replace(',', '.');

            // Handle function calls
            expression = Regex.Replace(expression, @"sqrt\s*\(\s*([^)]+)\s*\)", "Sqrt($1)", RegexOptions.IgnoreCase);
            expression = Regex.Replace(expression, @"abs\s*\(\s*([^)]+)\s*\)", "Abs($1)", RegexOptions.IgnoreCase);
            expression = Regex.Replace(expression, @"round\s*\(\s*([^)]+)\s*\)", "Round($1)", RegexOptions.IgnoreCase);
            expression = Regex.Replace(expression, @"floor\s*\(\s*([^)]+)\s*\)", "Floor($1)", RegexOptions.IgnoreCase);
            expression = Regex.Replace(expression, @"ceil\s*\(\s*([^)]+)\s*\)", "Ceiling($1)", RegexOptions.IgnoreCase);
            expression = Regex.Replace(expression, @"sin\s*\(\s*([^)]+)\s*\)", "Sin($1 * PI / 180)", RegexOptions.IgnoreCase);
            expression = Regex.Replace(expression, @"cos\s*\(\s*([^)]+)\s*\)", "Cos($1 * PI / 180)", RegexOptions.IgnoreCase);
            expression = Regex.Replace(expression, @"tan\s*\(\s*([^)]+)\s*\)", "Tan($1 * PI / 180)", RegexOptions.IgnoreCase);
            expression = Regex.Replace(expression, @"log\s*\(\s*([^)]+)\s*\)", "Log10($1)", RegexOptions.IgnoreCase);
            expression = Regex.Replace(expression, @"ln\s*\(\s*([^)]+)\s*\)", "Log($1)", RegexOptions.IgnoreCase);
            expression = Regex.Replace(expression, @"pi", "PI", RegexOptions.IgnoreCase);
            expression = Regex.Replace(expression, @"\^", "Pow", RegexOptions.IgnoreCase);

            // Ganti Pow(a,b) pattern
            expression = Regex.Replace(expression, @"Pow\s*\(\s*([^,]+)\s*,\s*([^)]+)\s*\)", "Pow($1,$2)");

            var result = EvaluateExpression(expression);

            return $"🧮 **Hasil Kalkulasi**\n\n" +
                   $"📐 Ekspresi: `{expression}`\n" +
                   $"✅ Hasil: **{result:N10}**".TrimEnd('0').TrimEnd('.');
        }
        catch (Exception ex)
        {
            return $"❌ Gagal menghitung: {ex.Message}\n\n" +
                   $"💡 Contoh yang didukung:\n" +
                   $"• `2 + 3 * 4`\n" +
                   $"• `sqrt(144)`\n" +
                   $"• `sin(30)`\n" +
                   $"• `round(3.7)`\n" +
                   $"• `(100 + 50) * 0.1`";
        }
    }

    /// <summary>
    /// Konversi satuan (panjang, luas, berat, suhu).
    /// </summary>
    [KernelFunction("convert_unit")]
    [Description("Convert between units of measurement. Supports: length (m, km, cm, mm, ft, inch, mile), area (m2, km2, ha, acre, ft2), weight (kg, g, ton, lb, oz), temperature (C, F, K).")]
    [return: Description("Conversion result")]
    public string ConvertUnit(
        [Description("Numeric value to convert")] double value,
        [Description("Source unit, e.g. 'km', 'm', 'kg', 'C', 'ha', 'm2'")] string fromUnit,
        [Description("Target unit, e.g. 'mile', 'ft', 'lb', 'F', 'acre', 'km2'")] string toUnit)
    {
        var fn = fromUnit.ToLowerInvariant().Trim();
        var tn = toUnit.ToLowerInvariant().Trim();

        if (fn == tn) return $"⚖️ {value} {fromUnit} = **{value} {toUnit}** (satuan sama)";

        // --- PANJANG (base: meter) ---
        var lengthToMeter = new Dictionary<string, double>
        {
            ["m"] = 1, ["meter"] = 1, ["km"] = 1000, ["cm"] = 0.01, ["mm"] = 0.001,
            ["ft"] = 0.3048, ["feet"] = 0.3048, ["foot"] = 0.3048,
            ["inch"] = 0.0254, ["in"] = 0.0254, ["mile"] = 1609.344, ["yard"] = 0.9144, ["yd"] = 0.9144
        };

        // --- LUAS (base: m²) ---
        var areaToM2 = new Dictionary<string, double>
        {
            ["m2"] = 1, ["km2"] = 1_000_000, ["ha"] = 10_000, ["hektar"] = 10_000,
            ["acre"] = 4046.86, ["ft2"] = 0.092903, ["are"] = 100
        };

        // --- BERAT (base: kg) ---
        var weightToKg = new Dictionary<string, double>
        {
            ["kg"] = 1, ["g"] = 0.001, ["gram"] = 0.001, ["ton"] = 1000, ["mg"] = 0.000001,
            ["lb"] = 0.453592, ["lbs"] = 0.453592, ["pound"] = 0.453592, ["oz"] = 0.0283495, ["ounce"] = 0.0283495
        };

        // Cek kategori
        if (lengthToMeter.ContainsKey(fn) && lengthToMeter.ContainsKey(tn))
        {
            var result = value * lengthToMeter[fn] / lengthToMeter[tn];
            return $"📏 **Konversi Panjang**\n{value:N4} {fromUnit} = **{result:N4} {toUnit}**";
        }

        if (areaToM2.ContainsKey(fn) && areaToM2.ContainsKey(tn))
        {
            var result = value * areaToM2[fn] / areaToM2[tn];
            return $"📐 **Konversi Luas**\n{value:N4} {fromUnit} = **{result:N4} {toUnit}**\n" +
                   $"(= {result / 10000:N4} hektar)";
        }

        if (weightToKg.ContainsKey(fn) && weightToKg.ContainsKey(tn))
        {
            var result = value * weightToKg[fn] / weightToKg[tn];
            return $"⚖️ **Konversi Berat**\n{value:N4} {fromUnit} = **{result:N4} {toUnit}**";
        }

        // --- SUHU ---
        if (fn is "c" or "celsius" && tn is "f" or "fahrenheit")
        {
            var f = value * 9 / 5 + 32;
            return $"🌡️ **Konversi Suhu**\n{value}°C = **{f:F2}°F**";
        }
        if (fn is "f" or "fahrenheit" && tn is "c" or "celsius")
        {
            var c = (value - 32) * 5 / 9;
            return $"🌡️ **Konversi Suhu**\n{value}°F = **{c:F2}°C**";
        }
        if (fn is "c" or "celsius" && tn is "k" or "kelvin")
        {
            var k = value + 273.15;
            return $"🌡️ **Konversi Suhu**\n{value}°C = **{k:F2} K**";
        }
        if (fn is "k" or "kelvin" && tn is "c" or "celsius")
        {
            var c = value - 273.15;
            return $"🌡️ **Konversi Suhu**\n{value} K = **{c:F2}°C**";
        }

        return $"❌ Konversi **{fromUnit} → {toUnit}** tidak didukung.\n\n" +
               $"💡 **Satuan yang didukung:**\n" +
               $"• Panjang: m, km, cm, mm, ft, inch, mile, yard\n" +
               $"• Luas: m2, km2, ha, acre, ft2, are\n" +
               $"• Berat: kg, g, ton, lb, oz, mg\n" +
               $"• Suhu: C, F, K";
    }

    // ================================================================
    // UTILITY
    // ================================================================

    /// <summary>
    /// Generate angka random dalam range.
    /// </summary>
    [KernelFunction("generate_random_number")]
    [Description("Generate a random number within a specified range.")]
    [return: Description("Random number generated")]
    public string GenerateRandomNumber(
        [Description("Minimum value (inclusive)")] double min = 1,
        [Description("Maximum value (inclusive)")] double max = 100,
        [Description("Type: 'int' for integer, 'double' for decimal")] string type = "int")
    {
        var rng = Random.Shared;

        if (type == "int")
        {
            var result = rng.Next((int)min, (int)max + 1);
            return $"🎲 **Random Integer:** {result} (range: {min:N0} - {max:N0})";
        }
        else
        {
            var result = min + rng.NextDouble() * (max - min);
            return $"🎲 **Random Decimal:** {result:F4} (range: {min} - {max})";
        }
    }

    /// <summary>
    /// Generate UUID/GUID.
    /// </summary>
    [KernelFunction("generate_uuid")]
    [Description("Generate a new UUID/GUID.")]
    [return: Description("Newly generated UUID")]
    public string GenerateUuid()
    {
        var guid = Guid.NewGuid();
        return $"🆔 **UUID:** `{guid}`\n• Format: `{guid:D}`\n• Short: `{guid:N}`";
    }

    /// <summary>
    /// Hitung persentase.
    /// </summary>
    [KernelFunction("calculate_percentage")]
    [Description("Calculate percentage: what is X% of Y, or what % is X of Y.")]
    [return: Description("Percentage calculation result")]
    public string CalculatePercentage(
        [Description("The value")] double value,
        [Description("The total/base value")] double total,
        [Description("Operation: 'percent_of' (X% of Y) or 'what_percent' (X is what % of Y)")] string operation = "what_percent")
    {
        if (operation == "percent_of")
        {
            // value% of total
            var result = (value / 100) * total;
            return $"📊 **{value}% dari {total:N2}** = **{result:N2}**\n" +
                   $"💡 Rumus: ({value}/100) × {total:N2} = {result:N2}";
        }
        else
        {
            // value is what % of total
            if (total == 0) return "❌ Total tidak boleh nol.";
            var result = (value / total) * 100;
            return $"📊 **{value:N2} adalah {result:F2}% dari {total:N2}**\n" +
                   $"💡 Rumus: ({value:N2}/{total:N2}) × 100 = {result:F2}%";
        }
    }

    /// <summary>
    /// Cek apakah tahun kabisat.
    /// </summary>
    [KernelFunction("is_leap_year")]
    [Description("Check if a year is a leap year (tahun kabisat).")]
    [return: Description("Leap year status")]
    public string IsLeapYear(
        [Description("Year to check, e.g. 2024")] int year)
    {
        var isLeap = DateTime.IsLeapYear(year);
        var nextLeap = year;
        while (!DateTime.IsLeapYear(nextLeap)) nextLeap++;
        var prevLeap = year;
        while (!DateTime.IsLeapYear(prevLeap)) prevLeap--;

        return isLeap
            ? $"✅ **{year} adalah tahun kabisat!** (366 hari, Februari 29 hari)\n📅 Kabisat sebelumnya: {prevLeap} | Berikutnya: {nextLeap}"
            : $"❌ **{year} bukan tahun kabisat** (365 hari)\n📅 Kabisat sebelumnya: {prevLeap} | Berikutnya: {nextLeap}";
    }

    /// <summary>
    /// Ringkasan teks sederhana.
    /// </summary>
    [KernelFunction("summarize_text")]
    [Description("Create a brief summary of a text by extracting key statistics.")]
    [return: Description("Text statistics summary")]
    public string SummarizeText(
        [Description("The text to analyze")] string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "❌ Teks kosong.";

        var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        var chars = text.Length;
        var charsNoSpace = text.Count(c => !char.IsWhiteSpace(c));

        var topWords = words
            .Select(w => w.ToLower().Trim(',', '.', '!', '?', ';', ':', '"', '\'', '(', ')'))
            .Where(w => w.Length > 3)
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("📄 **Statistik Teks**\n");
        sb.AppendLine($"• Total karakter: **{chars:N0}**");
        sb.AppendLine($"• Karakter (tanpa spasi): **{charsNoSpace:N0}**");
        sb.AppendLine($"• Jumlah kata: **{words.Length:N0}**");
        sb.AppendLine($"• Jumlah kalimat: **{sentences.Length:N0}**");
        sb.AppendLine($"• Rata-rata kata/kalimat: **{(sentences.Length > 0 ? (double)words.Length / sentences.Length : 0):F1}**");
        sb.AppendLine($"• Rata-rata karakter/kata: **{(words.Length > 0 ? (double)charsNoSpace / words.Length : 0):F1}**");

        if (topWords.Any())
        {
            sb.AppendLine($"\n🔤 **Top 5 kata:**");
            foreach (var tw in topWords)
                sb.AppendLine($"• \"{tw.Key}\" — {tw.Count()}×");
        }

        return sb.ToString();
    }

    // ================================================================
    // EXPRESSION EVALUATOR
    // ================================================================

    private static double EvaluateExpression(string expression)
    {
        // Recursive descent parser sederhana
        return ParseAddSub(expression, 0, out _);
    }

    private static double ParseAddSub(string expr, int pos, out int newPos)
    {
        var left = ParseMulDiv(expr, pos, out pos);
        while (pos < expr.Length)
        {
            if (expr[pos] == '+') { left += ParseMulDiv(expr, pos + 1, out pos); }
            else if (expr[pos] == '-') { left -= ParseMulDiv(expr, pos + 1, out pos); }
            else break;
        }
        newPos = pos;
        return left;
    }

    private static double ParseMulDiv(string expr, int pos, out int newPos)
    {
        var left = ParseUnary(expr, pos, out pos);
        while (pos < expr.Length)
        {
            if (expr[pos] == '*') { left *= ParseUnary(expr, pos + 1, out pos); }
            else if (expr[pos] == '/') { left /= ParseUnary(expr, pos + 1, out pos); }
            else if (expr[pos] == '%') { left %= ParseUnary(expr, pos + 1, out pos); }
            else break;
        }
        newPos = pos;
        return left;
    }

    private static double ParseUnary(string expr, int pos, out int newPos)
    {
        // Skip whitespace
        while (pos < expr.Length && char.IsWhiteSpace(expr[pos])) pos++;

        if (pos >= expr.Length) { newPos = pos; return 0; }

        // Unary minus
        if (expr[pos] == '-')
        {
            var val = ParseUnary(expr, pos + 1, out pos);
            newPos = pos;
            return -val;
        }

        if (expr[pos] == '+')
        {
            var val = ParseUnary(expr, pos + 1, out pos);
            newPos = pos;
            return val;
        }

        // Parentheses
        if (expr[pos] == '(')
        {
            var val = ParseAddSub(expr, pos + 1, out pos);
            if (pos < expr.Length && expr[pos] == ')') pos++;
            newPos = pos;
            return val;
        }

        // Functions
        if (pos + 3 < expr.Length)
        {
            var sub = expr[pos..];
            if (sub.StartsWith("Sqrt")) { var p = pos + 4; var arg = ParseAddSub(expr, p + 1, out var np); newPos = np + 1; return Math.Sqrt(arg); }
            if (sub.StartsWith("Abs")) { var p = pos + 3; var arg = ParseAddSub(expr, p + 1, out var np); newPos = np + 1; return Math.Abs(arg); }
            if (sub.StartsWith("Round")) { var p = pos + 5; var arg = ParseAddSub(expr, p + 1, out var np); newPos = np + 1; return Math.Round(arg); }
            if (sub.StartsWith("Floor")) { var p = pos + 5; var arg = ParseAddSub(expr, p + 1, out var np); newPos = np + 1; return Math.Floor(arg); }
            if (sub.StartsWith("Ceiling")) { var p = pos + 7; var arg = ParseAddSub(expr, p + 1, out var np); newPos = np + 1; return Math.Ceiling(arg); }
            if (sub.StartsWith("Sin")) { var p = pos + 3; var arg = ParseAddSub(expr, p + 1, out var np); newPos = np + 1; return Math.Sin(arg); }
            if (sub.StartsWith("Cos")) { var p = pos + 3; var arg = ParseAddSub(expr, p + 1, out var np); newPos = np + 1; return Math.Cos(arg); }
            if (sub.StartsWith("Tan")) { var p = pos + 3; var arg = ParseAddSub(expr, p + 1, out var np); newPos = np + 1; return Math.Tan(arg); }
            if (sub.StartsWith("Log10")) { var p = pos + 5; var arg = ParseAddSub(expr, p + 1, out var np); newPos = np + 1; return Math.Log10(arg); }
            if (sub.StartsWith("Log")) { var p = pos + 3; var arg = ParseAddSub(expr, p + 1, out var np); newPos = np + 1; return Math.Log(arg); }
            if (sub.StartsWith("Pow")) { var p = pos + 3; var a = ParseAddSub(expr, p + 1, out var np); var b = ParseAddSub(expr, np + 1, out var np2); newPos = np2 + 1; return Math.Pow(a, b); }
        }

        // Number or PI
        if (pos + 2 <= expr.Length && expr.Substring(pos, 2).Equals("PI", StringComparison.OrdinalIgnoreCase))
        {
            newPos = pos + 2;
            return Math.PI;
        }

        // Parse number
        var start = pos;
        while (pos < expr.Length && (char.IsDigit(expr[pos]) || expr[pos] == '.')) pos++;

        if (pos > start)
        {
            newPos = pos;
            return double.Parse(expr[start..pos], CultureInfo.InvariantCulture);
        }

        newPos = pos + 1;
        return 0;
    }
}
