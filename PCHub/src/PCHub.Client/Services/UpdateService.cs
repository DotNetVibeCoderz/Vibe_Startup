using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;

namespace PCHub.Client.Services;

/// <summary>
/// Service untuk auto-update aplikasi client dari server PCHub.
/// </summary>
public class UpdateService
{
    private readonly string _baseUrl;
    private readonly string _currentVersion;
    private readonly HttpClient _http;

    public UpdateService(string baseUrl, string currentVersion = "1.0.0")
    {
        _baseUrl = baseUrl;
        _currentVersion = currentVersion;
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public event Action<string>? UpdateAvailable;
    public event Action<int>? DownloadProgress;
    public event Action<string>? UpdateError;

    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            var response = await _http.GetAsync("/api/client/update/check?version=" + _currentVersion);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<UpdateInfo>();
        }
        catch (Exception ex)
        {
            UpdateError?.Invoke($"Failed: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> DownloadAndInstallAsync(UpdateInfo info)
    {
        try
        {
            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"PCHub_Update_{info.Version}.zip");
            var extractPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"PCHub_Update_{info.Version}");

            using var response = await _http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = System.IO.File.Create(tempPath);
            var buffer = new byte[8192];
            int bytesRead; long totalRead = 0;
            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalRead += bytesRead;
                if (totalBytes > 0)
                    DownloadProgress?.Invoke((int)(totalRead * 100 / totalBytes));
            }
            fileStream.Close();

            if (System.IO.Directory.Exists(extractPath))
                System.IO.Directory.Delete(extractPath, true);
            ZipFile.ExtractToDirectory(tempPath, extractPath);
            System.IO.File.Delete(tempPath);

            var updaterPath = System.IO.Path.Combine(extractPath, "PCHub.Updater.exe");
            if (System.IO.File.Exists(updaterPath))
            {
                Process.Start(updaterPath, $"\"{AppDomain.CurrentDomain.BaseDirectory}\" \"{extractPath}\"");
                Environment.Exit(0);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            UpdateError?.Invoke($"Update failed: {ex.Message}");
            return false;
        }
    }
}

public class UpdateInfo
{
    public string Version { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public long SizeBytes { get; set; }
    public string ReleaseNotes { get; set; } = "";
    public bool IsMandatory { get; set; }
    public DateTime ReleaseDate { get; set; }
}
