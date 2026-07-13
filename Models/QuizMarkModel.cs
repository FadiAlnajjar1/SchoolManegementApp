// Models/QuizMark.cs
namespace SchoolManagement.Api.Models;

public class QuizMark
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int SubjectId { get; set; }
    public int Semester { get; set; }
    public int QuizNumber { get; set; }
    public double Score { get; set; }
    public double MaxScore { get; set; }
    public DateOnly Date { get; set; }
    public string Notes { get; set; } = string.Empty;
    public int EnteredById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public Student? Student { get; set; }
    public Subject? Subject { get; set; }
    public Employee? EnteredBy { get; set; }
}