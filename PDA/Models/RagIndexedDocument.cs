using System.ComponentModel.DataAnnotations;

namespace PDA.Models;

/// <summary>
/// RAG Knowledge Base indexed file record
/// </summary>
public class RagIndexedDocument
{
    public long Id { get; set; }

    [Required, MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(50)]
    public string FileType { get; set; } = string.Empty; // pdf, docx, xlsx, txt, csv, pptx

    public long FileSize { get; set; }

    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;

    public DateTime? FileModifiedAt { get; set; }

    /// <summary>
    /// Number of vector chunks created
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// Vector database provider used
    /// </summary>
    [MaxLength(50)]
    public string VectorProvider { get; set; } = "InMemory";

    /// <summary>
    /// Content hash for change detection
    /// </summary>
    [MaxLength(128)]
    public string? ContentHash { get; set; }

    /// <summary>
    /// Status: Indexed, Failed, Processing, Skipped
    /// </summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Indexed";

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Keywords extracted from document
    /// </summary>
    public string? Keywords { get; set; }
}
