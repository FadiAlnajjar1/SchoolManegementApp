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
[Route("api/activities")]
[Authorize(Roles = Roles.ActivitySupervisor)]
public class ActivitiesController(
    AppDbContext db,
    NotificationService notifier) : ControllerBase
{
    private int SchoolId => User.GetSchoolId();

    // ============================================
    // إنشاء نشاط
    // ============================================

    [HttpPost]
    public async Task<IActionResult> Create(ActivityRequest request)
    {
        // ✅ حساب LocalActivityId
        var maxLocalId = await db.Activities
            .Where(a => a.SchoolId == SchoolId)
            .Select(a => (int?)a.LocalActivityId)
            .MaxAsync() ?? 0;

        int newLocalId = maxLocalId + 1;

        var activity = new Activity
        {
            SchoolId = SchoolId,
            LocalActivityId = newLocalId,  // ✅ تعيين Local ID
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            Schedule = request.Schedule ?? "",
            Capacity = request.Capacity,
            SupervisorId = User.GetUserId(),
            CreatedAt = DateTime.UtcNow
        };

        db.Activities.Add(activity);
        await db.SaveChangesAsync();

        // إشعار للطلاب
        await notifier.SendToAllStudentsInSchoolAsync(SchoolId,
            $"نشاط جديد: {request.Name}",
            $"تم فتح التسجيل في النشاط \"{request.Name}\" - {request.Schedule}",
            "activity");

        return Created($"api/activities/{activity.LocalActivityId}", new
        {
            success = true,
            message = "تم إنشاء النشاط بنجاح",
            data = new
            {
                activity.Id,
                activity.LocalActivityId,  // ✅ إرجاع Local ID
                activity.Name,
                activity.Description,
                activity.Type,
                activity.Schedule,
                activity.Capacity,
                activity.SupervisorId,
                activity.CreatedAt
            }
        });
    }

    // ============================================
    // جلب الأنشطة (مع Pagination وفلترة)
    // ============================================

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] ActivityType? type,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = db.Activities
            .Where(a => a.SchoolId == SchoolId);

        if (type.HasValue)
            query = query.Where(a => a.Type == type);

        if (fromDate.HasValue)
            query = query.Where(a => a.CreatedAt >= fromDate);

        if (toDate.HasValue)
            query = query.Where(a => a.CreatedAt <= toDate);

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var activities = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.LocalActivityId,  // ✅ إرجاع Local ID
                a.Name,
                a.Description,
                a.Type,
                a.Schedule,
                a.Capacity,
                a.SupervisorId,
                SupervisorName = a.Supervisor != null ? a.Supervisor.Name : null,
                a.CreatedAt,
                RegistrationsCount = db.ActivityRegistrations.Count(r => r.ActivityId == a.Id),
                ApprovedCount = db.ActivityRegistrations.Count(r => r.ActivityId == a.Id && r.Status == RegistrationStatus.Approved),
                PendingCount = db.ActivityRegistrations.Count(r => r.ActivityId == a.Id && r.Status == RegistrationStatus.Pending)
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب الأنشطة بنجاح",
            data = new
            {
                totalCount,
                totalPages,
                page,
                pageSize,
                activities
            }
        });
    }

    // ============================================
    // جلب نشاط معين
    // ============================================

    [HttpGet("{localActivityId:int}")]
    public async Task<IActionResult> GetActivity(int localActivityId)
    {
        var activity = await db.Activities
            .Include(a => a.Supervisor)
            .Where(a => a.SchoolId == SchoolId && a.LocalActivityId == localActivityId)  // ✅ استخدام Local ID
            .Select(a => new
            {
                a.Id,
                a.LocalActivityId,
                a.Name,
                a.Description,
                a.Type,
                a.Schedule,
                a.Capacity,
                a.SupervisorId,
                SupervisorName = a.Supervisor != null ? a.Supervisor.Name : null,
                a.CreatedAt,
                Registrations = db.ActivityRegistrations
                    .Where(r => r.ActivityId == a.Id)
                    .Select(r => new
                    {
                        r.Id,
                        r.StudentId,
                        StudentName = r.Student != null ? r.Student.Name : null,
                        r.Status,
                        r.CreatedAt
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (activity is null)
            return NotFound(new { success = false, message = "النشاط غير موجود" });

        return Ok(new
        {
            success = true,
            message = "تم جلب النشاط بنجاح",
            data = activity
        });
    }

    // ============================================
    // تحديث نشاط
    // ============================================

    [HttpPut("{localActivityId:int}")]
    public async Task<IActionResult> Update(int localActivityId, ActivityRequest request)
    {
        var activity = await db.Activities
            .FirstOrDefaultAsync(a => a.SchoolId == SchoolId && a.LocalActivityId == localActivityId);  // ✅ استخدام Local ID

        if (activity is null)
            return NotFound(new { success = false, message = "النشاط غير موجود" });

        // التحقق من أن السعة الجديدة لا تقل عن عدد المسجلين
        var approvedCount = await db.ActivityRegistrations
            .CountAsync(r => r.ActivityId == activity.Id && r.Status == RegistrationStatus.Approved);

        if (request.Capacity < approvedCount)
            return BadRequest(new
            {
                success = false,
                message = $"لا يمكن تقليل السعة إلى {request.Capacity} لأن هناك {approvedCount} طالب مسجل"
            });

        activity.Name = request.Name;
        activity.Description = request.Description;
        activity.Type = request.Type;
        activity.Schedule = request.Schedule ?? activity.Schedule;
        activity.Capacity = request.Capacity;

        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "تم تحديث النشاط بنجاح",
            data = new
            {
                activity.Id,
                activity.LocalActivityId,
                activity.Name,
                activity.Description,
                activity.Type,
                activity.Schedule,
                activity.Capacity,
                activity.SupervisorId,
                activity.CreatedAt
            }
        });
    }

    // ============================================
    // حذف نشاط
    // ============================================

    [HttpDelete("{localActivityId:int}")]
    public async Task<IActionResult> Delete(int localActivityId)
    {
        var activity = await db.Activities
            .FirstOrDefaultAsync(a => a.SchoolId == SchoolId && a.LocalActivityId == localActivityId);  // ✅ استخدام Local ID

        if (activity is null)
            return NotFound(new { success = false, message = "النشاط غير موجود" });

        // حذف جميع التسجيلات المرتبطة
        var registrations = await db.ActivityRegistrations
            .Where(r => r.ActivityId == activity.Id)
            .ToListAsync();

        if (registrations.Any())
            db.ActivityRegistrations.RemoveRange(registrations);

        db.Activities.Remove(activity);
        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "تم حذف النشاط بنجاح"
        });
    }

    // ============================================
    // جلب الطلاب المسجلين في نشاط معين
    // ============================================

    [HttpGet("{localActivityId:int}/students")]
    public async Task<IActionResult> GetActivityStudents(
        int localActivityId,
        [FromQuery] RegistrationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var activity = await db.Activities
            .FirstOrDefaultAsync(a => a.SchoolId == SchoolId && a.LocalActivityId == localActivityId);  // ✅ استخدام Local ID

        if (activity is null)
            return NotFound(new { success = false, message = "النشاط غير موجود" });

        var query = db.ActivityRegistrations
            .Include(r => r.Student)
            .Where(r => r.ActivityId == activity.Id);

        if (status.HasValue)
            query = query.Where(r => r.Status == status);

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var students = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.StudentId,
                StudentName = r.Student != null ? r.Student.Name : null,
                StudentLocalNumber = r.Student != null ? r.Student.LocalStudentNumber : 0,
                r.Student!.Email,
                r.Student.GuardianName,
                r.Student.GuardianPhone,
                r.Status,
                r.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب الطلاب المسجلين بنجاح",
            data = new
            {
                activity = new
                {
                    activity.Id,
                    activity.LocalActivityId,
                    activity.Name,
                    activity.Type,
                    activity.Capacity
                },
                students = students,
                totalCount,
                page,
                pageSize,
                totalPages,
                approvedCount = await db.ActivityRegistrations
                    .CountAsync(r => r.ActivityId == activity.Id && r.Status == RegistrationStatus.Approved),
                pendingCount = await db.ActivityRegistrations
                    .CountAsync(r => r.ActivityId == activity.Id && r.Status == RegistrationStatus.Pending),
                rejectedCount = await db.ActivityRegistrations
                    .CountAsync(r => r.ActivityId == activity.Id && r.Status == RegistrationStatus.Rejected)
            }
        });
    }

    // ============================================
    // الموافقة/رفض تسجيل
    // ============================================

    [HttpPatch("registrations/{registrationId:int}")]
