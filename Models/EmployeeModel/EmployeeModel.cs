// Models/Employee.cs
using System.Text.Json.Serialization;
using SchoolManagement.Api.Models;

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    [JsonIgnore]
    public string PasswordHash { get; set; } = "";
    public string? NationalId { get; set; }
    public string? Phone { get; set; }
    public string Address { get; set; } = "";
    public DateTime? BirthDate { get; set; }
    public string Qualification { get; set; } = "";
    public string Photo { get; set; } = "";
    public string? FcmToken { get; set; }
    public int UnexcusedAbsenceDays { get; set; }
    public bool DismissalWarning { get; set; } 
    public bool IsDismissed { get; set; }      
    public bool IsPhoneVerified { get; set; } 
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // العلاقات
    public ICollection<EmployeeSchool>? EmployeeSchools { get; set; }
    public ICollection<TeacherSubject>? TeacherSubjects { get; set; }
    public ICollection<TeacherGrade>? TeacherGrades { get; set; }
}