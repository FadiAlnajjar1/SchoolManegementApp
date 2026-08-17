// Models/TeacherGrade.cs
namespace SchoolManagement.Api.Models;

public class TeacherGrade
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public int SubjectId { get; set; }
    public int SectionId { get; set; }  
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public Employee? Teacher { get; set; }
    public Subject? Subject { get; set; }
    public Section? Section { get; set; }
}