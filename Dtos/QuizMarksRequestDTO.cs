// Dtos/QuizMarkRequest.cs
namespace SchoolManagement.Api.Dtos;

// Dtos/QuizMarkRequest.cs
public class QuizMarkRequest
{
    public int LocalStudentNumber { get; set; }
    public int LocalSubjectId { get; set; }
    public int Semester { get; set; }
    public int QuizTypeId { get; set; }  // ✅ 1=Quiz1, 2=Quiz2, 3=Homework, 4=Oral, 5=FinalExam
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public string? Notes { get; set; }
}