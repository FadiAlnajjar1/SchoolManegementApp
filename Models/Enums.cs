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
    Active,
    Returned,
    Overdue,
}

public enum ReservationStatus
{
    Pending,
    Fulfilled,
    Cancelled,
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
