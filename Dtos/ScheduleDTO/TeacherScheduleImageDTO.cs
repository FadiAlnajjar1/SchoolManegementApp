// Dtos/TeacherScheduleImageRequest.cs
using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Api.Dtos;

public class TeacherScheduleImageRequest
{
    [Required]
    public int LocalEmployeeNumber { get; set; }  // ← رقم المعلم المحلي داخل المدرسة
    
    public string? Description { get; set; }
    
    [Required]
    public IFormFile Image { get; set; } = null!;
}