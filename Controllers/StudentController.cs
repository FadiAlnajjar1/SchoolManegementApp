using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Auth;
using SchoolManagement.Api.Data;
using SchoolManagement.Api.Dtos;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/student")]
[Authorize(Roles = Roles.Student)]
public class StudentController(AppDbContext db) : ControllerBase
{
    private int StudentId => User.GetUserId();
    private int SchoolId => User.GetSchoolId();

    private async Task<Student?> MeAsync() => await db.Students.FindAsync(StudentId);

    // ============================================
    // الجدول الدراسي
    // ============================================

    // ============================================
// جلب صورة جدول الشعبة للطالب
// ============================================

[HttpGet("schedule-image")]
public async Task<IActionResult> GetScheduleImage()
{
    var me = await MeAsync();
    if (me?.SectionId is null)
        return NotFound(new { message = "أنت غير مسجل في أي شعبة" });

    var section = await db.Sections
        .Include(s => s.Grade)
        .FirstOrDefaultAsync(s => s.Id == me.SectionId && s.SchoolId == SchoolId);

    if (section is null)
        return NotFound(new { message = "الشعبة غير موجودة" });

    var image = await db.ScheduleImages
        .Where(s => s.SchoolId == SchoolId && 
                    s.SectionId == section.Id && 
                    s.Type == ScheduleImageType.Section)
        .OrderByDescending(s => s.CreatedAt)
        .Select(s => new
        {
            s.Id,
            s.ImageUrl,
            s.Description,
            s.CreatedAt,
            LocalGradeNumber = section.Grade != null ? section.Grade.LocalGradeNumber : 0,
            GradeName = section.Grade != null ? section.Grade.Name : null,
            LocalSectionNumber = section.LocalSectionNumber,
            SectionName = section.Name
        })
        .FirstOrDefaultAsync();

    if (image is null)
        return NotFound(new { message = "لا توجد صورة جدول لشعبتك" });

    return Ok(image);
}

    // ============================================
    // المواد الدراسية
    // ============================================

    [HttpGet("subjects")]
public async Task<IActionResult> GetSubjects()
{
    var me = await MeAsync();
    if (me?.SectionId is null) 
        return Ok(new StudentSubjectsResponse
        {
            Message = "أنت غير مسجل في أي شعبة",
            Subjects = new List<StudentSubjectDto>()
        });

    // ✅ جلب الشعبة مع الصف باستخدام Local IDs فقط
    var sectionData = await db.Sections
        .Include(s => s.Grade)
        .Where(s => s.Id == me.SectionId && s.SchoolId == SchoolId)
        .Select(s => new
        {
            LocalSectionNumber = s.LocalSectionNumber,
            SectionName = s.Name,
            LocalGradeNumber = s.Grade != null ? s.Grade.LocalGradeNumber : 0,
            GradeName = s.Grade != null ? s.Grade.Name : "غير معروف",
            AcademicYear = s.Grade != null ? s.Grade.AcademicYear : 0
            // ✅ لا حاجة لـ GradeId
        })
        .FirstOrDefaultAsync();

    if (sectionData is null) 
        return Ok(new StudentSubjectsResponse
        {
            Message = "الشعبة غير موجودة",
            Subjects = new List<StudentSubjectDto>()
        });

    // ✅ جلب المواد باستخدام Local Grade Number بدلاً من GradeId
    var subjects = await db.Subjects
        .Where(s => s.SchoolId == SchoolId && 
                    s.Grade != null && 
                    s.Grade.LocalGradeNumber == sectionData.LocalGradeNumber)  // ✅ استخدام LocalGradeNumber
        .Select(s => new StudentSubjectDto
        {
            LocalSubjectId = s.LocalSubjectId,
            Name = s.Name,
            TeacherName = s.Teacher != null ? s.Teacher.Name : null,
            LocalTeacherNumber = db.EmployeeSchools
                .Where(es => es.EmployeeId == s.TeacherId && 
                             es.SchoolId == SchoolId && 
                             es.IsActive)
                .Select(es => (int?)es.LocalEmployeeNumber)
                .FirstOrDefault()
        })
        .ToListAsync();

    return Ok(new StudentSubjectsResponse
    {
        LocalSectionNumber = sectionData.LocalSectionNumber,
        SectionName = sectionData.SectionName,
        LocalGradeNumber = sectionData.LocalGradeNumber,
        GradeName = sectionData.GradeName,
        AcademicYear = sectionData.AcademicYear,
        Subjects = subjects,
        TotalSubjects = subjects.Count
    });
}

