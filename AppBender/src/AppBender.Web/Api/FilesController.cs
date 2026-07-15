using AppBender.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppBender.Web.Api;

/// <summary>Upload endpoint for chat attachments, form file fields, and knowledge documents.</summary>
[ApiController]
[Route("api/files")]
[Authorize]
public class FilesController(IStorageService storage) : ControllerBase
{
    private static readonly string[] BlockedExtensions = [".exe", ".dll", ".bat", ".cmd", ".ps1", ".sh", ".msi"];
    private const long MaxSize = 50 * 1024 * 1024;

    /// <summary>Uploads a file and returns its public URL.</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(MaxSize)]
    public async Task<ActionResult<object>> Upload(IFormFile file)
    {
        if (file.Length == 0) return BadRequest(new { error = "Empty file." });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (BlockedExtensions.Contains(ext)) return BadRequest(new { error = "File type not allowed." });

        var safeName = Path.GetFileName(file.FileName);
        var path = $"uploads/{DateTime.UtcNow:yyyyMM}/{Guid.NewGuid():N}/{safeName}";
        await using var stream = file.OpenReadStream();
        await storage.SaveAsync(path, stream, file.ContentType);

        var isImage = file.ContentType.StartsWith("image/");
        return Ok(new
        {
            fileName = safeName,
            url = storage.GetPublicUrl(path),
            contentType = file.ContentType,
            size = file.Length,
            isImage
        });
    }
}

/// <summary>Export/import of forms, workflows, schemas and datasets as JSON.</summary>
[ApiController]
[Route("api/package")]
[Authorize(Roles = "Admin,Developer")]
public class PackageController(IImportExportService importExport) : ControllerBase
{
    /// <summary>Exports the whole workspace (schema, forms, workflows, apps; ?includeRecords=true adds data).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] bool includeRecords = false)
    {
        var json = await importExport.ExportAsync(includeRecords);
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json",
            $"appbender-export-{DateTime.UtcNow:yyyyMMdd-HHmm}.json");
    }

    /// <summary>Imports a previously exported package.</summary>
    [HttpPost("import")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<ActionResult<object>> Import(IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        var json = await reader.ReadToEndAsync();
        try
        {
            var result = await importExport.ImportAsync(json);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
