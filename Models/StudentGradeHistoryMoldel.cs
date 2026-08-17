// Models/StudentGradeHistory.cs
namespace SchoolManagement.Api.Models;

public class StudentGradeHistory
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int GradeId { get; set; }
    public int SectionId { get; set; }
    public int AcademicYear { get; set; }
    public int Semester { get; set; }
    public bool IsPassed { get; set; }
    public decimal Average { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public Student? Student { get; set; }
    public Grade? Grade { get; set; }
    public Section? Section { get; set; }
}