    // ============================================
    // العلامات
    // ============================================

    [HttpGet("marks")]
public async Task<IActionResult> GetMarks([FromQuery] int? semester)
{
    var query = db.Marks
        .Where(m => m.StudentId == StudentId);

    if (semester is not null) 
        query = query.Where(m => m.Semester == semester);

    var marks = await query
        .Select(m => new
        {
            SubjectName = db.Subjects
                .Where(s => s.Id == m.SubjectId)
                .Select(s => s.Name)
                .FirstOrDefault() ?? "غير معروف",
            LocalSubjectId = db.Subjects
                .Where(s => s.Id == m.SubjectId)
                .Select(s => s.LocalSubjectId)
                .FirstOrDefault(),
            LocalTeacherNumber = db.Subjects
                .Where(s => s.Id == m.SubjectId)
                .Select(s => s.TeacherId)
                .FirstOrDefault() != null ?
                db.EmployeeSchools
                    .Where(es => es.EmployeeId == db.Subjects
                        .Where(s => s.Id == m.SubjectId)
                        .Select(s => s.TeacherId)
                        .FirstOrDefault() && 
                                 es.SchoolId == SchoolId && 
                                 es.IsActive)
                    .Select(es => (int?)es.LocalEmployeeNumber)
                    .FirstOrDefault() : null,
            TeacherName = db.Subjects
                .Where(s => s.Id == m.SubjectId)
                .Select(s => s.Teacher != null ? s.Teacher.Name : null)
                .FirstOrDefault(),
            m.Semester,
            m.Oral,
            m.Quiz1,
            m.Quiz2,
            m.Homework,
            m.FinalExam,
            m.Total,
            m.UpdatedAt
        })
        .ToListAsync();

    return Ok(marks);
}

    // ============================================
    // بطاقات التقارير
    // ============================================

    // [HttpGet("report-cards")]
    // public async Task<IActionResult> GetReportCards()
    // {
    //     var reportCards = await db.ReportCards
    //         .Include(r => r.Subjects)
    //         .Where(r => r.StudentId == StudentId)
    //         .OrderByDescending(r => r.Year)
    //         .ThenByDescending(r => r.Semester)
    //         .Select(r => new
    //         {
    //             r.Id,
    //             r.Semester,
    //             r.Year,
    //             r.Average,
    //             r.Rank,
    //             r.Passed,
    //             Subjects = r.Subjects.Select(s => new
    //             {
    //                 s.SubjectName,
    //                 LocalSubjectId = db.Subjects
    //                     .Where(sub => sub.Name == s.SubjectName && sub.SchoolId == SchoolId)
    //                     .Select(sub => sub.LocalSubjectId)
    //                     .FirstOrDefault(),
    //                 s.Total,
    //                 s.GradeLetter
    //             }).ToList()
    //         })
    //         .ToListAsync();

    //     return Ok(reportCards);
    // }

    // ============================================
    // الحضور والغياب
    // ============================================

