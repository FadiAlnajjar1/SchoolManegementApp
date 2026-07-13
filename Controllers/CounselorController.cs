using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Auth;
using SchoolManagement.Api.Data;
using SchoolManagement.Api.Dtos;
using SchoolManagement.Api.Models;
using SchoolManagement.Api.Services;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/counselor")]
[Authorize(Roles = Roles.Counselor)]
public class CounselorController(
    AppDbContext db,
    NotificationService notifier) : ControllerBase
{
    private int CounselorId => User.GetUserId();
    private int SchoolId => User.GetSchoolId();

    // جلب شعب الموجه
    private IQueryable<Section> MySections() =>
        db.Sections.Where(s => s.CounselorId == CounselorId);

    // جلب معرفات الشعب التي يشرف عليها الموجه
    private async Task<List<int>> GetMySectionIdsAsync() =>
        await MySections().Select(s => s.Id).ToListAsync();

    // ============================================
    // 1. جلب الشعب التابعة للموجه
    // ============================================

    [HttpGet("sections")]
    public async Task<IActionResult> GetSections()
    {
        var sections = await MySections()
            .Include(s => s.Grade)
            .OrderBy(s => s.Grade!.LocalGradeNumber)
            .ThenBy(s => s.LocalSectionNumber)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.LocalSectionNumber,
                s.GradeId,
                GradeName = s.Grade != null ? s.Grade.Name : null,
                LocalGradeNumber = s.Grade != null ? s.Grade.LocalGradeNumber : (int?)null,
                s.CreatedAt
            })
            .ToListAsync();

        var result = sections
            .GroupBy(s => new { s.GradeId, s.GradeName, s.LocalGradeNumber })
            .Select(g => new
            {
                GradeId = g.Key.GradeId,
                GradeName = g.Key.GradeName,
                LocalGradeNumber = g.Key.LocalGradeNumber,
                Sections = g.Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.LocalSectionNumber,
                    s.CreatedAt
                }).OrderBy(s => s.LocalSectionNumber).ToList()
            })
            .OrderBy(g => g.LocalGradeNumber)
            .ToList();

        return Ok(result);
    }

    // ============================================
    // 2. حضور الطلاب
    // ============================================

    // ============================================
// حضور الطلاب - باستخدام Local IDs
// ============================================
[HttpGet("attendance")]
public async Task<IActionResult> GetAttendance(
    [FromQuery] int localGradeNumber,
    [FromQuery] int localSectionNumber, 
    [FromQuery] DateOnly? date)
{
    // ✅ البحث عن الشعبة باستخدام Local IDs
    var section = await db.Sections
        .Include(s => s.Grade)
        .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                  s.Grade != null &&
                                  s.Grade.LocalGradeNumber == localGradeNumber &&
                                  s.LocalSectionNumber == localSectionNumber);

    if (section is null)
        return BadRequest(new { message = $"لا توجد شعبة برقم {localSectionNumber} في الصف {localGradeNumber}" });

    // ✅ التحقق من أن الشعبة تابعة للموجه
    if (section.CounselorId != CounselorId)
        return BadRequest(new { message = "هذه الشعبة ليست من شعبك" });

    var sectionId = section.Id;

    // ✅ جلب الحضور
    var query = db.StudentAttendances.Where(a => a.SectionId == sectionId);
    if (date is not null) 
        query = query.Where(a => a.Date == date);

    var attendance = await query
        .OrderByDescending(a => a.Date)
        .Take(500)
        .Select(a => new
        {
            a.Id,
            a.StudentId,
            StudentName = a.Student != null ? a.Student.Name : null,
            StudentLocalNumber = a.Student != null ? a.Student.LocalStudentNumber : 0,
            a.SectionId,
            LocalSectionNumber = section.LocalSectionNumber,
            LocalGradeNumber = section.Grade != null ? section.Grade.LocalGradeNumber : 0,
            a.Date,
            a.Status,
            StatusName = a.Status.ToString(),
            a.TakenById
        })
        .ToListAsync();

    return Ok(new
    {
        success = true,
        message = "تم جلب بيانات الحضور بنجاح",
        data = new
        {
            section = new
            {
                section.Id,
                section.Name,
                LocalSectionNumber = section.LocalSectionNumber,
                GradeName = section.Grade != null ? section.Grade.Name : null,
                LocalGradeNumber = section.Grade != null ? section.Grade.LocalGradeNumber : 0
            },
            attendance = attendance,
            totalRecords = attendance.Count
        }
    });
}
[HttpPost("attendance")]
public async Task<IActionResult> TakeAttendance(StudentAttendanceLocalRequest request)
{
    // ✅ البحث عن الشعبة باستخدام Local IDs
    var section = await db.Sections
        .Include(s => s.Grade)
        .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                  s.Grade != null &&
                                  s.Grade.LocalGradeNumber == request.LocalGradeNumber &&
                                  s.LocalSectionNumber == request.LocalSectionNumber);

    if (section is null)
        return BadRequest(new { message = $"لا توجد شعبة برقم {request.LocalSectionNumber} في الصف {request.LocalGradeNumber}" });

    if (section.CounselorId != CounselorId)
        return BadRequest(new { message = "هذه الشعبة ليست من شعبك" });

    // ✅ تحويل LocalStudentNumber إلى StudentId
    var entries = new List<StudentAttendanceEntry>();
    foreach (var entry in request.Entries)
    {
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalStudentNumber == entry.LocalStudentNumber &&
                                      s.SectionId == section.Id);

        if (student is null)
            return BadRequest(new { message = $"لا يوجد طالب برقم {entry.LocalStudentNumber} في هذه الشعبة" });

        entries.Add(new StudentAttendanceEntry
        {
            StudentId = student.Id,
            Status = entry.Status,
            Justification = entry.Justification
        });
    }

    // ✅ إنشاء الـ Request
    var attendanceRequest = new StudentAttendanceRequest
    {
        SectionId = section.Id,
        Date = DateOnly.FromDateTime(DateTime.Today),
        Entries = entries
    };

    // ✅ استدعاء AttendanceHelper
    return await AttendanceHelper.RecordAsync(db, attendanceRequest, CounselorId, this);
}

    // ============================================
    // 3. التحذيرات
    // ============================================

    // ============================================
