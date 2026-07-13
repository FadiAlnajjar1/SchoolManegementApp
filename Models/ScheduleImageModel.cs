// Models/ScheduleImage.cs
namespace SchoolManagement.Api.Models;

public class ScheduleImage
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int? GradeId { get; set; }        // اختياري: للصف
    public int? SectionId { get; set; }      // اختياري: للشعبة
    public int? TeacherId { get; set; }      // اختياري: للمعلم
    public string ImageUrl { get; set; } = string.Empty;  // مسار الصورة
    public string Description { get; set; } = string.Empty; // وصف (مثل: جدول الشعبة أ)
    public ScheduleImageType Type { get; set; }  // نوع الصورة
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // العلاقات
    public School? School { get; set; }
    public Grade? Grade { get; set; }
    public Section? Section { get; set; }
    public Employee? Teacher { get; set; }
}

// أنواع الصور
public enum ScheduleImageType
{
    Section,    // جدول شعبة
    Teacher,    // جدول معلم (حصصه فقط)
    Grade       // جدول صف
}