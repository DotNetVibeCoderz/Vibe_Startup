using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Comblang.Models;

public class Profile
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public Guid UserId { get; set; }

    [MaxLength(500)] public string? Bio { get; set; }
    [MaxLength(50)]  public string? Gender { get; set; }

    /// <summary>Gender preference for matching: "Male", "Female", or "Both".</summary>
    [MaxLength(20)]  public string? LookingForGender { get; set; } = "Both";

    public DateTime? DateOfBirth { get; set; }
    [MaxLength(100)] public string? Occupation { get; set; }
    [MaxLength(100)] public string? Education { get; set; }
    [MaxLength(50)]  public string? RelationshipGoal { get; set; }
    public int HeightCm { get; set; }
    [MaxLength(1000)] public string? ProfilePictureUrl { get; set; }
    public bool IsVerifiedPhoto { get; set; }
    public bool IsVerifiedVideo { get; set; }
    public string? PhotosJson { get; set; }
    [MaxLength(50)]  public string? Religion { get; set; }
    public bool IsPhotoBlurred { get; set; }
    [MaxLength(50)]  public string? ZodiacSign { get; set; }
    [MaxLength(20)]  public string? BloodType { get; set; }
    public bool IsSmoker { get; set; }
    public bool IsDrinker { get; set; }
    public bool HasPet { get; set; }

    [ForeignKey(nameof(UserId))] public User? User { get; set; }
}
