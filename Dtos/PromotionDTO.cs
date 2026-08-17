// Dtos/PromotionRequest.cs
using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Api.Dtos;
// ============================================
// DTOs للترقية
// ============================================

// Dtos/PromoteRequest.cs
public class PromoteRequest
{
    public int LocalGradeNumber { get; set; }  // رقم الصف المحلي
    public decimal PassPercent { get; set; }   // نسبة النجاح
}

// Dtos/PromoteAllRequest.cs
public class PromoteAllRequest
{
    public decimal PassPercent { get; set; }  // نسبة النجاح لجميع الصفوف
}

// Dtos/GradePromotionResult.cs
public class GradePromotionResult
{
    public string GradeName { get; set; } = string.Empty;
    public int Level { get; set; }
    public int LocalGradeNumber { get; set; }
    public int TotalStudents { get; set; }
    public int PromotedCount { get; set; }
    public int FailedCount { get; set; }
    public int GraduatedCount { get; set; }
    public List<StudentBasicInfo> PromotedStudents { get; set; } = new();
    public List<StudentBasicInfo> FailedStudents { get; set; } = new();
    public List<StudentBasicInfo> GraduatedStudents { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

// Dtos/StudentBasicInfo.cs
public class StudentBasicInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int LocalStudentNumber { get; set; }
    public string? Email { get; set; }
}

// Dtos/StudentFailInfo.cs
public class StudentFailInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SectionName { get; set; }
}

// Dtos/PromotionResponse.cs
public class PromotionResponse
{
    public string Message { get; set; } = string.Empty;
    public PromotionStatistics Statistics { get; set; } = new();
    public PromotionDetails Details { get; set; } = new();
}

// Dtos/PromotionStatistics.cs
public class PromotionStatistics
{
    public int Total { get; set; }
    public int Promoted { get; set; }
    public int Failed { get; set; }
    public int Graduated { get; set; }
}

// Dtos/PromotionDetails.cs
public class PromotionDetails
{
    public string CurrentGrade { get; set; } = string.Empty;
    public int CurrentLevel { get; set; }
    public int CurrentLocalGradeNumber { get; set; }
    public string NextGrade { get; set; } = string.Empty;
    public int NextLevel { get; set; }
    public int? NextLocalGradeNumber { get; set; }
    public List<StudentBasicInfo> PromotedStudents { get; set; } = new();
    public List<StudentFailInfo> FailedStudents { get; set; } = new();
    public List<StudentBasicInfo> GraduatedStudents { get; set; } = new();
}

// Dtos/PromotionReportStudentDto.cs
public class PromotionReportStudentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int LocalStudentNumber { get; set; }
    public decimal Average { get; set; }
    public decimal Semester1Average { get; set; }
    public decimal Semester2Average { get; set; }
    public bool Passed { get; set; }
    public string? SectionName { get; set; }
    public int SectionLocalNumber { get; set; }
    public string? GradeName { get; set; }
    public int GradeLocalNumber { get; set; }
    public int GradeLevel { get; set; }
}

// Dtos/PromotionReportResponse.cs
public class PromotionReportResponse
{
    public string GradeName { get; set; } = string.Empty;
    public int Level { get; set; }
    public int LocalGradeNumber { get; set; }
    public int? NextLevel { get; set; }
    public string? NextGradeName { get; set; }
    public int? NextLocalGradeNumber { get; set; }
    public int TotalStudents { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public decimal PassPercent { get; set; }
    public List<PromotionReportStudentDto> Students { get; set; } = new();
}
// Dtos/PromoteRequest.cs
// public class PromoteRequest
// {   
//     public int LocalGradeNumber { get; set; }
//     public decimal? PassPercent { get; set; }  // اختياري
// }

// // Dtos/PromotionReportStudentDto.cs
// public class PromotionReportStudentDto
// {
//     public int Id { get; set; }
//     public string Name { get; set; } = string.Empty;
//     public int LocalStudentNumber { get; set; }
//     public decimal Average { get; set; }
//     public decimal Semester1Average { get; set; }
//     public decimal Semester2Average { get; set; }
//     public bool Passed { get; set; }
//     public string? SectionName { get; set; }
//     public int SectionLocalNumber { get; set; }
//     public string? GradeName { get; set; }
//     public int GradeLocalNumber { get; set; }
//     public int GradeLevel { get; set; }  // ✅ إضافة المستوى
// }

// // Dtos/PromotionReportResponse.cs
// public class PromotionReportResponse
// {
//     public string GradeName { get; set; } = string.Empty;
//     public int Level { get; set; }  // ✅ المستوى الحالي
//     public int LocalGradeNumber { get; set; }
//     public int? NextLevel { get; set; }  // ✅ المستوى التالي
//     public int? NextLocalGradeNumber { get; set; } 
//     public string? NextGradeName { get; set; }
//     public int TotalStudents { get; set; }
//     public int PassedCount { get; set; }
//     public int FailedCount { get; set; }
//     public decimal PassPercent { get; set; }
//     public List<PromotionReportStudentDto> Students { get; set; } = new();
// }
// // Dtos/PromotionResponse.cs

// public class PromotionResponse
// {
//     public string Message { get; set; } = string.Empty;
//     public PromotionStatistics Statistics { get; set; } = new();
//     public PromotionDetails Details { get; set; } = new();
// }
// // Dtos/PromotionReportDto.cs



// public class PromotionStatistics
// {
//     public int Total { get; set; }
//     public int Promoted { get; set; }
//     public int Failed { get; set; }
//     public int Graduated { get; set; }
// }

// public class PromotionDetails
// {
//     public string CurrentGrade { get; set; } = string.Empty;
//     public string? NextGrade { get; set; }
//     public List<StudentBasicInfo> PromotedStudents { get; set; } = new();
//     public List<StudentFailInfo> FailedStudents { get; set; } = new();
//     public List<StudentBasicInfo> GraduatedStudents { get; set; } = new();
// }

// public class StudentBasicInfo
// {
//     public int Id { get; set; }
//     public string Name { get; set; } = string.Empty;
// }

// public class StudentFailInfo
// {
//     public int Id { get; set; }
//     public string Name { get; set; } = string.Empty;
//     public string? SectionName { get; set; }
// }