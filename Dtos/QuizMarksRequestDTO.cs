// Dtos/QuizMarkRequest.cs
namespace SchoolManagement.Api.Dtos;

public class QuizMarkRequest
{
    public int StudentId { get; set; }
    public int SubjectId { get; set; }
    public int Semester { get; set; }
    public int QuizNumber { get; set; }
    public double Score { get; set; }
    public double MaxScore { get; set; }
    public string? Notes { get; set; }

public double Percentage => MaxScore > 0 ? (double)(Score * 100 / MaxScore) : 0;
}