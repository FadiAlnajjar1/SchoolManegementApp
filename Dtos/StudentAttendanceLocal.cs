// Dtos/StudentAttendanceLocalRequest.cs
using SchoolManagement.Api.Models;

public class StudentAttendanceLocalRequest
{
    public int LocalGradeNumber { get; set; }
    public int LocalSectionNumber { get; set; }
    public List<StudentAttendanceEntryLocal> Entries { get; set; } = new();
}

public class StudentAttendanceEntryLocal
{
    public int LocalStudentNumber { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Justification { get; set; }
}

// Dtos/StudentAttendanceRequest.cs
public class StudentAttendanceRequest
{
    public int SectionId { get; set; }
    public DateOnly Date { get; set; }
    public List<StudentAttendanceEntry> Entries { get; set; } = new();
}

public class StudentAttendanceEntry
{
    public int StudentId { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Justification { get; set; }
}