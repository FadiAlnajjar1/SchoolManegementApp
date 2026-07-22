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
// جلب الغيابات - مع فلترة حسب الشهر والسنة
// ============================================

// ============================================
// جلب الغيابات - باستخدام Local IDs
// ============================================

[HttpGet("absences")]
public async Task<IActionResult> GetAbsences(
    [FromQuery] int? localStudentNumber = null,
    [FromQuery] int? month = null,
    [FromQuery] int? year = null,
    [FromQuery] int? localGradeNumber = null,
    [FromQuery] int? localSectionNumber = null)
{
    // ✅ جلب جميع الشعب التابعة للموجه
    var sectionIds = await MySections().Select(s => s.Id).ToListAsync();

    if (!sectionIds.Any())
        return Ok(new
        {
            success = true,
            message = "لا توجد شعب تحت إشرافك",
            data = new List<object>()
        });

    // ✅ جلب سجلات الحضور للشعب التابعة للموجه
    var query = db.StudentAttendances
        .Include(a => a.Student)
            .ThenInclude(s => s!.Section)
                .ThenInclude(sec => sec!.Grade)
        .Where(a => sectionIds.Contains(a.SectionId ?? 0) && !a.IsDeleted);

    // ✅ فلترة حسب الطالب (LocalStudentNumber)
    if (localStudentNumber.HasValue)
    {
        query = query.Where(a => a.Student != null &&
                                 a.Student.LocalStudentNumber == localStudentNumber.Value);
    }

    // ✅ فلترة حسب الصف (LocalGradeNumber)
    if (localGradeNumber.HasValue)
    {
        query = query.Where(a => a.Student != null &&
                                 a.Student.Section != null &&
                                 a.Student.Section.Grade != null &&
                                 a.Student.Section.Grade.LocalGradeNumber == localGradeNumber.Value);
    }

    // ✅ فلترة حسب الشعبة (LocalSectionNumber)
    if (localSectionNumber.HasValue)
    {
        query = query.Where(a => a.Student != null &&
                                 a.Student.Section != null &&
                                 a.Student.Section.LocalSectionNumber == localSectionNumber.Value);
    }

    // ✅ فلترة حسب الشهر والسنة
    if (month.HasValue && year.HasValue)
    {
        var startDate = new DateOnly(year.Value, month.Value, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);
        query = query.Where(a => a.Date >= startDate && a.Date <= endDate);
    }
    else if (month.HasValue)
    {
        var currentYear = DateTime.Now.Year;
        var startDate = new DateOnly(currentYear, month.Value, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);
        query = query.Where(a => a.Date >= startDate && a.Date <= endDate);
    }
    else if (year.HasValue)
    {
        var startDate = new DateOnly(year.Value, 1, 1);
        var endDate = new DateOnly(year.Value, 12, 31);
        query = query.Where(a => a.Date >= startDate && a.Date <= endDate);
    }

    // ✅ جلب الغيابات فقط
    var absences = await query
        .Where(a => a.Status == AttendanceStatus.Absent || a.Status == AttendanceStatus.Justified)
        .OrderByDescending(a => a.Date)
        .Select(a => new
        {
            a.Id,
            StudentLocalNumber = a.Student != null ? a.Student.LocalStudentNumber : 0,
            StudentName = a.Student != null ? a.Student.Name : null,
            LocalGradeNumber = a.Student != null && a.Student.Section != null && a.Student.Section.Grade != null ? 
                a.Student.Section.Grade.LocalGradeNumber : 0,
            GradeName = a.Student != null && a.Student.Section != null && a.Student.Section.Grade != null ? 
                a.Student.Section.Grade.Name : null,
            LocalSectionNumber = a.Student != null && a.Student.Section != null ? 
                a.Student.Section.LocalSectionNumber : 0,
            SectionName = a.Student != null && a.Student.Section != null ? 
                a.Student.Section.Name : null,
            a.Date,
            a.Status,
            StatusName = a.Status.ToString(),
            a.Justification,
            a.TakenById,
            TakenByName = db.Employees
                .Where(e => e.Id == a.TakenById)
                .Select(e => e.Name)
                .FirstOrDefault() ?? "الإدارة",
            a.CreatedAt,
            a.UpdatedAt,
            DayOfWeek = a.Date.DayOfWeek.ToString(),
            DayNumber = a.Date.Day
        })
        .ToListAsync();

    // ✅ إحصائيات
    var totalAbsences = absences.Count;
    var unexcusedAbsences = absences.Count(a => a.Status == AttendanceStatus.Absent);
    var justifiedAbsences = absences.Count(a => a.Status == AttendanceStatus.Justified);

    // ✅ تجميع حسب اليوم
    var absencesByDay = absences
        .GroupBy(a => a.Date)
        .Select(g => new
        {
            Date = g.Key,
            Day = g.Key.Day,
            DayOfWeek = g.Key.DayOfWeek.ToString(),
            Total = g.Count(),
            Unexcused = g.Count(a => a.Status == AttendanceStatus.Absent),
            Justified = g.Count(a => a.Status == AttendanceStatus.Justified)
        })
        .OrderBy(g => g.Date)
        .ToList();

    // ✅ تجميع حسب الطالب
    var absencesByStudent = absences
        .GroupBy(a => a.StudentLocalNumber)
        .Select(g => new
        {
            StudentLocalNumber = g.Key,
            StudentName = g.First().StudentName,
            Total = g.Count(),
            Unexcused = g.Count(a => a.Status == AttendanceStatus.Absent),
            Justified = g.Count(a => a.Status == AttendanceStatus.Justified)
        })
        .OrderByDescending(g => g.Total)
        .ToList();

    return Ok(new
    {
        success = true,
        message = "تم جلب سجلات الغيابات بنجاح",
        data = new
        {
            Filters = new
            {
                LocalStudentNumber = localStudentNumber,
                Month = month,
                Year = year,
                LocalGradeNumber = localGradeNumber,
                LocalSectionNumber = localSectionNumber
            },
            Statistics = new
            {
                TotalAbsences = totalAbsences,
                UnexcusedAbsences = unexcusedAbsences,
                JustifiedAbsences = justifiedAbsences,
                AbsenceRate = totalAbsences > 0 ? 
                    Math.Round((double)unexcusedAbsences / totalAbsences * 100, 2) : 0
            },
            AbsencesByDay = absencesByDay,
            AbsencesByStudent = absencesByStudent,
            Absences = absences
        }
    });
}
// ============================================
// تعديل سجل غياب - باستخدام Local IDs
// ============================================

