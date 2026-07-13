// Dtos/RepeatGradeResponse.cs
namespace SchoolManagement.Api.Dtos;

public class RepeatGradeResponse
{
    public string Message { get; set; } = string.Empty;
    public RepeatGradeStudentDto Student { get; set; } = new();
}

public class RepeatGradeStudentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SectionName { get; set; }
    public string? GradeName { get; set; }
}