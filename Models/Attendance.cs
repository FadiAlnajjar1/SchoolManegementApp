namespace SchoolManagement.Api.Models;


// Models/Attendance.cs
public class StudentAttendance
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int? SectionId { get; set; }
    public DateOnly Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Justification { get; set; }
    public int? TakenById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }  // ✅ إضافة هذا
    public bool IsDeleted { get; set; } = false;  // ✅ إضافة هذا لـ Soft Delete

    public Student? Student { get; set; }
    public Section? Section { get; set; }
    public Employee? TakenBy { get; set; }
}

public class EmployeeAttendance
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateOnly Date { get; set; }
    public AttendanceStatus Status { get; set; }

    public bool OnLeave { get; set; }
}


public class Leave
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = "";
    public int GrantedByAdminId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
