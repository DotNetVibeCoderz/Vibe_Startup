using System.ComponentModel.DataAnnotations;

namespace JuraganKost.Data.Models;

/// <summary>
/// Multiple images for a Kost
/// </summary>
public class GambarKost
{
    public int Id { get; set; }
    public int KostId { get; set; }
    public Kost? Kost { get; set; }
    [MaxLength(2000)]
    public string Url { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? Caption { get; set; }
    public int Urutan { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Multiple images for a Kamar
/// </summary>
public class GambarKamar
{
    public int Id { get; set; }
    public int KamarId { get; set; }
    public Kamar? Kamar { get; set; }
    [MaxLength(2000)]
    public string Url { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? Caption { get; set; }
    public int Urutan { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
