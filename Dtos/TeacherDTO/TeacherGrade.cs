// Dtos/TeacherGradeRequest.cs
namespace SchoolManagement.Api.Dtos;

public class TeacherGradeRequest
{
    public int TeacherId { get; set; }
    public int SubjectId { get; set; }
    public int LocalGradeNumber { get; set; }
    public int LocalSectionNumber { get; set; }  
    public int TeacherLocalNumber { get; set; }
    public int LocalSubjectId { get; set; }
}

// Dtos/TeacherGradeResponse.cs

public class TeacherGradeResponse
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public int SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public int GradeId { get; set; }
    public string? GradeName { get; set; }
    public int? SectionId { get; set; }
    public string? SectionName { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Dtos/TeacherGradeDetailResponse.cs


public class TeacherGradeDetailResponse
{
    public int TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public List<TeacherGradeSubjectDto>? Subjects { get; set; }
}

public class TeacherGradeSubjectDto
{
    public int SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public List<TeacherGradeGradeDto>? Grades { get; set; }
}

public class TeacherGradeGradeDto
{
    public int GradeId { get; set; }
    public string? GradeName { get; set; }
    public int? SectionId { get; set; }
    public string? SectionName { get; set; }
    public DateTime CreatedAt { get; set; }
}
// Dtos/TeacherGradeSummaryResponse.cs

public class TeacherGradeSummaryResponse
{
    public int TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public List<GradeSummaryDto>? Grades { get; set; }
}

public class GradeSummaryDto
{
    public int GradeId { get; set; }
    public string? GradeName { get; set; }
    public int? SectionId { get; set; }
    public string? SectionName { get; set; }
    public List<SubjectSummaryDto>? Subjects { get; set; }
}

public class SubjectSummaryDto
{
    public int SubjectId { get; set; }
    public string? SubjectName { get; set; }
}
// Dtos/UnassignTeacherGradeRequest.cs

public class UnassignTeacherGradeRequest
{
    public int TeacherId { get; set; }
    public int GradeId { get; set; }
    public int? SectionId { get; set; }
}