using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolManagement.Api.Models;


// Models/SchoolType.cs (إذا لم يكن موجوداً)

public enum SchoolType
{
    Primary,      // ابتدائي
    Preparatory,  // إعدادي
    Secondary,    // ثانوي
    PrimaryPreparatory, // ابتدائي وإعدادي (مختلط)
    PreparatorySecondary, // إعدادي وثانوي (مختلط)
    AllStages     // جميع المراحل
}
public enum QuizType
{
    Quiz1 = 1,
    Quiz2 = 2,
    Homework = 3,
    Oral = 4,
    FinalExam = 5
}
public enum LoanRequestStatus
{
    Pending = 1,      // قيد الانتظار
    Approved = 2,     // تمت الموافقة
    Rejected = 3,     // مرفوض
    Cancelled = 4     // ملغي
}
public class BookLoanRequest
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int BookId { get; set; }
    
    [Required]
    public int StudentId { get; set; }
    
    [Required]
    public int LocalRequestNumber { get; set; }
    
    [Required]
    public DateTime RequestDate { get; set; }
    
    [Required]
    public LoanRequestStatus Status { get; set; }
    
    public string? RejectionReason { get; set; }
    
    public DateTime? ProcessedAt { get; set; }
    
    [Required]
    public DateTime CreatedAt { get; set; }
    
    // Navigation Properties
    [ForeignKey(nameof(BookId))]
    public virtual Book? Book { get; set; }
    
    [ForeignKey(nameof(StudentId))]
    public virtual Student? Student { get; set; }
}
public enum EmployeeRole
{
    Principal,          
    Secretary,        
    Counselor,        
    Librarian,         
    ActivitySupervisor, 
    Teacher,       
}

public enum UserType
{
    Admin,
    Employee,
    Student,
}

public enum AttendanceStatus
{
    Present,
    Absent,
    Justified, 
}

public enum ComplaintStatus
{
    Open,
    Resolved,
    Rejected,
}

public enum WarningType
{
    Absence,        
    Behavior,       
    DismissalWarning,
}

public enum AnnouncementType
{
    General,
    Activity,
}

public enum AnnouncementAudience
{
    All,           // الكل
    Students,      // الطلاب فقط
    Employees,     // الموظفين فقط
    Teachers,      // المعلمين فقط
    Parents,       // أولياء الأمور فقط
    Section,       // شعبة معينة
    Grade,         // صف معين
    Administrators
}

public enum ActivityType
{
    Trip, 
    Camp,
    Club, 
    Other,
}

public enum LoanStatus
{
    Active=1,
    Returned=2,
    Overdue=3,
}

public enum ReservationStatus
{
    Pending = 1,
    Fulfilled=2,
    Cancelled=3,
}

public enum RegistrationStatus
{
    Pending,
    Approved,
    Rejected,
}

public enum MemberStatus
{
    Active,
    Suspended,
}
