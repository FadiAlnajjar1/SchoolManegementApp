// Models/Section.cs
using SchoolManagement.Api.Models;

public class Section
{
    public int Id { get; set; }
    public int GradeId { get; set; }
    public int SchoolId { get; set; }
    public string Name { get; set; } = "";
    public int? CounselorId { get; set; }
    public int LocalSectionNumber { get; set; }  // ← الرقم المحلي للشعبة داخل الصف
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public Grade? Grade { get; set; }
    public School? School { get; set; }
    public Employee? Counselor { get; set; }
    public ICollection<Student>? Students { get; set; }
    public ICollection<TeacherGrade>? TeacherGrades { get; set; }
}