public async Task<IActionResult> DecideRegistration(int registrationId, RegistrationDecisionRequest request)
{
    var registration = await db.ActivityRegistrations
        .Include(r => r.Activity)
        .Include(r => r.Student)
        .FirstOrDefaultAsync(r => r.Id == registrationId && r.Activity!.SchoolId == SchoolId);

    if (registration is null)
        return NotFound(new { success = false, message = "التسجيل غير موجود" });

        // إذا كانت الموافقة، التحقق من السعة
        if (request.Status == RegistrationStatus.Approved)
        {
            var approved = await db.ActivityRegistrations
                .CountAsync(r => r.ActivityId == registration.ActivityId &&
                                r.Status == RegistrationStatus.Approved);

            if (approved >= registration.Activity!.Capacity)
                return BadRequest(new { success = false, message = "اكتملت سعة النشاط" });
        }

        registration.Status = request.Status;
        await db.SaveChangesAsync();

        // إشعار للطالب
        var statusMessage = request.Status == RegistrationStatus.Approved ? "تم قبول طلبك" : "تم رفض طلبك";
        await notifier.SendAsync(registration.StudentId, UserType.Student,
            request.Status == RegistrationStatus.Approved ? "✅ قبول تسجيل نشاط" : "❌ تحديث تسجيل نشاط",
            $"{statusMessage} في نشاط \"{registration.Activity!.Name}\"",
            "activity");

        return Ok(new
        {
            success = true,
            message = $"تم {request.Status} التسجيل بنجاح",
            data = new
            {
                registration.Id,
                registration.StudentId,
                StudentName = registration.Student?.Name,
                registration.ActivityId,
                ActivityLocalId = registration.Activity?.LocalActivityId,
                ActivityName = registration.Activity?.Name,
                registration.Status,
                registration.CreatedAt
            }
        });
    }
    [HttpGet("students/{localStudentNumber:int}/registrations")]
