// Dtos/ScheduleImageRequest.cs
using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Api.Dtos;

public class ScheduleImageRequest
{
    [Required]
    public int LocalGradeNumber { get; set; }
    
    [Required]
    public int LocalSectionNumber { get; set; }  // ← رقم الشعبة المحلي داخل الصف
    
    public string? Description { get; set; }
    
    [Required]
    public IFormFile Image { get; set; } = null!;
}