// التحذيرات - باستخدام Local IDs
// ============================================

[HttpPost("warnings")]
public async Task<IActionResult> CreateWarning(WarningLocalRequest request)
{
    // ✅ البحث عن الطالب باستخدام LocalStudentNumber
    var student = await db.Students
        .Include(s => s.Section)
            .ThenInclude(sec => sec!.Grade)
        .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                  s.LocalStudentNumber == request.LocalStudentNumber);

    if (student is null)
        return BadRequest(new { message = $"لا يوجد طالب برقم {request.LocalStudentNumber} في هذه المدرسة" });

    // ✅ التحقق من أن الطالب في شعبة تابعة للموجه
    if (student.SectionId is null)
        return BadRequest(new { message = "الطالب ليس في أي شعبة" });

    var section = await db.Sections
        .FirstOrDefaultAsync(s => s.Id == student.SectionId && s.CounselorId == CounselorId);

    if (section is null)
        return BadRequest(new { message = "الطالب ليس في شعبك" });

    var warning = new Warning
    {
        StudentId = student.Id,
        Type = request.Type,
        Reason = request.Reason,
        IssuedById = CounselorId,
        CreatedAt = DateTime.UtcNow
    };

    db.Warnings.Add(warning);
    await db.SaveChangesAsync();

    // ✅ إرسال إشعارات
    await notifier.SendAsync(student.Id, UserType.Student, "تحذير", request.Reason, "warning");
    await notifier.SendToGuardianAsync(student, "تحذير لابنكم", $"{student.Name}: {request.Reason}", "warning");

    return Created($"api/counselor/warnings/{warning.Id}", new
    {
        warning.Id,
        StudentLocalNumber = student.LocalStudentNumber,
        StudentName = student.Name,
        SectionName = section.Name,
        LocalSectionNumber = section.LocalSectionNumber,
        GradeName = section.Grade != null ? section.Grade.Name : null,
        LocalGradeNumber = section.Grade != null ? section.Grade.LocalGradeNumber : 0,
        warning.Type,
        warning.Reason,
        warning.CreatedAt
    });
}