public async Task<IActionResult> GetStudentRegistrations(int localStudentNumber)
{
    var student = await db.Students
        .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                  s.LocalStudentNumber == localStudentNumber);

    if (student is null)
        return NotFound(new { success = false, message = "الطالب غير موجود" });

    var registrations = await db.ActivityRegistrations
        .Where(r => r.StudentId == student.Id)
        .Select(r => new
        {
            r.Id,
            ActivityLocalId = r.Activity != null ? r.Activity.LocalActivityId : 0,
            ActivityName = r.Activity != null ? r.Activity.Name : null,
            r.Status,
            r.CreatedAt
        })
        .ToListAsync();

    return Ok(new
    {
        success = true,
        message = "تم جلب تسجيلات الطالب بنجاح",
        data = new
        {
            Student = new
            {
                student.Id,
                student.Name,
                student.LocalStudentNumber
            },
            Registrations = registrations
        }
    });
}

    // ============================================
    // إلغاء تسجيل طالب من نشاط
    // ============================================

    [HttpDelete("registrations/{registrationId:int}")]
public async Task<IActionResult> CancelRegistration(int registrationId)
{
    var registration = await db.ActivityRegistrations
        .Include(r => r.Activity)
        .FirstOrDefaultAsync(r => r.Id == registrationId && r.Activity!.SchoolId == SchoolId);

    if (registration is null)
        return NotFound(new { success = false, message = "التسجيل غير موجود" });

    db.ActivityRegistrations.Remove(registration);
    await db.SaveChangesAsync();

    return Ok(new
    {
        success = true,
        message = "تم إلغاء التسجيل بنجاح",
        data = new
        {
            registration.Id,
            ActivityLocalId = registration.Activity != null ? registration.Activity.LocalActivityId : 0,
            ActivityName = registration.Activity != null ? registration.Activity.Name : null,
            registration.StudentId,
            registration.CreatedAt
        }
    });
}

    // ============================================
    // إحصائيات الأنشطة
    // ============================================

    [HttpGet("statistics")]
public async Task<IActionResult> GetStatistics()
{
    var totalActivities = await db.Activities
        .CountAsync(a => a.SchoolId == SchoolId);

    var upcomingActivities = await db.Activities
        .CountAsync(a => a.SchoolId == SchoolId && a.CreatedAt > DateTime.UtcNow.AddDays(-7));

    var totalRegistrations = await db.ActivityRegistrations
        .CountAsync(r => r.Activity!.SchoolId == SchoolId);

    var pendingRegistrations = await db.ActivityRegistrations
        .CountAsync(r => r.Activity!.SchoolId == SchoolId && r.Status == RegistrationStatus.Pending);

    var approvedRegistrations = await db.ActivityRegistrations
        .CountAsync(r => r.Activity!.SchoolId == SchoolId && r.Status == RegistrationStatus.Approved);

    var activitiesByType = await db.Activities
        .Where(a => a.SchoolId == SchoolId)
        .GroupBy(a => a.Type)
        .Select(g => new
        {
            Type = g.Key.ToString(),
            Count = g.Count(),
            LocalActivityIds = g.Select(a => a.LocalActivityId).ToList()  // ✅ Local IDs
        })
        .ToListAsync();

    // ✅ أنشطة مع Local IDs
    var topActivities = await db.Activities
        .Where(a => a.SchoolId == SchoolId)
        .OrderByDescending(a => db.ActivityRegistrations.Count(r => r.ActivityId == a.Id))
        .Take(5)
        .Select(a => new
        {
            LocalActivityId = a.LocalActivityId,  // ✅ Local ID
            a.Name,
            RegistrationsCount = db.ActivityRegistrations.Count(r => r.ActivityId == a.Id)
        })
        .ToListAsync();

    return Ok(new
    {
        success = true,
        message = "تم جلب الإحصائيات بنجاح",
        data = new
        {
            totalActivities,
            upcomingActivities,
            totalRegistrations,
            pendingRegistrations,
            approvedRegistrations,
            activitiesByType,
            topActivities  // ✅ أنشطة مع Local IDs
        }
    });
}
}