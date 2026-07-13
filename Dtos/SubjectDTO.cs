// Dtos/SubjectDtos.cs (تعديل)
namespace SchoolManagement.Api.Dtos;

public class SubjectRequest
{
    public string Name { get; set; } = string.Empty;
    // public int GradeId { get; set; }
    // public int SchoolId { get; set; }
    
}

public class SubjectResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int GradeId { get; set; }
    public string? GradeName { get; set; }
    public int SchoolId { get; set; }
    public List<TeacherSubjectDto>? Teachers { get; set; }
}

public class TeacherSubjectDto
{
    public int TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public DateTime AssignedAt { get; set; }
}