[HttpPut("absences/{localStudentNumber}")]
public async Task<IActionResult> UpdateAbsence(
    [FromRoute] int localStudentNumber,
    [FromBody] UpdateAbsenceLocalRequest request)
{
    // ✅ البحث عن الطالب
    var student = await db.Students
        .Include(s => s.Section)
            .ThenInclude(sec => sec!.Grade)
        .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                  s.LocalStudentNumber == localStudentNumber);
    
    if (student is null)
        return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber}" });

    // ✅ التحقق من أن الطالب في شعبة
    if (student.SectionId is null)
        return BadRequest(new { success = false, message = "الطالب ليس في أي شعبة" });

    // ✅ التحقق من أن الشعبة تابعة للموجه
    var section = await db.Sections
        .FirstOrDefaultAsync(s => s.Id == student.SectionId && s.CounselorId == CounselorId);

    if (section is null)
        return BadRequest(new { success = false, message = "الطالب ليس في شعبك" });

    // ✅ تحديد التاريخ
    var date = request.Date ?? DateOnly.FromDateTime(DateTime.Today);

    // ✅ البحث عن سجل الغياب
    var attendance = await db.StudentAttendances
        .Include(a => a.Student)
        .FirstOrDefaultAsync(a => a.StudentId == student.Id &&
                                  a.SectionId == student.SectionId &&
                                  a.Date == date &&
                                  !a.IsDeleted);

    if (attendance is null)
        return NotFound(new { 
            success = false, 
            message = $"لا يوجد سجل غياب للطالب {localStudentNumber} في تاريخ {date}" 
        });

    // ✅ التحقق من أن السجل هو غياب
    if (attendance.Status != AttendanceStatus.Absent)
        return BadRequest(new { success = false, message = "هذا السجل ليس غياباً" });

    // ✅ تحديث البيانات (فقط السبب)
    if (!string.IsNullOrWhiteSpace(request.Justification))
        attendance.Justification = request.Justification;

    attendance.UpdatedAt = DateTime.UtcNow;
    attendance.TakenById = CounselorId;

    await db.SaveChangesAsync();

    return Ok(new
    {
        success = true,
        message = "تم تحديث سجل الغياب بنجاح",
        data = new
        {
            attendance.Id,
            StudentLocalNumber = student.LocalStudentNumber,
            StudentName = student.Name,
            LocalGradeNumber = student.Section?.Grade?.LocalGradeNumber ?? 0,
            GradeName = student.Section?.Grade?.Name,
            LocalSectionNumber = student.Section?.LocalSectionNumber ?? 0,
            SectionName = student.Section?.Name,
            attendance.Date,
            attendance.Status,
            StatusName = "غياب",
            attendance.Justification,
            attendance.UpdatedAt
        }
    });
}
// ============================================
// حذف سجل غياب - باستخدام Local IDs
// ============================================

