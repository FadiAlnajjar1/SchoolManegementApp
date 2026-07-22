using System.ComponentModel.DataAnnotations;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Dtos;


// Dtos/TransferStudentLocalRequest.cs
public class TransferStudentLocalRequest
{
    public int CurrentSchoolId { get; set; }          // ✅ المدرسة الحالية
    public int LocalStudentNumber { get; set; }       // ✅ Local ID للطالب
    public int NewSchoolId { get; set; }              // ✅ المدرسة الجديدة
    public int? LocalGradeNumber { get; set; }        // ✅ Local ID للصف الجديد
    public int? LocalSectionNumber { get; set; }      // ✅ Local ID للشعبة الجديدة
}

// Dtos/TransferEmployeeLocalRequest.cs
public class TransferEmployeeLocalRequest
{
    public int CurrentSchoolId { get; set; }          // ✅ المدرسة الحالية
    public int LocalEmployeeNumber { get; set; }      // ✅ Local ID للموظف
    public int NewSchoolId { get; set; }              // ✅ المدرسة الجديدة
    public EmployeeRole NewRole { get; set; }         // ✅ الدور الجديد
}
// Dtos/LoanLocalRequest.cs
public class LoanLocalRequest
{
    public int LocalBookNumber { get; set; }      // ✅ Local ID للكتاب
    public int LocalMemberNumber { get; set; }    // ✅ Local ID للعضو
    public DateOnly DueDate { get; set; }
}
// Dtos/ReservationLocalRequest.cs
public class ReservationLocalRequest
{
    public int LocalBookNumber { get; set; }      // ✅ Local ID للكتاب
    public int LocalMemberNumber { get; set; }    // ✅ Local ID للعضو
}

// Dtos/ReservationDecisionRequest.cs
public class ReservationDecisionRequest
{
    public ReservationStatus Status { get; set; }
}
// Dtos/BookLoanDto.cs (اختياري)
public class BookLoanDto
{
    public int Id { get; set; }
    public int LocalLoanNumber { get; set; }
    public int LocalBookNumber { get; set; }
    public string BookTitle { get; set; } = "";
    public int LocalMemberNumber { get; set; }
    public string? StudentName { get; set; }
    public DateOnly LoanDate { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly? ReturnDate { get; set; }
    public string Status { get; set; } = "";
    public bool IsOverdue { get; set; }
    public DateTime CreatedAt { get; set; }
}
public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    UserType? UserType,
    string? FcmToken
    );

public class LoginResponse
{
    public string Token { get; set; }
    public UserType UserType { get; set; }
    public string Role { get; set; }
    public int Id { get; set; }
    public int? LocalId { get; set; }  // ✅ LocalId (LocalEmployeeNumber أو LocalStudentNumber)
    public string Name { get; set; }
    public int? SchoolId { get; set; }

    public LoginResponse(string token, UserType userType, string role, int id, string name, int? schoolId, int? localId = null)
    {
        Token = token;
        UserType = userType;
        Role = role;
        Id = id;
        LocalId = localId;
        Name = name;
        SchoolId = schoolId;
    }
}

public record FcmTokenRequest([Required] string Token);
// Dtos/StudentSubjectDto.cs
public class StudentSubjectDto
{
    public int LocalSubjectId { get; set; }        // ✅ فقط Local ID
    public string Name { get; set; } = "";
    public string? TeacherName { get; set; }
    public int? LocalTeacherNumber { get; set; }   // ✅ فقط Local ID
}

// Dtos/StudentSubjectsResponse.cs
public class StudentSubjectsResponse
{
    public string? Message { get; set; }
    public int LocalSectionNumber { get; set; }    // ✅ فقط Local ID
    public string? SectionName { get; set; }
    public int LocalGradeNumber { get; set; }      // ✅ فقط Local ID
    public string? GradeName { get; set; }
    public int? AcademicYear { get; set; }
    public List<StudentSubjectDto> Subjects { get; set; } = new();
    public int TotalSubjects { get; set; }
}

// 

// public record EmployeeCreateRequest(
//     [Required] string Name,
//     [Required, EmailAddress] string Email,
//     [Required, MinLength(6)] string Password,
//     [Required] EmployeeRole Role,
//     string? Phone,
//     string? Address,
//     DateTime? BirthDate,
//     string? Qualification);

// public record EmployeeUpdateRequest(
//     string? Name,
//     string? Phone,
//     string? Address,
//     DateTime? BirthDate,
//     string? Qualification,
//     [MinLength(6)] string? Password);

    // public record TransferRequest(
    //     [Required] int SchoolId,
    //     [Required] int GradeId
    //     );