[HttpGet("warnings")]
public async Task<IActionResult> GetWarnings(
    [FromQuery] int? localGradeNumber,
    [FromQuery] int? localSectionNumber,
    [FromQuery] int? localStudentNumber)
{
    // ✅ جلب جميع الطلاب في شعب الموجه
    var studentIds = await db.Students
        .Where(s => s.SchoolId == SchoolId && 
                    s.SectionId != null &&
                    db.Sections.Any(x => x.Id == s.SectionId && x.CounselorId == CounselorId))
        .Select(s => s.Id)
        .ToListAsync();

    if (!studentIds.Any())
        return Ok(new
        {
            success = true,
            message = "لا توجد تحذيرات",
            data = new List<object>()
        });

    var query = db.Warnings
        .Include(w => w.Student)
            .ThenInclude(s => s!.Section)
                .ThenInclude(sec => sec!.Grade)
        .Where(w => studentIds.Contains(w.StudentId));

    // ✅ فلترة حسب الصف (LocalGradeNumber)
    if (localGradeNumber.HasValue)
    {
        query = query.Where(w => w.Student != null && 
                                 w.Student.Section != null &&
                                 w.Student.Section.Grade != null &&
                                 w.Student.Section.Grade.LocalGradeNumber == localGradeNumber.Value);
    }

    // ✅ فلترة حسب الشعبة (LocalSectionNumber)
    if (localSectionNumber.HasValue)
    {
        query = query.Where(w => w.Student != null && 
                                 w.Student.Section != null &&
                                 w.Student.Section.LocalSectionNumber == localSectionNumber.Value);
    }

    // ✅ فلترة حسب الطالب (LocalStudentNumber)
    if (localStudentNumber.HasValue)
    {
        query = query.Where(w => w.Student != null && 
                                 w.Student.LocalStudentNumber == localStudentNumber.Value);
    }

    var warnings = await query
        .OrderByDescending(w => w.CreatedAt)
        .Select(w => new
        {
            w.Id,
            StudentLocalNumber = w.Student != null ? w.Student.LocalStudentNumber : 0,
            StudentName = w.Student != null ? w.Student.Name : null,
            SectionName = w.Student != null && w.Student.Section != null ? w.Student.Section.Name : null,
            LocalSectionNumber = w.Student != null && w.Student.Section != null ? w.Student.Section.LocalSectionNumber : 0,
            GradeName = w.Student != null && w.Student.Section != null && w.Student.Section.Grade != null ? 
                w.Student.Section.Grade.Name : null,
            LocalGradeNumber = w.Student != null && w.Student.Section != null && w.Student.Section.Grade != null ? 
                w.Student.Section.Grade.LocalGradeNumber : 0,
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

    return Ok(new
    {
        success = true,
        message = "تم جلب التحذيرات بنجاح",
        data = new
        {
            totalWarnings = warnings.Count,
            filters = new
            {
                localGradeNumber,
                localSectionNumber,
                localStudentNumber
            },
            warnings = warnings
        }
    });
}

// ============================================
// جلب تحذيرات طالب معين (باستخدام LocalStudentNumber)
// ============================================

[HttpGet("students/{localStudentNumber:int}/warnings")]
public async Task<IActionResult> GetStudentWarnings(int localStudentNumber)
{
    // ✅ البحث عن الطالب
    var student = await db.Students
        .Include(s => s.Section)
            .ThenInclude(sec => sec!.Grade)
        .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                  s.LocalStudentNumber == localStudentNumber);

    if (student is null)
        return NotFound(new { message = $"لا يوجد طالب برقم {localStudentNumber} في هذه المدرسة" });

    // ✅ التحقق من أن الطالب في شعبة تابعة للموجه
    if (student.SectionId is null)
        return BadRequest(new { message = "الطالب ليس في أي شعبة" });

    var section = await db.Sections
        .FirstOrDefaultAsync(s => s.Id == student.SectionId && s.CounselorId == CounselorId);

    if (section is null)
        return BadRequest(new { message = "الطالب ليس في شعبك" });

    // ✅ جلب تحذيرات الطالب
    var warnings = await db.Warnings
        .Where(w => w.StudentId == student.Id)
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

    return Ok(new
    {
        success = true,
        message = "تم جلب تحذيرات الطالب بنجاح",
        data = new
        {
            student = new
            {
                student.Id,
                student.Name,
                student.LocalStudentNumber,
                SectionName = student.Section?.Name,
                LocalSectionNumber = student.Section?.LocalSectionNumber ?? 0,
                GradeName = student.Section?.Grade?.Name,
                LocalGradeNumber = student.Section?.Grade?.LocalGradeNumber ?? 0
            },
            warnings = warnings,
            totalWarnings = warnings.Count
        }
    });
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
    // 4. الملف الكامل للطالب
    // ============================================

    // ============================================
// الملف الكامل للطالب - باستخدام Local IDs
// ============================================

[HttpGet("students/{localStudentNumber:int}/full-profile")]
public async Task<IActionResult> GetStudentFullProfile(int localStudentNumber)
{
    // ✅ البحث عن الطالب باستخدام LocalStudentNumber
    var student = await db.Students
        .Include(s => s.Section)
            .ThenInclude(sec => sec!.Grade)
        .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                  s.LocalStudentNumber == localStudentNumber);

    if (student is null)
        return NotFound(new { message = $"لا يوجد طالب برقم {localStudentNumber} في هذه المدرسة" });

    if (student.SectionId is null)
        return BadRequest(new { message = "الطالب ليس في أي شعبة" });

    var section = await db.Sections
        .FirstOrDefaultAsync(s => s.Id == student.SectionId && s.CounselorId == CounselorId);

    if (section is null)
        return BadRequest(new { message = "الطالب ليس في شعبك" });

    // ✅ معلومات الطالب الأساسية
    var basic = new
    {
        student.Id,
        student.Name,
        student.Email,
        LocalStudentNumber = student.LocalStudentNumber,
        student.SchoolId,
        student.SectionId,
        SectionName = student.Section?.Name,
        LocalSectionNumber = student.Section?.LocalSectionNumber ?? 0,
        GradeName = student.Section?.Grade?.Name,
        LocalGradeNumber = student.Section?.Grade?.LocalGradeNumber ?? 0,
        student.GuardianName,
        student.GuardianPhone,
        student.BloodType,
        student.ChronicDiseases,
        student.Allergies,
        student.HealthNotes,
        student.BirthDate,
        student.Address,
        student.DismissalWarning,
        student.CreatedAt
    };

    // ✅ معلومات الشعبة والصف
    var sectionInfo = new
    {
        section.Id,
        section.Name,
        LocalSectionNumber = section.LocalSectionNumber,
        GradeName = section.Grade?.Name ?? "",
        LocalGradeNumber = section.Grade?.LocalGradeNumber ?? 0,
        AcademicYear = section.Grade?.AcademicYear ?? 0
    };

    // ✅ المواد الدراسية
    var subjects = new List<object>();
    if (section is not null)
    {
        var subjectList = await db.TeacherGrades
            .Where(tg => tg.SectionId == section.Id)
            .Select(tg => new
            {
                tg.SubjectId,
                LocalSubjectId = db.Subjects
                    .Where(s => s.Id == tg.SubjectId)
                    .Select(s => s.LocalSubjectId)
                    .FirstOrDefault(),
                SubjectName = db.Subjects
                    .Where(s => s.Id == tg.SubjectId)
                    .Select(s => s.Name)
                    .FirstOrDefault(),
                tg.TeacherId,
                TeacherName = db.Employees
                    .Where(e => e.Id == tg.TeacherId)
                    .Select(e => e.Name)
                    .FirstOrDefault(),
                LocalTeacherNumber = db.EmployeeSchools
                    .Where(es => es.EmployeeId == tg.TeacherId && 
                                 es.SchoolId == SchoolId && 
                                 es.IsActive)
                    .Select(es => (int?)es.LocalEmployeeNumber)
                    .FirstOrDefault()
            })
            .Distinct()
            .ToListAsync();
        
        subjects.AddRange(subjectList);
    }

    // ✅ العلامات
    var marks = await db.Marks
        .Where(m => m.StudentId == student.Id)
        .OrderByDescending(m => m.Semester)
        .Select(m => new
        {
            m.SubjectId,
            LocalSubjectId = db.Subjects
                .Where(s => s.Id == m.SubjectId)
                .Select(s => s.LocalSubjectId)
                .FirstOrDefault(),
            SubjectName = db.Subjects
                .Where(s => s.Id == m.SubjectId)
                .Select(s => s.Name)
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
        .Where(r => r.StudentId == student.Id)
        .OrderByDescending(r => r.Year)
        .ThenByDescending(r => r.Semester)
        .Select(r => new
        {
            r.Id,
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
                s.Total
            }).ToList()
        })
        .ToListAsync();

    // ✅ الحضور
    var attendance = await db.StudentAttendances
        .Where(a => a.StudentId == student.Id)
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

    // ✅ المكتبة
    var member = await db.LibraryMembers
        .Where(m => m.StudentId == student.Id)
        .Select(m => new
        {
            m.Id,
            LocalMemberNumber = m.LocalMemberNumber,
            Status = m.Status.ToString(),
            m.CreatedAt
        })
        .FirstOrDefaultAsync();

    var memberId = member?.Id ?? 0;

    // var loans = memberId > 0 ? await db.BookLoans
    //     .Where(l => l.MemberId == memberId)
    //     .OrderByDescending(l => l.LoanDate)
    //     .Select(l => new
    //     {
    //         l.Id,
    //         LocalLoanNumber = l.LocalLoanNumber,
    //         BookTitle = l.Book != null ? l.Book.Title : null,
    //         LocalBookNumber = l.Book != null ? l.Book.LocalBookNumber : 0,
    //         l.LoanDate,
    //         l.DueDate,
    //         l.ReturnDate,
    //         Status = l.Status.ToString()
    //     })
    //     .ToListAsync() : new List<object>();

    // var reservations = memberId > 0 ? await db.BookReservations
    //     .Where(r => r.MemberId == memberId)
    //     .OrderByDescending(r => r.Date)
    //     .Select(r => new
    //     {
    //         r.Id,
    //         BookTitle = r.Book != null ? r.Book.Title : null,
    //         LocalBookNumber = r.Book != null ? r.Book.LocalBookNumber : 0,
    //         r.Date,
    //         Status = r.Status.ToString()
    //     })
    //     .ToListAsync() : new List<object>();

    // ✅ الأنشطة
    var activities = await db.ActivityRegistrations
        .Where(r => r.StudentId == student.Id)
        .Select(r => new
        {
            r.Id,
            r.ActivityId,
            LocalActivityId = r.Activity != null ? r.Activity.LocalActivityId : 0,
            ActivityName = r.Activity != null ? r.Activity.Name : null,
            ActivityType = r.Activity != null ? r.Activity.Type.ToString() : null,
            ActivitySchedule = r.Activity != null ? r.Activity.Schedule : null,
            Status = r.Status.ToString(),
            r.CreatedAt
        })
        .ToListAsync();

    // ✅ التحذيرات
    var warnings = await db.Warnings
        .Where(w => w.StudentId == student.Id)
        .OrderByDescending(w => w.CreatedAt)
        .Select(w => new
        {
            w.Id,
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
        .Where(p => p.StudentId == student.Id)
        .OrderByDescending(p => p.CreatedAt)
        .Select(p => new
        {
            p.Id,
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
        .Where(s => s.StudentId == student.Id)
        .OrderByDescending(s => s.CreatedAt)
        .Select(s => new
        {
            s.Id,
            s.Reason,
            s.Date,
            s.CreatedAt
        })
        .ToListAsync();

    // ✅ الشكاوى
    var complaints = await db.Complaints
        .Where(c => c.FromUserId == student.Id && c.FromUserType == UserType.Student)
        .OrderByDescending(c => c.CreatedAt)
        .Select(c => new
        {
            c.Id,
            c.Against,
            c.Content,
            Status = c.Status.ToString(),
            c.Resolution,
            c.CreatedAt
        })
        .ToListAsync();

    // ✅ الإشعارات
    var notifications = await db.Notifications
        .Where(n => n.UserId == student.Id && n.UserType == UserType.Student)
        .OrderByDescending(n => n.CreatedAt)
        .Take(100)
        .Select(n => new
        {
            n.Id,
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

    // ✅ الرد النهائي
    return Ok(new
    {
        success = true,
        message = "تم جلب الملف الكامل للطالب بنجاح",
        data = new
        {
            Student = basic,
            Section = sectionInfo,
            Statistics = statistics,
            Subjects = subjects,
            Marks = marks,
            ReportCards = reportCards,
            Attendance = attendance,
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
        }
    });
}
    // ============================================
    // 5. صورة جدول الشعبة - رفع (للموجه فقط)
    // ============================================

    [HttpPost("schedule-images/section")]
public async Task<IActionResult> UploadSectionScheduleImage([FromForm] ScheduleImageRequest request)
{
    var school = await db.Schools.FindAsync(SchoolId);
    if (school is null)
        return BadRequest(new { message = "المدرسة غير موجودة" });

    // ✅ البحث عن الصف باستخدام LocalGradeNumber
    var grade = await db.Grades
        .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                  g.LocalGradeNumber == request.LocalGradeNumber);
    if (grade is null)
        return BadRequest(new { message = $"لا يوجد صف برقم {request.LocalGradeNumber} في هذه المدرسة" });

    // ✅ البحث عن الشعبة باستخدام LocalSectionNumber
    var section = await db.Sections
        .FirstOrDefaultAsync(s => s.GradeId == grade.Id && 
                                  s.LocalSectionNumber == request.LocalSectionNumber &&
                                  s.SchoolId == SchoolId);
    
    if (section is null)
        return BadRequest(new { message = $"لا توجد شعبة برقم {request.LocalSectionNumber} في الصف {request.LocalGradeNumber}" });

    // ✅ التحقق من أن المستخدم هو الموجه المشرف على الشعبة
    if (section.CounselorId != CounselorId)
        return BadRequest(new { message = "أنت غير مشرف على هذه الشعبة" });

    var imageUrl = await SaveScheduleImageAsync(request.Image);

    var existingImage = await db.ScheduleImages
        .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                  s.SectionId == section.Id && 
                                  s.Type == ScheduleImageType.Section);

    if (existingImage is not null)
    {
        DeleteScheduleImageFile(existingImage.ImageUrl);
        db.ScheduleImages.Remove(existingImage);
        await db.SaveChangesAsync();
    }

    var scheduleImage = new ScheduleImage
    {
        SchoolId = SchoolId,
        GradeId = grade.Id,
        SectionId = section.Id,
        TeacherId = null,
        ImageUrl = imageUrl,
        Description = request.Description ?? $"جدول الشعبة {section.Name} - {grade.Name}",
        Type = ScheduleImageType.Section,
        CreatedAt = DateTime.UtcNow
    };

    db.ScheduleImages.Add(scheduleImage);
    await db.SaveChangesAsync();

    return Created($"api/counselor/schedule-images/section/{scheduleImage.Id}", new
    {
        message = "تم رفع صورة جدول الشعبة بنجاح",
        scheduleImage = new
        {
            scheduleImage.Id,
            scheduleImage.ImageUrl,
            scheduleImage.Description,
            LocalGradeNumber = grade.LocalGradeNumber,
            GradeName = grade.Name,
            LocalSectionNumber = section.LocalSectionNumber,
            SectionName = section.Name,
            scheduleImage.CreatedAt
        }
    });
}

    // ============================================
    // 6. صورة جدول الشعبة - جلب (للموجه فقط)
    // ============================================

   // ============================================
// جلب صورة جدول الشعبة (للمعلمين والطلاب)
// ============================================

[HttpGet("schedule-images/section/{localGradeNumber:int}/{localSectionNumber:int}")]
public async Task<IActionResult> GetSectionScheduleImage(int localGradeNumber, int localSectionNumber)
{
    var grade = await db.Grades
        .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                  g.LocalGradeNumber == localGradeNumber);
    if (grade is null)
        return NotFound(new { message = $"لا يوجد صف برقم {localGradeNumber} في هذه المدرسة" });

    var section = await db.Sections
        .FirstOrDefaultAsync(s => s.GradeId == grade.Id && 
                                  s.LocalSectionNumber == localSectionNumber &&
                                  s.SchoolId == SchoolId);
    
    if (section is null)
        return NotFound(new { message = $"لا توجد شعبة برقم {localSectionNumber} في الصف {localGradeNumber}" });

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
            LocalGradeNumber = grade.LocalGradeNumber,
            GradeName = grade.Name,
            LocalSectionNumber = section.LocalSectionNumber,
            SectionName = section.Name
        })
        .FirstOrDefaultAsync();

    if (image is null)
        return NotFound(new { message = "لا توجد صورة جدول لهذه الشعبة" });

    return Ok(image);
}

    // ============================================
    // 7. صورة جدول الشعبة - حذف (للموجه فقط)
    // ============================================

    [HttpDelete("schedule-images/section/{localGradeNumber:int}/{localSectionNumber:int}")]
public async Task<IActionResult> DeleteSectionScheduleImage(int localGradeNumber, int localSectionNumber)
{
    // ✅ البحث عن الصف باستخدام LocalGradeNumber
    var grade = await db.Grades
        .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                  g.LocalGradeNumber == localGradeNumber);

    if (grade is null)
        return NotFound(new { message = $"لا يوجد صف برقم {localGradeNumber} في هذه المدرسة" });

    // ✅ البحث عن الشعبة باستخدام LocalSectionNumber
    var section = await db.Sections
        .FirstOrDefaultAsync(s => s.GradeId == grade.Id && 
                                  s.LocalSectionNumber == localSectionNumber &&
                                  s.SchoolId == SchoolId);

    if (section is null)
        return NotFound(new { message = $"لا توجد شعبة برقم {localSectionNumber} في الصف {localGradeNumber}" });

    // ✅ التحقق من أن المستخدم هو الموجه المشرف على الشعبة
    if (section.CounselorId != CounselorId)
        return BadRequest(new { message = "أنت غير مشرف على هذه الشعبة" });

    // ✅ البحث عن الصورة
    var image = await db.ScheduleImages
        .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                  s.SectionId == section.Id && 
                                  s.Type == ScheduleImageType.Section);

    if (image is null)
        return NotFound(new { message = "لا توجد صورة جدول لهذه الشعبة" });

    // ✅ حذف الصورة
    DeleteScheduleImageFile(image.ImageUrl);
    db.ScheduleImages.Remove(image);
    await db.SaveChangesAsync();

    return Ok(new
    {
        success = true,
        message = "تم حذف صورة جدول الشعبة بنجاح",
        data = new
        {
            LocalGradeNumber = grade.LocalGradeNumber,
            GradeName = grade.Name,
            LocalSectionNumber = section.LocalSectionNumber,
            SectionName = section.Name
        }
    });
}

    // ============================================
    // 8. طلاب شعبة معينة (Pagination)
    // ============================================

    [HttpGet("sections/{localGradeNumber:int}/{localSectionNumber:int}/students")]