    [HttpGet("attendance")]
    public async Task<IActionResult> GetAttendance()
    {
        var attendance = await db.StudentAttendances
            .Where(a => a.StudentId == StudentId)
            .OrderByDescending(a => a.Date)
            .Take(200)
            .Select(a => new
            {
                a.Date,
                a.Status,
                StatusName = a.Status.ToString(),
                a.SectionId,
                LocalSectionNumber = db.Sections
                    .Where(s => s.Id == a.SectionId)
                    .Select(s => s.LocalSectionNumber)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(attendance);
    }

    // ============================================
// Feed - الإعلانات والأنشطة مع Local IDs
// ============================================

[HttpGet("feed")]
public async Task<IActionResult> GetFeed()
{
    var now = DateTime.UtcNow;
    
    // ✅ الإعلانات مع Local IDs
    var announcements = await db.Announcements
        .Where(a => a.SchoolId == SchoolId && 
                   a.IsActive &&
                   (a.Audience == AnnouncementAudience.All || 
                    a.Audience == AnnouncementAudience.Students) &&
                   (a.ExpiryDate == null || a.ExpiryDate > now))
        .OrderByDescending(a => a.CreatedAt)
        .Select(a => new
        {
            a.Id,
            LocalAnnouncementId = a.LocalAnnouncementId,  // ✅ Local ID للإعلان
            a.Title,
            Description = a.Body,
            Date = a.CreatedAt.ToString("yyyy-MM-dd"),
            a.ExpiryDate,
            Type = "announcement"
        })
        .ToListAsync();

    // ✅ الأنشطة مع Local IDs
    var activities = await db.Activities
        .Where(a => a.SchoolId == SchoolId)
        .OrderByDescending(a => a.CreatedAt)
        .Select(a => new
        {
            a.Id,
            LocalActivityId = a.LocalActivityId,  // ✅ Local ID للنشاط
            Title = a.Name,
            Description = a.Description ?? a.Schedule ?? "",
            Date = a.CreatedAt.ToString("yyyy-MM-dd"),
            ExpiryDate = (DateTime?)null,
            Type = "activity"
        })
        .ToListAsync();

    // ✅ دمج الإعلانات والأنشطة في قائمة واحدة
    var allItems = new List<object>();
    allItems.AddRange(announcements);
    allItems.AddRange(activities);

    // ✅ ترتيب حسب التاريخ (الأحدث أولاً)
    var sortedFeed = allItems
        .OrderByDescending(x => DateTime.Parse(((dynamic)x).Date))
        .ToList();

    return Ok(new
    {
        success = true,
        message = "تم جلب البيانات بنجاح",
        data = new
        {
            announcements = announcements,  // ← مع Local IDs
            activities = activities,        // ← مع Local IDs
            feed = sortedFeed               // ← مع Local IDs
        }
    });
}

    // ============================================
    // الشكاوى
    // ============================================

    [HttpPost("complaints")]
    public async Task<IActionResult> CreateComplaint(ComplaintRequest request)
    {
        var me = await MeAsync();
        if (me is null) return NotFound();

        var complaint = new Complaint
        {
            FromUserId = StudentId,
            FromUserType = UserType.Student,
            FromName = me.Name,
            Against = request.Against,
            SchoolId = SchoolId,
            Content = request.Content,
        };

        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        return Created($"api/student/complaints/{complaint.Id}", new
        {
            complaint.Id,
            complaint.FromUserId,
            complaint.FromName,
            complaint.Against,
            complaint.Content,
            complaint.Status,
            complaint.CreatedAt
        });
    }

    [HttpGet("complaints")]
    public async Task<IActionResult> GetMyComplaints()
    {
        var complaints = await db.Complaints
            .Where(c => c.FromUserId == StudentId && c.FromUserType == UserType.Student)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Against,
                c.Content,
                c.Status,
                StatusName = c.Status.ToString(),
                c.Resolution,
                c.CreatedAt
            })
            .ToListAsync();

        return Ok(complaints);
    }

    // ============================================
    // الأنشطة
    // ============================================

