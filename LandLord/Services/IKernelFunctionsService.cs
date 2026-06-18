using LandLord.Models;

namespace LandLord.Services;

/// <summary>
/// Interface untuk kernel functions yang bisa dipanggil chatbot
/// Functions: Tavily Search, Web Scraper, File Reader, Database Query
/// </summary>
public interface IKernelFunctionsService
{
    /// <summary>Dapatkan daftar fungsi yang tersedia</summary>
    List<KernelFunctionDefinition> GetAvailableFunctions();

    /// <summary>Eksekusi fungsi berdasarkan nama</summary>
    Task<FunctionResult> ExecuteAsync(string functionName, Dictionary<string, object?> parameters);

    /// <summary>Cek apakah user prompt memicu internet search</summary>
    bool ShouldSearchInternet(string userMessage);
}

/// <summary>
/// Definisi kernel function untuk registrasi
/// </summary>
public class KernelFunctionDefinition
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<KernelFunctionParameter> Parameters { get; set; } = new();
}

/// <summary>
/// Parameter kernel function
/// </summary>
public class KernelFunctionParameter
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public bool Required { get; set; } = true;
}
