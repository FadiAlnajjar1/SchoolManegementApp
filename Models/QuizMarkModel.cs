// Models/QuizMark.cs
namespace SchoolManagement.Api.Models;

public class QuizMark
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int SubjectId { get; set; }
    public int Semester { get; set; }
    public int QuizTypeId { get; set; }  // ✅ 1=Quiz1, 2=Quiz2, 3=Homework, 4=Oral, 5=FinalExam
    public int QuizNumber { get; set; }  // ✅ نفس QuizTypeId (للتوافق)
    public int Score { get; set; }       // ✅ int بدلاً من double
    public int MaxScore { get; set; }    // ✅ int بدلاً من double
    public DateOnly Date { get; set; }
    public string Notes { get; set; } = "";
    public int? EnteredById { get; set; }  // ✅ nullable
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }  // ✅ أضف هذا
    
    // Navigation properties
    public Student? Student { get; set; }
    public Subject? Subject { get; set; }
    public Employee? EnteredBy { get; set; }
}