//     public class TransferRequest1
// {
//     public int StudentId { get; set; }        // ID الطالب
//     public int CurrentSchoolId { get; set; }  // المدرسة الحالية (للتأكيد)
//     public int NewSchoolId { get; set; }      // المدرسة الجديدة
//     public int? GradeId { get; set; }         // الصف الجديد (اختياري)
//     
// }
//  int? SectionId
 

public record LeaveRequest(
    [Required] int EmployeeId,
    [Required] DateOnly StartDate,
    [Required] DateOnly EndDate,
    string? Reason);


// public record StudentCreateRequest(
//     [Required] string Name,
//     [Required, EmailAddress] string Email,
//     [Required, MinLength(6)] string Password,
//     // int? SectionId,
//     // string? GuardianName,
//     // string? GuardianPhone,
//     // string? BloodType,
//     // string? ChronicDiseases,
//     // string? Allergies,
//     // string? HealthNotes,
//     DateTime? BirthDate,
//     string? Address);

// public record StudentUpdateRequest(
//     string? Name,
//     int? SectionId,
//     string? GuardianName,
//     string? GuardianPhone,
//     string? BloodType,
//     string? ChronicDiseases,
//     string? Allergies,
//     string? HealthNotes,
//     DateTime? BirthDate,
//     string? Address,
//     [MinLength(6)] string? Password);


// public record GradeRequest([Required] string Name, [Required] int Level);
// public record SectionRequest([Required] int GradeId, [Required] string Name, int? CounselorId);
// public record SubjectRequest([Required] string Name,  int? TeacherId);

public record SchedulePeriodRequest([Required] int Order, [Required] int SubjectId, [Required] int TeacherId);
public record ScheduleRequest(
    [Required] int SectionId,
    [Required] DayOfWeek Day,
    [Required] List<SchedulePeriodRequest> Periods);

public record StudentAttendanceEntry([Required] int StudentId, [Required] AttendanceStatus Status);
public record StudentAttendanceRequest(
    [Required] int SectionId,
    [Required] DateOnly Date,
    [Required] List<StudentAttendanceEntry> Entries);

public record EmployeeAttendanceEntry([Required] int EmployeeId, [Required] AttendanceStatus Status);
public record EmployeeAttendanceRequest(
    [Required] DateOnly Date,
    [Required] List<EmployeeAttendanceEntry> Entries);


public record MarkRequest(
    [Required] int StudentId,
    [Required] int SubjectId,
    [Required, Range(1, 2)] int Semester,
    [Range(0, 1000)] decimal Oral,
    [Range(0, 1000)] decimal Quiz1,
    [Range(0, 1000)] decimal Quiz2,
    [Range(0, 1000)] decimal Homework,
    [Range(0, 1000)] decimal FinalExam);

public record MarkConfigRequest(
    [Range(0, 1000)] decimal MaxOral,
    [Range(0, 1000)] decimal MaxQuiz1,
    [Range(0, 1000)] decimal MaxQuiz2,
    [Range(0, 1000)] decimal MaxHomework,
    [Range(0, 1000)] decimal MaxFinalExam,
    [Range(0, 100)] decimal PassPercent);

public record ReportCardRequest(
    [Required] int SectionId,
    [Required, Range(1, 2)] int Semester,
    [Required] int Year);

public record PerformanceReportRequest(
    [Required] int StudentId,
    [Required] int SubjectId,
    [Required, Range(1, 2)] int Semester,
    string? Behavior,
    string? Notes);


public record ComplaintRequest([Required] string Against, [Required] string Content);
public record ComplaintResolveRequest([Required] ComplaintStatus Status, string? Resolution);
public record PunishmentRequest(int? StudentId, int? EmployeeId, [Required] string Reason, [Required] string Type);
public record WarningRequest([Required] int StudentId, [Required] WarningType Type, [Required] string Reason);
public record SummonRequest([Required] int StudentId, [Required] string Reason, [Required] DateOnly Date);
public record ContactGuardianRequest([Required] string Title, [Required] string Body);
// Dtos/AnnouncementRequest.cs





// public record BookRequest(
//     [Required] string Title,
//     string? Author,
//     string? Isbn,
//     [Required, Range(0, 100000)] int Copies);

// public record MemberRequest([Required] int StudentId);
// public record LoanRequest([Required] int BookId, [Required] int MemberId, [Required] DateOnly DueDate);
// public record ReservationDecisionRequest([Required] ReservationStatus Status);


