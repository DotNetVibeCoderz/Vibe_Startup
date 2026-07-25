using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ngibrid.Models;

/// <summary>
/// Master data kota/kabupaten — the reference table every distance, tariff, and route calculation
/// resolves against.
///
/// One row per second-level administrative area (kabupaten/kota), which is the granularity
/// Indonesian couriers actually quote on. <see cref="Latitude"/>/<see cref="Longitude"/> are the
/// coordinates of the administrative seat (ibu kota kabupaten), not the geometric centroid of the
/// area — a parcel is delivered to the town, not to the middle of a mountain range, so the seat is
/// the point that makes a great-circle distance meaningful.
///
/// <see cref="Name"/> holds the bare name ("Bandung") and <see cref="Type"/> distinguishes
/// KOTA from KABUPATEN, because plenty of pairs share a name (Kota Bandung vs Kabupaten Bandung,
/// Kota Tasikmalaya vs Kabupaten Tasikmalaya, …) and sit tens of kilometres apart.
/// <see cref="FullName"/> is what gets stored on an order and shown in the UI.
/// </summary>
public class City : BaseEntity
{
    [Required, MaxLength(100)]
    public string Country { get; set; } = "Indonesia";

    [Required, MaxLength(100)]
    public string Province { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>KOTA or KABUPATEN.</summary>
    [Required, MaxLength(20)]
    public string Type { get; set; } = "KOTA";

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>Optional: the seat's town name when it differs from the area name (Kab. Bogor → Cibinong).</summary>
    [MaxLength(100)]
    public string? SeatName { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Display and storage form, e.g. "Kota Bandung" / "Kabupaten Bandung".</summary>
    [NotMapped]
    public string FullName => Type == "KOTA" ? $"Kota {Name}" : $"Kabupaten {Name}";
}