[HttpDelete("absences/{localStudentNumber}")]
public async Task<IActionResult> DeleteAbsence(
    [FromRoute] int localStudentNumber,
    [FromQuery] DateOnly? date)  // ✅ التاريخ من الـ Query
{
    // ✅ البحث عن الطالب
    var student = await db.Students
        .Include(s => s.Section)
            .ThenInclude(sec => sec!.Grade)
        .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                  s.LocalStudentNumber == localStudentNumber);
    
    if (student is null)
        return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber}" });

    // ✅ التحقق من أن الطالب في شعبة
    if (student.SectionId is null)
        return BadRequest(new { success = false, message = "الطالب ليس في أي شعبة" });

    // ✅ التحقق من أن الشعبة تابعة للموجه
    var section = await db.Sections
        .FirstOrDefaultAsync(s => s.Id == student.SectionId && s.CounselorId == CounselorId);

    if (section is null)
        return BadRequest(new { success = false, message = "الطالب ليس في شعبك" });

    // ✅ تحديد التاريخ (إذا لم يتم إرساله، استخدم تاريخ اليوم)
    var attendanceDate = date ?? DateOnly.FromDateTime(DateTime.Today);

    // ✅ البحث عن سجل الغياب
    var attendance = await db.StudentAttendances
        .FirstOrDefaultAsync(a => a.StudentId == student.Id &&
                                  a.SectionId == student.SectionId &&
                                  a.Date == attendanceDate &&
                                  !a.IsDeleted);

    if (attendance is null)
        return NotFound(new { 
            success = false, 
            message = $"لا يوجد سجل غياب للطالب {localStudentNumber} في تاريخ {attendanceDate}" 
        });

    // ✅ التحقق من أن السجل هو غياب (وليس حضور أو تأخر)
    if (attendance.Status != AttendanceStatus.Absent)
        return BadRequest(new { success = false, message = "هذا السجل ليس غياباً" });

    // ✅ حذف السجل (حذف منطقي - Soft Delete)
    attendance.IsDeleted = true;
    attendance.UpdatedAt = DateTime.UtcNow;
    attendance.TakenById = CounselorId;

    await db.SaveChangesAsync();

    // ✅ إرسال إشعار لولي الأمر (اختياري)
    await notifier.SendToGuardianAsync(student,
        $"إلغاء تسجيل غياب للطالب {student.Name}",
        $"تم إلغاء تسجيل غياب الطالب {student.Name} في تاريخ {attendanceDate}",
        "absence_deleted");

    return Ok(new
    {
        success = true,
        message = $"تم حذف سجل الغياب للطالب {localStudentNumber} في تاريخ {attendanceDate} بنجاح",
        data = new
        {
            StudentLocalNumber = student.LocalStudentNumber,
            StudentName = student.Name,
            Date = attendanceDate,
            DeletedAt = DateTime.UtcNow
        }
    });
}
// ============================================
// تسجيل غياب للطالب - باستخدام Local IDs فقط
// ============================================