public record ActivityRequest(
    [Required] string Name,
    [Required] ActivityType Type,
    string? Description,
    string? Schedule,
    [Required, Range(1, 100000)] int Capacity);

public record RegistrationDecisionRequest([Required] RegistrationStatus Status);

// ===== Student Full Profile =====
// Dtos/StudentBasicInfo.cs

// Dtos/EmployeeAttendanceLocalRequest.cs
public class EmployeeAttendanceLocalRequest
{
    public DateOnly Date { get; set; }
    public List<EmployeeAttendanceEntryLocal> Entries { get; set; } = new();
}

public class EmployeeAttendanceEntryLocal
{
    public int LocalEmployeeNumber { get; set; }
    public AttendanceStatus Status { get; set; }
}

// Dtos/PunishmentLocalRequest.cs
public class PunishmentLocalRequest
{
    public int? LocalStudentNumber { get; set; }
    public int? LocalEmployeeNumber { get; set; }
    public string Reason { get; set; } = "";

}

// Dtos/TeacherGradeLocalRequest.cs
public class TeacherGradeLocalRequest
{
    public int TeacherLocalNumber { get; set; }
    public int LocalSubjectId { get; set; }
    public int LocalSectionNumber { get; set; }
}
public class PerformanceReportLocalRequest
{
    public int LocalStudentNumber { get; set; }
    public int LocalSubjectId { get; set; }
    public int Semester { get; set; }
    public string? Behavior { get; set; }
    public string? Notes { get; set; }
}
// Dtos/EmployeeCreateLocalRequest.cs
public class EmployeeCreateLocalRequest
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string? NationalId { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Qualification { get; set; }
    public EmployeeRole Role { get; set; }
}
public class SubjectMarkDto
{
    public int SubjectId { get; set; }
    public int LocalSubjectId { get; set; }
    public string SubjectName { get; set; } = "";
    public decimal Total { get; set; }
    public bool IsPassed { get; set; }
    public int Semester { get; set; }
}
public class AtRiskStudentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public int LocalStudentNumber { get; set; }
    public string? SectionName { get; set; }
    public int LocalSectionNumber { get; set; }
    public string? GradeName { get; set; }
    public int LocalGradeNumber { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public decimal Average { get; set; }
    public decimal Threshold { get; set; }
    public int TotalMarks { get; set; }
    public int FailedSubjects { get; set; }
    public int PassedSubjects { get; set; }
    public object? LastReport { get; set; }
    public List<SubjectMarkDto>? SubjectMarks { get; set; }
}
// Dtos/EmployeeUpdateLocalRequest.cs
public class EmployeeUpdateLocalRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? NationalId { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Qualification { get; set; }
    public EmployeeRole? Role { get; set; }
}
public class MarkLocalRequest
{
    public int LocalStudentNumber { get; set; }
    public int LocalSubjectId { get; set; }
    public int Semester { get; set; }
    public decimal Oral { get; set; }
    public decimal Quiz1 { get; set; }
    public decimal Quiz2 { get; set; }
    public decimal Homework { get; set; }
    public decimal FinalExam { get; set; }
}
public class QuizMarkUpdateRequest
{
    public int? Score { get; set; }
    public int? MaxScore { get; set; }
    public string? Notes { get; set; }
}
// Dtos/RestoreAttendanceLocalRequest.cs
// Dtos/UpdateAttendanceLocalRequest.cs
public class UpdateAttendanceLocalRequest
{
    public int LocalStudentNumber { get; set; }  // ✅ فقط LocalStudentNumber
    public DateOnly? Date { get; set; }          // ✅ اختياري
    public AttendanceStatus Status { get; set; }
    public string? Justification { get; set; }
}
// Dtos/UpdateAbsenceLocalRequest.cs
public class UpdateAbsenceLocalRequest
{
    public DateOnly? Date { get; set; }
    public string? Justification { get; set; }
}
// Dtos/RecordAbsenceLocalRequest.cs
public class RecordAbsenceLocalRequest
{
    public DateOnly? Date { get; set; }          // ✅ اختياري - إذا لم يتم إرساله، يستخدم تاريخ اليوم
    public string? Justification { get; set; }   // سبب الغياب (اختياري)
}
// Dtos/RestoreAttendanceLocalRequest.cs
public class RestoreAttendanceLocalRequest
{
    public int LocalStudentNumber { get; set; }  // ✅ فقط LocalStudentNumber
    public DateOnly? Date { get; set; }          // ✅ اختياري
}
public class QuizMarkUpdateLocalRequest
{
    public int LocalStudentNumber { get; set; }
    public int LocalSubjectId { get; set; }
    public int Semester { get; set; }
    public int QuizTypeId { get; set; }  // 1=Quiz1, 2=Quiz2, 3=Homework, 4=Oral, 5=FinalExam
    public int? Score { get; set; }
    public int? MaxScore { get; set; }
    public string? Notes { get; set; }
}
// Dtos/QuizTypeDto.cs
public class QuizTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int MaxScore { get; set; }
    public bool IsRequired { get; set; }
}
public class QuizMarkLocalRequest
{
    public int LocalStudentNumber { get; set; }
    public int LocalSubjectId { get; set; }
    public int Semester { get; set; }
    public int QuizNumber { get; set; }
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public string? Notes { get; set; }
}
public class StudentUpdateRequesting
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public string? Address { get; set; }
    public string? BloodType { get; set; }
    public DateTime? BirthDate { get; set; }
    public int? LocalSectionNumber { get; set; }
}
public class StudentBasicInfor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int SchoolId { get; set; }
    public int? SectionId { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public string? BloodType { get; set; }
    public string? ChronicDiseases { get; set; }
    public string? Allergies { get; set; }
    public string? HealthNotes { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Address { get; set; }
    public bool DismissalWarning { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public StudentBasicInfor(
        int id, 
        string name, 
        string email, 
        int schoolId, 
        int? sectionId,
        string? guardianName, 
        string? guardianPhone, 
        string? bloodType,
        string? chronicDiseases, 
        string? allergies, 
        string? healthNotes,
        DateTime? birthDate, 
        string? address,
        bool dismissalWarning, 
        DateTime createdAt)
    {
        Id = id;
        Name = name;
        Email = email;
        SchoolId = schoolId;
        SectionId = sectionId;
        GuardianName = guardianName;
        GuardianPhone = guardianPhone;
        BloodType = bloodType;
        ChronicDiseases = chronicDiseases;
        Allergies = allergies;
        HealthNotes = healthNotes;
        BirthDate = birthDate;
        Address = address;
        DismissalWarning = dismissalWarning;
        CreatedAt = createdAt;
    }
}

public record StudentProfileSection(
    int Id, 
    string Name, 
    string GradeName, 
    int LocalGradeNumber,
    int LocalSectionNumber  // ← إضافة الرقم المحلي للشعبة
);

public record StudentProfileSubject(int Id, string Name, int? TeacherId);

public record StudentProfileMark(
    int SubjectId, string SubjectName, int Semester,
    decimal Oral, decimal Quiz1, decimal Quiz2, decimal Homework,
    decimal FinalExam, decimal Total, DateTime UpdatedAt);

public record StudentProfileReportCardSubject(string SubjectName, decimal Total);
public record StudentProfileReportCard(
    int Id, int Semester, int Year, decimal Average, int? Rank, bool Passed,
    List<StudentProfileReportCardSubject> Subjects);

public record StudentProfilePerformanceReport(
    int Id, string SubjectName, int Semester, string Behavior,
    string Notes, DateTime CreatedAt);

public record StudentProfileAttendance(DateOnly Date, string Status);

public record StudentProfilePeriod(int Order, string SubjectName);
public record StudentProfileSchedule(string Day, List<StudentProfilePeriod> Periods);

public record StudentProfileLibraryMember(int Id, string Status);
public record StudentProfileLoan(int Id, string BookTitle, DateOnly LoanDate,
    DateOnly DueDate, DateOnly? ReturnDate, string Status);
public record StudentProfileReservation(int Id, string BookTitle, DateOnly Date, string Status);
public record StudentProfileLibrary(
    StudentProfileLibraryMember? Membership,
    List<StudentProfileLoan> Loans,
    List<StudentProfileReservation> Reservations);

public record StudentProfileActivity(int Id, string Name, string Type,
    string? Schedule, string? RegistrationStatus);

public record StudentProfileWarning(int Id, string Type, string Reason, DateTime CreatedAt);
public record StudentProfilePunishment(int Id, string Reason, string Type, DateTime CreatedAt);
public record StudentProfileGuardianSummon(int Id, string Reason, DateOnly Date, DateTime CreatedAt);
public record StudentProfileComplaint(int Id, string Against, string Content,
    string Status, string? Resolution, DateTime CreatedAt);
public record StudentProfileNotification(int Id, string Title, string Body,
    string Type, bool IsRead, DateTime CreatedAt);

// Dtos/StudentFullProfileResponse.cs

public record StudentFullProfileResponse(
    StudentBasicInfor Student,
    StudentProfileSection? Section,
    List<StudentProfileSubject> Subjects,
    List<StudentProfileMark> Marks,
    List<StudentProfileReportCard> ReportCards,
    List<StudentProfilePerformanceReport> PerformanceReports,
    List<StudentProfileAttendance> Attendance,
    StudentProfileLibrary Library,
    List<StudentProfileActivity> Activities,
    List<StudentProfileWarning> Warnings,
    List<StudentProfilePunishment> Punishments,
    List<StudentProfileComplaint> Complaints,
    List<StudentProfileNotification> Notifications
);

// ===== Teacher Full Profile =====
public record TeacherBasicInfo(
    int Id, string Name, string Email, int SchoolId,
    string Phone, string Address, DateTime? BirthDate, string Qualification,
    bool IsDismissed, DateTime CreatedAt);

public record TeacherProfileSubject(int Id, string Name, int GradeId, string GradeName, int GradeLevel);
public record TeacherProfileStudent(int Id, string Name);
public record TeacherProfileSection(int Id, string Name, string GradeName, int GradeLevel, List<TeacherProfileStudent> Students);
public record TeacherProfilePeriod(int Order, string SubjectName, int SectionId, string SectionName);
public record TeacherProfileDaySchedule(string Day, List<TeacherProfilePeriod> Periods);
public record TeacherSchoolInfo(
    int SchoolId,
    string SchoolName,
    List<TeacherProfileSubject> Subjects,
    List<TeacherProfileSection> Sections,
    List<TeacherProfileDaySchedule> Schedule);

public record TeacherProfileMark(
    int MarkId, int StudentId, string StudentName, int SubjectId, string SubjectName,
    int Semester, decimal Oral, decimal Quiz1, decimal Quiz2, decimal Homework,
    decimal FinalExam, decimal Total, DateTime UpdatedAt);

public record TeacherProfileAttendance(DateOnly Date, string Status);
public record TeacherProfileLeave(int Id, DateOnly StartDate, DateOnly EndDate, string Reason);
public record TeacherProfilePerformanceReport(
    int Id, int StudentId, string StudentName, string SubjectName,
    int Semester, string Behavior, string Notes, DateTime CreatedAt);
public record TeacherProfileComplaint(int Id, string Against, string Content,
    string Status, string? Resolution, DateTime CreatedAt);
public record TeacherProfilePunishment(int Id, string Reason, string Type, DateTime CreatedAt);
public record TeacherProfileNotification(int Id, string Title, string Body,
    string Type, bool IsRead, DateTime CreatedAt);

// ===== Paginated Response (generic) =====
public record PaginatedResponse<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

// ===== Counselor Full Profile =====
public record CounselorBasicInfo(int Id, string Name, string Email,int SchoolId, string Phone, DateTime CreatedAt);
public record CounselorBasicInfor(int Id, string Name, string Email, string Phone, DateTime CreatedAt);
public record CounselorSectionInfo(int Id, string Name, string GradeName, int GradeLevel, int StudentCount);
public record CounselorWarningSimple(int Id, int StudentId, string StudentName, string Type, string Reason, DateTime CreatedAt);
public record CounselorSummonSimple(int Id, int StudentId, string StudentName, string Reason, DateOnly Date, DateTime CreatedAt);
public record CounselorAttendanceRecent(int StudentId, string StudentName, DateOnly Date, string Status);
public record CounselorStudentItem(int Id, string Name, string? BloodType, string? GuardianPhone, bool DismissalWarning);

public record CounselorFullProfileResponse(
    CounselorBasicInfo Counselor,
    List<CounselorSectionInfo> Sections,
    List<CounselorWarningSimple> Warnings,
    List<CounselorSummonSimple> GuardianSummons,
    List<CounselorAttendanceRecent> RecentAttendance);

public record TeacherFullProfileResponse(
    TeacherBasicInfo Teacher,
    List<TeacherSchoolInfo> Schools,
    List<TeacherProfileMark> Marks,
    List<TeacherProfileAttendance> Attendance,
    List<TeacherProfileLeave> Leaves,
    List<TeacherProfilePerformanceReport> PerformanceReports,
    List<TeacherProfileComplaint> Complaints,
    List<TeacherProfilePunishment> Punishments,
    List<TeacherProfileNotification> Notifications);
