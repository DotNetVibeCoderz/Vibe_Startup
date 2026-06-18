namespace LandLord.Services;

/// <summary>
/// Interface untuk konfigurasi aplikasi
/// </summary>
public interface ISettingsService
{
    Task<Dictionary<string, object>> GetAllSettingsAsync();
    Task<T?> GetSettingAsync<T>(string key);
    Task<bool> UpdateSettingAsync(string key, object value);
    Task<bool> SaveSettingsToFileAsync();
}