    // ============================================
// عرض الأنشطة (مع Local IDs)
// ============================================

[HttpGet("activities")]
public async Task<IActionResult> GetActivities()
{
    var activities = await db.Activities
        .Where(a => a.SchoolId == SchoolId)
        .Select(a => new
        {
            a.Id,
            a.LocalActivityId,  // ✅ إضافة Local ID
            a.Name,
            a.Description,
            a.Type,
            TypeName = a.Type.ToString(),
            a.Schedule,
            a.Capacity,
            RegisteredCount = db.ActivityRegistrations
                .Count(r => r.ActivityId == a.Id && r.Status == RegistrationStatus.Approved),
            IsRegistered = db.ActivityRegistrations
                .Any(r => r.ActivityId == a.Id && r.StudentId == StudentId),
            IsFull = db.ActivityRegistrations
                .Count(r => r.ActivityId == a.Id && r.Status == RegistrationStatus.Approved) >= a.Capacity
        })
        .ToListAsync();

    return Ok(activities);
}

// ============================================
// التسجيل في نشاط (باستخدام LocalActivityId)
// ============================================

[HttpPost("activities/{localActivityId:int}/register")]
public async Task<IActionResult> RegisterActivity(int localActivityId)
{
    // ✅ البحث عن النشاط باستخدام LocalActivityId
    var activity = await db.Activities
        .FirstOrDefaultAsync(a => a.SchoolId == SchoolId && 
                                  a.LocalActivityId == localActivityId);
            
    if (activity is null) 
        return NotFound(new { message = "النشاط غير موجود" });

    // التحقق من التسجيل المسبق
    var existingRegistration = await db.ActivityRegistrations
        .FirstOrDefaultAsync(r => r.ActivityId == activity.Id && r.StudentId == StudentId);

    if (existingRegistration is not null)
    {
        if (existingRegistration.Status == RegistrationStatus.Approved)
            return BadRequest(new { message = "أنت مسجل في هذا النشاط بالفعل" });
        if (existingRegistration.Status == RegistrationStatus.Pending)
            return BadRequest(new { message = "طلب التسجيل قيد المراجعة" });
    }

    // التحقق من السعة
    var approved = await db.ActivityRegistrations
        .CountAsync(r => r.ActivityId == activity.Id && r.Status == RegistrationStatus.Approved);
            
    if (approved >= activity.Capacity)
        return BadRequest(new { message = "اكتملت سعة النشاط" });

    // إنشاء التسجيل
    var registration = new ActivityRegistration 
    { 
        ActivityId = activity.Id, 
        StudentId = StudentId,
        Status = RegistrationStatus.Pending,
        CreatedAt = DateTime.UtcNow
    };

    db.ActivityRegistrations.Add(registration);
    await db.SaveChangesAsync();

    return Created($"api/student/activities/{localActivityId}/register", new
    {
        registration.Id,
        ActivityLocalId = localActivityId,  // ✅ Local ID
        registration.ActivityId,
        registration.StudentId,
        registration.Status,
        StatusName = registration.Status.ToString(),
        registration.CreatedAt
    });
}

// ============================================
// عرض تسجيلاتي (مع Local IDs)
// ============================================

[HttpGet("activities/registrations")]
public async Task<IActionResult> GetMyRegistrations()
{
    var registrations = await db.ActivityRegistrations
        .Include(r => r.Activity)
        .Where(r => r.StudentId == StudentId)
        .Select(r => new
        {
            r.Id,
            r.ActivityId,
            ActivityLocalId = r.Activity != null ? r.Activity.LocalActivityId : 0,  // ✅ Local ID
            ActivityName = r.Activity != null ? r.Activity.Name : "غير معروف",
            ActivityType = r.Activity != null ? r.Activity.Type.ToString() : "غير معروف",
            ActivitySchedule = r.Activity != null ? r.Activity.Schedule : null,
            r.Status,
            StatusName = r.Status.ToString(),
            r.CreatedAt
        })
        .ToListAsync();

    return Ok(registrations);
}

// ============================================
// إلغاء التسجيل من نشاط
// ============================================

[HttpDelete("activities/registrations/{id:int}")]
public async Task<IActionResult> CancelRegistration(int id)
{
    var registration = await db.ActivityRegistrations
        .Include(r => r.Activity)
        .FirstOrDefaultAsync(r => r.Id == id && r.StudentId == StudentId);

    if (registration is null)
        return NotFound(new { message = "التسجيل غير موجود" });

    if (registration.Status == RegistrationStatus.Approved)
        return BadRequest(new { message = "لا يمكن إلغاء تسجيل تمت الموافقة عليه، راجع مشرف النشاطات" });

    db.ActivityRegistrations.Remove(registration);
    await db.SaveChangesAsync();

    return Ok(new
    {
        success = true,
        message = "تم إلغاء التسجيل بنجاح"
    });
}

    // ============================================
    // المكتبة
    // ============================================

