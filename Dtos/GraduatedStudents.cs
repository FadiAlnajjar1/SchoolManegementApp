// Dtos/GraduatedStudentsResponse.cs
namespace SchoolManagement.Api.Dtos;

public class GraduatedStudentsResponse
{
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<GraduatedStudentDto> Students { get; set; } = new();
}

public class GraduatedStudentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int? GraduationYear { get; set; }
    public DateTime CreatedAt { get; set; }
}