public async Task<IActionResult> GetSectionStudents(
    int localGradeNumber,
    int localSectionNumber)
{
    // ✅ البحث عن الصف باستخدام LocalGradeNumber
    var grade = await db.Grades
        .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                  g.LocalGradeNumber == localGradeNumber);

    if (grade is null)
        return NotFound(new { message = $"لا يوجد صف برقم {localGradeNumber} في هذه المدرسة" });

    // ✅ البحث عن الشعبة باستخدام LocalSectionNumber
    var section = await db.Sections
        .FirstOrDefaultAsync(s => s.GradeId == grade.Id && 
                                  s.LocalSectionNumber == localSectionNumber &&
                                  s.SchoolId == SchoolId);

    if (section is null)
        return NotFound(new { message = $"لا توجد شعبة برقم {localSectionNumber} في الصف {localGradeNumber}" });

    // ✅ التحقق من أن المستخدم هو الموجه المشرف على الشعبة
    if (section.CounselorId != CounselorId)
        return BadRequest(new { message = "هذه الشعبة ليست من شعبك" });

    // ✅ جلب جميع الطلاب في الشعبة (بدون Pagination)
    var students = await db.Students
        .Where(s => s.SectionId == section.Id && s.IsActive)
        .OrderBy(s => s.LocalStudentNumber)
        .Select(s => new
        {
            s.Id,
            s.Name,
            s.Email,
            LocalStudentNumber = s.LocalStudentNumber,
            s.BloodType,
            s.GuardianName,
            s.GuardianPhone,
            s.BirthDate,
            s.Address,
            s.DismissalWarning,
            s.CreatedAt
        })
        .ToListAsync();

    return Ok(new
    {
        success = true,
        message = "تم جلب طلاب الشعبة بنجاح",
        data = new
        {
            Section = new
            {
                section.Id,
                section.Name,
                LocalSectionNumber = section.LocalSectionNumber,
                GradeName = grade.Name,
                LocalGradeNumber = grade.LocalGradeNumber
            },
            Students = students,
            TotalStudents = students.Count
        }
    });
}

    

    private async Task<Student?> StudentInMySectionsAsync(int studentId) =>
        await db.Students
            .Include(s => s.Section)
            .FirstOrDefaultAsync(s =>
                s.Id == studentId && s.SectionId != null &&
                db.Sections.Any(x => x.Id == s.SectionId && x.CounselorId == CounselorId));

    private async Task<string> SaveScheduleImageAsync(IFormFile image)
    {
        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "schedules");
        
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid()}_{image.FileName}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await image.CopyToAsync(fileStream);
        }

        return $"/uploads/schedules/{uniqueFileName}";
    }

    private void DeleteScheduleImageFile(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return;

        var fileName = Path.GetFileName(imageUrl);
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "schedules", fileName);

        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);
    }
}