[HttpPost("attendance/absent/{localStudentNumber}")]
public async Task<IActionResult> RecordAbsence(
    [FromRoute] int localStudentNumber,
    [FromBody] RecordAbsenceLocalRequest request)
{
    // ✅ البحث عن الطالب
    var student = await db.Students
        .Include(s => s.Section)
            .ThenInclude(sec => sec!.Grade)
        .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                  s.LocalStudentNumber == localStudentNumber);
    
    if (student is null)
        return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber}" });

    // ✅ التحقق من أن الطالب في شعبة
    if (student.SectionId is null)
        return BadRequest(new { success = false, message = "الطالب ليس في أي شعبة" });

    // ✅ التحقق من أن الشعبة تابعة للموجه
    var section = await db.Sections
        .FirstOrDefaultAsync(s => s.Id == student.SectionId && s.CounselorId == CounselorId);

    if (section is null)
        return BadRequest(new { success = false, message = "الطالب ليس في شعبك" });

    // ✅ تحديد التاريخ
    var date = request.Date ?? DateOnly.FromDateTime(DateTime.Today);

    // ✅ التحقق من عدم وجود سجل حضور لهذا الطالب في هذا التاريخ
    var existingAttendance = await db.StudentAttendances
        .FirstOrDefaultAsync(a => a.StudentId == student.Id &&
                                  a.SectionId == student.SectionId &&
                                  a.Date == date);

    if (existingAttendance is not null)
        return BadRequest(new { 
            success = false, 
            message = $"يوجد بالفعل سجل حضور للطالب {localStudentNumber} في تاريخ {date}" 
        });

    // ✅ إنشاء سجل غياب جديد (مع تعيين Status = Absent بشكل افتراضي)
    var attendance = new StudentAttendance
    {
        StudentId = student.Id,
        SectionId = student.SectionId.Value,
        Date = date,
        Status = AttendanceStatus.Absent,  // ✅ دائماً غياب
        Justification = request.Justification ?? "",
        TakenById = CounselorId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsDeleted = false
    };

    db.StudentAttendances.Add(attendance);
    await db.SaveChangesAsync();

    // ✅ إرسال إشعار لولي الأمر
    await notifier.SendToGuardianAsync(student,
        $"تسجيل غياب للطالب {student.Name}",
        $"تم تسجيل غياب للطالب {student.Name} في تاريخ {date}" +
        (string.IsNullOrEmpty(request.Justification) ? "" : $" - السبب: {request.Justification}"),
        "absence");

    return Created($"api/counselor/attendance/absent/{localStudentNumber}/{attendance.Id}", new
    {
        success = true,
        message = "تم تسجيل الغياب بنجاح",
        data = new
        {
            attendance.Id,
            StudentLocalNumber = student.LocalStudentNumber,
            StudentName = student.Name,
            LocalGradeNumber = student.Section?.Grade?.LocalGradeNumber ?? 0,
            GradeName = student.Section?.Grade?.Name,
            LocalSectionNumber = student.Section?.LocalSectionNumber ?? 0,
            SectionName = student.Section?.Name,
            attendance.Date,
            attendance.Status,
            StatusName = "غياب",  // ✅ دائماً غياب
            attendance.Justification,
            attendance.CreatedAt
        }
    });
}

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