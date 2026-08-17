// Models/School.cs
using SchoolManagement.Api.Models;

public class School
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public SchoolType Type { get; set; }
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public int AdminId { get; set; }
    public Admin? Admin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<Grade>? Grades { get; set; }
    public ICollection<Section>? Sections { get; set; }
    public ICollection<Subject>? Subjects { get; set; }
    public ICollection<EmployeeSchool>? EmployeeSchools { get; set; }  // ← جديد
}