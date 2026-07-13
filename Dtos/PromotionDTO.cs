// Dtos/PromotionRequest.cs
using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Api.Dtos;

public class PromoteRequest
{
    [Required]
    public int CurrentGradeNumber { get; set; }  // LocalGradeNumber (1-12)
    
    [Required]
    public int CurrentAcademicYear { get; set; }  // 2024
    
    [Required]
    public int NextAcademicYear { get; set; }  // 2025
    
    public int Semester { get; set; } = 2;
    public decimal PassPercent { get; set; } = 50;
}
// Dtos/PromotionResponse.cs

public class PromotionResponse
{
    public string Message { get; set; } = string.Empty;
    public PromotionStatistics Statistics { get; set; } = new();
    public PromotionDetails Details { get; set; } = new();
}
// Dtos/PromotionReportDto.cs


public class PromotionReportResponse
{
    public string GradeName { get; set; } = string.Empty;
    public int LocalGradeNumber { get; set; }
    public int TotalStudents { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public List<PromotionReportStudentDto> Students { get; set; } = new();
}

public class PromotionReportStudentDto
{
    public int Id { get; set; }
    public int LocalStudentNumber { get; set; }
    public int SectionLocalNumber { get; set; }
    public string GradeName { get; set; } = " ";
    public int GradeLocalNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Average { get; set; }
    public decimal Semester1Average { get; set; }
    public decimal Semester2Average { get; set; }
    public bool Passed { get; set; }
    public string? SectionName { get; set; }
}
public class PromotionStatistics
{
    public int Total { get; set; }
    public int Promoted { get; set; }
    public int Failed { get; set; }
    public int Graduated { get; set; }
}

public class PromotionDetails
{
    public string CurrentGrade { get; set; } = string.Empty;
    public string? NextGrade { get; set; }
    public List<StudentBasicInfo> PromotedStudents { get; set; } = new();
    public List<StudentFailInfo> FailedStudents { get; set; } = new();
    public List<StudentBasicInfo> GraduatedStudents { get; set; } = new();
}

public class StudentBasicInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class StudentFailInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SectionName { get; set; }
}