    [HttpGet("library/books")]
public async Task<IActionResult> GetBooks()
{
    var books = await db.Books
        .Where(b => b.SchoolId == SchoolId)
        .Select(b => new
        {
            LocalBookNumber = b.LocalBookNumber,  // ✅ Local ID
            b.Title,
            b.Author,
            b.Isbn,
            b.Copies,
            AvailableCopies = b.AvailableCopies,
            IsAvailable = b.AvailableCopies > 0
        })
        .ToListAsync();

    return Ok(books);
}
[HttpGet("library/loans")]
public async Task<IActionResult> GetMyLoans()
{
    var member = await db.LibraryMembers
        .Where(m => m.StudentId == StudentId)
        .Select(m => new { m.Id, m.LocalMemberNumber })
        .FirstOrDefaultAsync();

    if (member is null)
        return Ok(new { message = "لست عضواً في المكتبة", loans = Array.Empty<object>() });

    var loans = await db.BookLoans
        .Where(l => l.MemberId == member.Id)
        .OrderByDescending(l => l.LoanDate)
        .Select(l => new
        {
            LocalLoanNumber = l.LocalLoanNumber,  // ✅ Local ID
            BookTitle = l.Book != null ? l.Book.Title : "غير معروف",
            LocalBookNumber = l.Book != null ? l.Book.LocalBookNumber : 0,  // ✅ Local ID
            l.LoanDate,
            l.DueDate,
            l.ReturnDate,
            Status = l.Status.ToString(),
            IsOverdue = l.DueDate < DateOnly.FromDateTime(DateTime.Today) && l.Status == LoanStatus.Active
        })
        .ToListAsync();

    return Ok(new
    {
        MemberLocalNumber = member.LocalMemberNumber,
        Loans = loans,
        TotalLoans = loans.Count,
        ActiveLoans = loans.Count(l => l.Status == "Active")
    });
}
[HttpPost("library/books/{localBookNumber:int}/reserve")]
public async Task<IActionResult> ReserveBook(int localBookNumber)
{
    // ✅ البحث عن الكتاب باستخدام LocalBookNumber
    var book = await db.Books
        .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                  b.LocalBookNumber == localBookNumber);
            
