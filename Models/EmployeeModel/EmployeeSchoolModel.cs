// Models/EmployeeSchool.cs
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolManagement.Api.Models;

[PrimaryKey(nameof(SchoolId), nameof(LocalEmployeeNumber))]  // ← مفتاح مركب
public class EmployeeSchool
{
    public int Id { get; set; }
    // المفتاح المركب (SchoolId + LocalEmployeeNumber)
    public int SchoolId { get; set; }
    public int LocalEmployeeNumber { get; set; }  // ← يبدأ من 1 في كل مدرسة
    
    // بيانات إضافية
    public int EmployeeId { get; set; }
    public EmployeeRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // العلاقات
    [ForeignKey(nameof(SchoolId))]
    public School? School { get; set; }
    
    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }
}