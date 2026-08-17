// Dtos/WarningLocalRequest.cs
using SchoolManagement.Api.Models;

public class WarningLocalRequest
{
    public int LocalStudentNumber { get; set; }
    public WarningType Type { get; set; }
    public string Reason { get; set; } = "";
}

// Dtos/WarningFilterRequest.cs (اختياري)
public class WarningFilterRequest
{
    public int? LocalGradeNumber { get; set; }
    public int? LocalSectionNumber { get; set; }
    public int? LocalStudentNumber { get; set; }
}