    if (book is null) 
        return NotFound(new { message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" });

    // ✅ البحث عن العضو باستخدام StudentId
    var member = await db.LibraryMembers
        .FirstOrDefaultAsync(m => m.StudentId == StudentId);
            
    if (member is null) 
        return BadRequest(new { message = "لست عضواً في المكتبة — راجع أمين المكتبة" });
            
    if (member.Status != MemberStatus.Active) 
        return BadRequest(new { message = "عضويتك موقوفة" });

    // ✅ التحقق من وجود حجز معلق
    var existingReservation = await db.BookReservations
        .FirstOrDefaultAsync(r => r.BookId == book.Id && 
                                  r.MemberId == member.Id && 
                                  r.Status == ReservationStatus.Pending);
                                      
    if (existingReservation is not null)
        return BadRequest(new { message = "لديك حجز معلق على هذا الكتاب" });

    // ✅ حساب LocalLoanNumber للحجز
    var maxLocalNumber = await db.BookReservations
        .Where(r => r.BookId == book.Id)
        .Select(r => (int?)r.Id) // يمكن استخدام Id كرقم محلي مؤقت
        .MaxAsync() ?? 0;

    var reservation = new BookReservation
    {
        BookId = book.Id,
        MemberId = member.Id,
        Date = DateOnly.FromDateTime(DateTime.Today),
        Status = ReservationStatus.Pending
    };

    db.BookReservations.Add(reservation);
    await db.SaveChangesAsync();

    return Created($"api/student/library/reservations/{reservation.Id}", new
    {
        LocalBookNumber = book.LocalBookNumber,  // ✅ Local ID
        BookTitle = book.Title,
        MemberLocalNumber = member.LocalMemberNumber,  // ✅ Local ID
        reservation.Date,
        Status = reservation.Status.ToString()
    });
}
[HttpGet("library/reservations")]
public async Task<IActionResult> GetMyReservations()
{
    var member = await db.LibraryMembers
        .Where(m => m.StudentId == StudentId)
        .Select(m => new { m.Id, m.LocalMemberNumber })
        .FirstOrDefaultAsync();

    if (member is null)
        return Ok(new { message = "لست عضواً في المكتبة", reservations = Array.Empty<object>() });

    var reservations = await db.BookReservations
        .Where(r => r.MemberId == member.Id)
        .OrderByDescending(r => r.Date)
        .Select(r => new
        {
            BookTitle = r.Book != null ? r.Book.Title : "غير معروف",
            LocalBookNumber = r.Book != null ? r.Book.LocalBookNumber : 0,  // ✅ Local ID
            r.Date,
            Status = r.Status.ToString(),
            r.CreatedAt
        })
        .ToListAsync();

    return Ok(new
    {
        MemberLocalNumber = member.LocalMemberNumber,
        Reservations = reservations,
        TotalReservations = reservations.Count,
        PendingReservations = reservations.Count(r => r.Status == "Pending")
    });
}
[HttpGet("library/books/{localBookNumber:int}")]
public async Task<IActionResult> GetBook(int localBookNumber)
{
    var book = await db.Books
        .Where(b => b.SchoolId == SchoolId && 
                    b.LocalBookNumber == localBookNumber)
        .Select(b => new
        {
            LocalBookNumber = b.LocalBookNumber,  // ✅ Local ID
            b.Title,
            b.Author,
            b.Isbn,
            b.Copies,
            b.AvailableCopies,
            IsAvailable = b.AvailableCopies > 0,
            b.CreatedAt
        })
        .FirstOrDefaultAsync();

    if (book is null)
        return NotFound(new { message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" });

    return Ok(book);
}

    // ============================================
    // التحذيرات والعقوبات
    // ============================================

    [HttpGet("warnings")]
    public async Task<IActionResult> GetMyWarnings()
    {
        var warnings = await db.Warnings
            .Where(w => w.StudentId == StudentId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new
            {
                w.Id,
                w.Type,
                TypeName = w.Type.ToString(),
                w.Reason,
                w.CreatedAt,
                IssuedBy = db.Employees
                    .Where(e => e.Id == w.IssuedById)
                    .Select(e => e.Name)
                    .FirstOrDefault() ?? "الإدارة"
            })
            .ToListAsync();

        return Ok(warnings);
    }

    [HttpGet("punishments")]
    public async Task<IActionResult> GetMyPunishments()
    {
        var punishments = await db.Punishments
            .Where(p => p.StudentId == StudentId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.Type,
                TypeName = p.Type.ToString(),
                p.Reason,
                p.CreatedAt,
                IssuedBy = db.Employees
                    .Where(e => e.Id == p.IssuedById)
                    .Select(e => e.Name)
                    .FirstOrDefault() ?? "الإدارة"
            })
            .ToListAsync();

        return Ok(punishments);
    }

    // ============================================
    // الملف الشخصي الكامل
    // ============================================

    [HttpGet("full-profile")]
public async Task<IActionResult> GetFullProfile()
{
    var me = await db.Students
        .Include(s => s.Section)
            .ThenInclude(sec => sec!.Grade)
        .FirstOrDefaultAsync(s => s.Id == StudentId);

    if (me is null) return NotFound();

    // ✅ معلومات الطالب الأساسية
    var studentInfo = new
    {
        me.Name,
        me.Email,
        LocalStudentNumber = me.LocalStudentNumber,
        SectionName = me.Section?.Name,
        LocalSectionNumber = me.Section?.LocalSectionNumber ?? 0,
        GradeName = me.Section?.Grade?.Name,
        LocalGradeNumber = me.Section?.Grade?.LocalGradeNumber ?? 0,
        AcademicYear = me.Section?.Grade?.AcademicYear ?? 0,
        me.GuardianName,
        me.GuardianPhone,
        me.BloodType,
        me.ChronicDiseases,
        me.Allergies,
        me.HealthNotes,
        me.BirthDate,
        me.Address,
        me.DismissalWarning,
        me.IsPhoneVerified,
        me.CreatedAt
    };

    // ✅ المواد الدراسية (ترتبط بـ GradeId)
    // ✅ المواد الدراسية - باستخدام List<object>
var subjects = new List<object>();

if (me.SectionId is not null && me.Section?.Grade != null)
{
    var subjectList = await db.Subjects
        .Where(s => s.SchoolId == SchoolId && 
                    s.Grade != null && 
                    s.Grade.LocalGradeNumber == me.Section.Grade.LocalGradeNumber)
        .Select(s => new
        {
            LocalSubjectId = s.LocalSubjectId,
            s.Name,
            TeacherName = s.Teacher != null ? s.Teacher.Name : null,
            LocalTeacherNumber = db.EmployeeSchools
                .Where(es => es.EmployeeId == s.TeacherId && 
                             es.SchoolId == SchoolId && 
                             es.IsActive)
                .Select(es => (int?)es.LocalEmployeeNumber)
                .FirstOrDefault()
        })
        .ToListAsync();
    
    subjects.AddRange(subjectList);  // ✅ إضافة القائمة
}

    // ✅ العلامات
    var marks = await db.Marks
        .Where(m => m.StudentId == StudentId)
        .OrderByDescending(m => m.Semester)
        .Select(m => new
        {
            SubjectName = db.Subjects
                .Where(s => s.Id == m.SubjectId)
                .Select(s => s.Name)
                .FirstOrDefault() ?? "غير معروف",
            LocalSubjectId = db.Subjects
                .Where(s => s.Id == m.SubjectId)
                .Select(s => s.LocalSubjectId)
                .FirstOrDefault(),
            LocalTeacherNumber = db.EmployeeSchools
                .Where(es => es.EmployeeId == db.Subjects
                    .Where(s => s.Id == m.SubjectId)
                    .Select(s => s.TeacherId)
                    .FirstOrDefault() && 
                             es.SchoolId == SchoolId && 
                             es.IsActive)
                .Select(es => (int?)es.LocalEmployeeNumber)
                .FirstOrDefault(),
            m.Semester,
            m.Oral,
            m.Quiz1,
            m.Quiz2,
            m.Homework,
            m.FinalExam,
            m.Total,
            m.UpdatedAt
        })
        .ToListAsync();

    // ✅ بطاقات التقارير
    var reportCards = await db.ReportCards
        .Where(r => r.StudentId == StudentId)
        .OrderByDescending(r => r.Year)
        .ThenByDescending(r => r.Semester)
        .Select(r => new
        {
            r.Semester,
            r.Year,
            r.Average,
            r.Rank,
            r.Passed,
            Subjects = r.Subjects.Select(s => new
            {
                s.SubjectName,
                LocalSubjectId = db.Subjects
                    .Where(sub => sub.Name == s.SubjectName && sub.SchoolId == SchoolId)
                    .Select(sub => sub.LocalSubjectId)
                    .FirstOrDefault(),
                s.Total,
            }).ToList()
        })
        .ToListAsync();

    // ✅ الحضور
    var attendance = await db.StudentAttendances
        .Where(a => a.StudentId == StudentId)
        .OrderByDescending(a => a.Date)
        .Take(200)
        .Select(a => new
        {
            a.Date,
            Status = a.Status.ToString(),
            LocalSectionNumber = db.Sections
                .Where(s => s.Id == a.SectionId)
                .Select(s => s.LocalSectionNumber)
                .FirstOrDefault()
        })
        .ToListAsync();

    // ✅ الجدول الدراسي (يرتبط بـ SectionId)
    // var schedule = me.SectionId is not null ?
    //     await db.Schedules
    //         .Where(s => s.SectionId == me.SectionId)  // ✅ Schedule يرتبط بـ SectionId
    //         .OrderBy(s => s.Day)
    //         .Select(s => new
    //         {
    //             Day = s.Day.ToString(),
    //             Periods = s.Periods
    //                 .OrderBy(p => p.Order)
    //                 .Select(p => new
    //                 {
    //                     p.Order,
    //                     SubjectName = p.Subject != null ? p.Subject.Name : "غير محدد",
    //                     LocalSubjectId = p.Subject != null ? p.Subject.LocalSubjectId : 0,
    //                     p.StartTime,
    //                     p.EndTime
    //                 })
    //                 .ToList()
    //         })
    //         .ToListAsync() : new List<object>();

    // ✅ صورة جدول الشعبة
    var scheduleImage = me.SectionId is not null ?
        await db.ScheduleImages
            .Where(s => s.SchoolId == SchoolId && 
                        s.SectionId == me.SectionId && 
                        s.Type == ScheduleImageType.Section)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.ImageUrl,
                s.Description,
                s.CreatedAt
            })
            .FirstOrDefaultAsync() : null;

    // ✅ المكتبة
    var member = await db.LibraryMembers
        .Where(m => m.StudentId == StudentId)
        .Select(m => new
        {
            LocalMemberNumber = m.LocalMemberNumber,
            Status = m.Status.ToString(),
            m.CreatedAt
        })
        .FirstOrDefaultAsync();

    var memberId = await db.LibraryMembers
        .Where(m => m.StudentId == StudentId)
        .Select(m => m.Id)
        .FirstOrDefaultAsync();

    // var loans = memberId > 0 ?
    //     await db.BookLoans
    //         .Where(l => l.MemberId == memberId)
    //         .OrderByDescending(l => l.LoanDate)
    //         .Select(l => new
    //         {
    //             LocalLoanNumber = l.LocalLoanNumber,
    //             BookTitle = l.Book != null ? l.Book.Title : "غير معروف",
    //             LocalBookNumber = l.Book != null ? l.Book.LocalBookNumber : 0,
    //             l.LoanDate,
    //             l.DueDate,
    //             l.ReturnDate,
    //             Status = l.Status.ToString(),
    //             IsOverdue = l.DueDate < DateOnly.FromDateTime(DateTime.Today) && l.Status == LoanStatus.Active
    //         })
    //         .ToListAsync() : new List<object>();

    // var reservations = memberId > 0 ?
    //     await db.BookReservations
    //         .Where(r => r.MemberId == memberId)
    //         .OrderByDescending(r => r.Date)
    //         .Select(r => new
    //         {
    //             BookTitle = r.Book != null ? r.Book.Title : "غير معروف",
    //             LocalBookNumber = r.Book != null ? r.Book.LocalBookNumber : 0,
    //             r.Date,
    //             Status = r.Status.ToString()
    //         })
    //         .ToListAsync() : new List<object>();

    // ✅ الأنشطة
    var activities = await db.ActivityRegistrations
        .Where(r => r.StudentId == StudentId)
        .Select(r => new
        {
            ActivityName = db.Activities
                .Where(a => a.Id == r.ActivityId)
                .Select(a => a.Name)
                .FirstOrDefault() ?? "غير معروف",
            ActivityType = db.Activities
                .Where(a => a.Id == r.ActivityId)
                .Select(a => a.Type.ToString())
                .FirstOrDefault() ?? "غير معروف",
            Status = r.Status.ToString(),
            r.CreatedAt
        })
        .ToListAsync();

    // ✅ التحذيرات
    var warnings = await db.Warnings
        .Where(w => w.StudentId == StudentId)
        .OrderByDescending(w => w.CreatedAt)
        .Select(w => new
        {
            Type = w.Type.ToString(),
            w.Reason,
            w.CreatedAt,
            IssuedBy = db.Employees
                .Where(e => e.Id == w.IssuedById)
                .Select(e => e.Name)
                .FirstOrDefault() ?? "الإدارة"
        })
        .ToListAsync();

    // ✅ العقوبات
    var punishments = await db.Punishments
        .Where(p => p.StudentId == StudentId)
        .OrderByDescending(p => p.CreatedAt)
        .Select(p => new
        {
            Type = p.Type.ToString(),
            p.Reason,
            p.CreatedAt,
            IssuedBy = db.Employees
                .Where(e => e.Id == p.IssuedById)
                .Select(e => e.Name)
                .FirstOrDefault() ?? "الإدارة"
        })
        .ToListAsync();

    // ✅ استدعاءات ولي الأمر
    var summons = await db.GuardianSummons
        .Where(s => s.StudentId == StudentId)
        .OrderByDescending(s => s.CreatedAt)
        .Select(s => new
        {
            s.Reason,
            s.Date,
            s.CreatedAt
        })
        .ToListAsync();

    // ✅ الشكاوى
    var complaints = await db.Complaints
        .Where(c => c.FromUserId == StudentId && c.FromUserType == UserType.Student)
        .OrderByDescending(c => c.CreatedAt)
        .Select(c => new
        {
            c.Against,
            c.Content,
            Status = c.Status.ToString(),
            c.Resolution,
            c.CreatedAt
        })
        .ToListAsync();

    // ✅ الإشعارات
    var notifications = await db.Notifications
        .Where(n => n.UserId == StudentId && n.UserType == UserType.Student)
        .OrderByDescending(n => n.CreatedAt)
        .Take(100)
        .Select(n => new
        {
            n.Title,
            n.Body,
            n.Type,
            n.IsRead,
            n.CreatedAt
        })
        .ToListAsync();

    // ✅ الإحصائيات
    var statistics = new
    {
        TotalSubjects = subjects.Count,
        TotalMarks = marks.Count,
        TotalReportCards = reportCards.Count,
        TotalAttendance = attendance.Count,
        TotalActivities = activities.Count,
        TotalWarnings = warnings.Count,
        TotalPunishments = punishments.Count,
        TotalComplaints = complaints.Count,
        TotalNotifications = notifications.Count,
    };

    return Ok(new
    {
        Student = studentInfo,
        Statistics = statistics,
        Subjects = subjects,
        Marks = marks,
        ReportCards = reportCards,
        Attendance = attendance,        
        ScheduleImage = scheduleImage,
        Library = new
        {
            Member = member,
        },
        Activities = activities,
        Warnings = warnings,
        Punishments = punishments,
        Summons = summons,
        Complaints = complaints,
        Notifications = notifications
    });
}
}