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
        // ✅ التحقق من أن ExpiryDate في المستقبل
        if (request.ExpiryDate.HasValue && request.ExpiryDate.Value <= DateTime.UtcNow)
        {
            return BadRequest(new
            {
                success = false,
                message = "تاريخ الانتهاء يجب أن يكون في المستقبل"
            });
        }

        // ✅ حساب LocalActivityId
        var maxLocalId = await db.Activities
            .Where(a => a.SchoolId == SchoolId)
            .Select(a => (int?)a.LocalActivityId)
            .MaxAsync() ?? 0;

        int newLocalId = maxLocalId + 1;

        var activity = new Activity
        {
            SchoolId = SchoolId,
            LocalActivityId = newLocalId,
            Title = request.Title,
            Description = request.Description,
            ExpiryDate = request.ExpiryDate,
            CreatedAt = DateTime.UtcNow
        };

        db.Activities.Add(activity);
        await db.SaveChangesAsync();

        // ✅ إرسال إشعار للطلاب
        var expiryMessage = request.ExpiryDate.HasValue 
            ? $" (ينتهي التسجيل في {request.ExpiryDate.Value:yyyy-MM-dd})" 
            : "";

        await notifier.SendToAllStudentsInSchoolAsync(
            SchoolId,
            $"📢 نشاط جديد: {request.Title}",
            $"تم فتح التسجيل في النشاط \"{request.Title}\"{expiryMessage}",
            "activity",
            $"/activities/{newLocalId}"
        );

        return Created($"api/activities/{activity.LocalActivityId}", new
        {
            success = true,
            message = "تم إنشاء النشاط بنجاح",
            data = new
            {
                activity.Id,
                activity.LocalActivityId,
                activity.Title,
                activity.Description,
                activity.ExpiryDate,
                activity.CreatedAt
            }
        });
    }

    // ============================================
    // جلب جميع الأنشطة (النشطة فقط)
    // ============================================

    [HttpGet]
    public async Task<IActionResult> GetActivities()
    {
        var now = DateTime.UtcNow;
        
        var activities = await db.Activities
            .Where(a => a.SchoolId == SchoolId &&
                       (!a.ExpiryDate.HasValue || a.ExpiryDate.Value >= now))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                a.LocalActivityId,
                a.Title,
                a.Description,
                a.ExpiryDate,
                a.CreatedAt,
                IsExpired = a.ExpiryDate.HasValue && a.ExpiryDate.Value < now,
                RegisteredCount = db.ActivityRegistrations.Count(r => r.ActivityId == a.Id)
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب الأنشطة بنجاح",
            data = new
            {
                total = activities.Count,
                activities = activities
            }
        });
    }

    // ============================================
    // جلب نشاط محدد بواسطة LocalActivityId
    // ============================================

    [HttpGet("{localActivityId:int}")]
    public async Task<IActionResult> GetActivity(int localActivityId)
    {
        var activity = await db.Activities
            .FirstOrDefaultAsync(a => a.SchoolId == SchoolId &&
                                      a.LocalActivityId == localActivityId);

        if (activity is null)
            return NotFound(new
            {
                success = false,
                message = $"لا يوجد نشاط برقم {localActivityId} في هذه المدرسة"
            });

        var registeredCount = await db.ActivityRegistrations
            .CountAsync(r => r.ActivityId == activity.Id);

        var registrations = await db.ActivityRegistrations
            .Where(r => r.ActivityId == activity.Id)
            .Include(r => r.Student)
            .Select(r => new
            {
                r.Id,
                r.StudentId,
                StudentName = r.Student != null ? r.Student.Name : null,
                StudentLocalNumber = r.Student != null ? r.Student.LocalStudentNumber : 0,
                r.Status,
                StatusName = r.Status == RegistrationStatus.Pending ? "قيد الانتظار" :
                             r.Status == RegistrationStatus.Approved ? "مقبول" :
                             r.Status == RegistrationStatus.Rejected ? "مرفوض" : "غير معروف"
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب النشاط بنجاح",
            data = new
            {
                activity.Id,
                activity.LocalActivityId,
                activity.Title,
                activity.Description,
                activity.ExpiryDate,
                activity.CreatedAt,
                IsExpired = activity.ExpiryDate.HasValue && activity.ExpiryDate.Value < DateTime.UtcNow,
                RegisteredCount = registeredCount,
                Registrations = registrations
            }
        });
    }

    // ============================================
    // تحديث نشاط
    // ============================================

    [HttpPut("{id:int}")]
public async Task<IActionResult> Update(int id, ActivityUpdateRequest request)
{
    var activity = await db.Activities
        .FirstOrDefaultAsync(a => a.SchoolId == SchoolId &&
                                  a.Id == id);

    if (activity is null)
        return NotFound(new
        {
            success = false,
            message = $"لا يوجد نشاط بهذا المعرف في هذه المدرسة"
        });

    // ✅ التحقق من أن ExpiryDate في المستقبل (إذا تم إرساله)
    if (request.ExpiryDate.HasValue && request.ExpiryDate.Value <= DateTime.UtcNow)
    {
        return BadRequest(new
        {
            success = false,
            message = "تاريخ الانتهاء يجب أن يكون في المستقبل"
        });
    }

    // تحديث الحقول
    if (!string.IsNullOrWhiteSpace(request.Title))
        activity.Title = request.Title;

    if (!string.IsNullOrWhiteSpace(request.Description))
        activity.Description = request.Description;

    if (request.ExpiryDate.HasValue)
        activity.ExpiryDate = request.ExpiryDate;

    await db.SaveChangesAsync();

    return Ok(new
    {
        success = true,
        message = "تم تحديث النشاط بنجاح",
        data = new
        {
            activity.Id,
            activity.LocalActivityId,
            activity.Title,
            activity.Description,
            activity.ExpiryDate,
            activity.CreatedAt
        }
    });
}

// ============================================
// حذف نشاط
// ============================================

[HttpDelete("{id:int}")]
public async Task<IActionResult> Delete(int id)
{
    var activity = await db.Activities
        .FirstOrDefaultAsync(a => a.SchoolId == SchoolId &&
                                  a.Id == id);

    if (activity is null)
        return NotFound(new
        {
            success = false,
            message = $"لا يوجد نشاط بهذا المعرف في هذه المدرسة"
        });

    // حذف التسجيلات المرتبطة بالنشاط
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
        message = "تم حذف النشاط وجميع التسجيلات المرتبطة بنجاح",
        data = new
        {
            activity.Id,
            activity.LocalActivityId,
            activity.Title,
            DeletedRegistrations = registrations.Count
        }
    });
}

    // ============================================
    // جلب الطلاب المسجلين في نشاط معين
    // ============================================

    [HttpGet("{localActivityId:int}/registrations")]
    public async Task<IActionResult> GetActivityRegistrations(int localActivityId)
    {
        var activity = await db.Activities
            .FirstOrDefaultAsync(a => a.SchoolId == SchoolId &&
                                      a.LocalActivityId == localActivityId);

        if (activity is null)
            return NotFound(new
            {
                success = false,
                message = $"لا يوجد نشاط برقم {localActivityId} في هذه المدرسة"
            });

        var registrations = await db.ActivityRegistrations
            .Where(r => r.ActivityId == activity.Id)
            .Include(r => r.Student)
            .Select(r => new
            {
                r.Id,
                r.StudentId,
                StudentName = r.Student != null ? r.Student.Name : null,
                StudentLocalNumber = r.Student != null ? r.Student.LocalStudentNumber : 0,
                StudentEmail = r.Student != null ? r.Student.Email : null,
                SectionName = r.Student != null && r.Student.Section != null ? r.Student.Section.Name : null,
                LocalSectionNumber = r.Student != null && r.Student.Section != null ? r.Student.Section.LocalSectionNumber : 0,
                GradeName = r.Student != null && r.Student.Section != null && r.Student.Section.Grade != null ? 
                    r.Student.Section.Grade.Name : null,
                LocalGradeNumber = r.Student != null && r.Student.Section != null && r.Student.Section.Grade != null ? 
                    r.Student.Section.Grade.LocalGradeNumber : 0,
                r.Status,
                StatusName = r.Status == RegistrationStatus.Pending ? "قيد الانتظار" :
                             r.Status == RegistrationStatus.Approved ? "مقبول" :
                             r.Status == RegistrationStatus.Rejected ? "مرفوض" : "غير معروف"
            })
            .ToListAsync();

        var stats = new
        {
            Total = registrations.Count,
            Pending = registrations.Count(r => r.Status == RegistrationStatus.Pending),
            Approved = registrations.Count(r => r.Status == RegistrationStatus.Approved),
            Rejected = registrations.Count(r => r.Status == RegistrationStatus.Rejected)
        };

        return Ok(new
        {
            success = true,
            message = "تم جلب تسجيلات النشاط بنجاح",
            data = new
            {
                activity = new
                {
                    activity.Id,
                    activity.LocalActivityId,
                    activity.Title,
                    activity.Description,
                    activity.ExpiryDate
                },
                statistics = stats,
                registrations = registrations
            }
        });
    }

    // ============================================
    // جلب الطلاب المنتظرين (Pending) في نشاط معين
    // ============================================

    [HttpGet("{localActivityId:int}/registrations/pending")]
    public async Task<IActionResult> GetPendingRegistrations(int localActivityId)
    {
        var activity = await db.Activities
            .FirstOrDefaultAsync(a => a.SchoolId == SchoolId &&
                                      a.LocalActivityId == localActivityId);

        if (activity is null)
            return NotFound(new
            {
                success = false,
                message = $"لا يوجد نشاط برقم {localActivityId} في هذه المدرسة"
            });

        var pendingRegistrations = await db.ActivityRegistrations
            .Where(r => r.ActivityId == activity.Id &&
                       r.Status == RegistrationStatus.Pending)
            .Include(r => r.Student)
            .Select(r => new
            {
                r.Id,
                r.StudentId,
                StudentName = r.Student != null ? r.Student.Name : null,
                StudentLocalNumber = r.Student != null ? r.Student.LocalStudentNumber : 0,
                StudentEmail = r.Student != null ? r.Student.Email : null,
                SectionName = r.Student != null && r.Student.Section != null ? r.Student.Section.Name : null,
                LocalSectionNumber = r.Student != null && r.Student.Section != null ? r.Student.Section.LocalSectionNumber : 0,
                GradeName = r.Student != null && r.Student.Section != null && r.Student.Section.Grade != null ? 
                    r.Student.Section.Grade.Name : null,
                LocalGradeNumber = r.Student != null && r.Student.Section != null && r.Student.Section.Grade != null ? 
                    r.Student.Section.Grade.LocalGradeNumber : 0,
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب الطلاب المنتظرين بنجاح",
            data = new
            {
                activity = new
                {
                    activity.Id,
                    activity.LocalActivityId,
                    activity.Title
                },
                totalPending = pendingRegistrations.Count,
                students = pendingRegistrations
            }
        });
    }

    // ============================================
    // قبول تسجيل طالب في نشاط
    // ============================================

    [HttpPost("{localActivityId:int}/registrations/{studentLocalNumber:int}/approve")]
    public async Task<IActionResult> ApproveRegistration(int localActivityId, int studentLocalNumber)
    {
        // 1. التحقق من وجود النشاط
        var activity = await db.Activities
            .FirstOrDefaultAsync(a => a.SchoolId == SchoolId &&
                                      a.LocalActivityId == localActivityId);

        if (activity is null)
            return NotFound(new
            {
                success = false,
                message = $"لا يوجد نشاط برقم {localActivityId} في هذه المدرسة"
            });

        // 2. التحقق من وجود الطالب
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalStudentNumber == studentLocalNumber &&
                                      s.IsActive);

        if (student is null)
            return NotFound(new
            {
                success = false,
                message = $"لا يوجد طالب برقم {studentLocalNumber} في هذه المدرسة"
            });

        // 3. البحث عن التسجيل
        var registration = await db.ActivityRegistrations
            .FirstOrDefaultAsync(r => r.ActivityId == activity.Id &&
                                      r.StudentId == student.Id);

        if (registration is null)
            return NotFound(new
            {
                success = false,
                message = $"الطالب {studentLocalNumber} غير مسجل في هذا النشاط"
            });

        // 4. التحقق من أن الحالة "قيد الانتظار"
        if (registration.Status != RegistrationStatus.Pending)
            return BadRequest(new
            {
                success = false,
                message = $"حالة التسجيل الحالية هي {registration.Status}، لا يمكن قبولها"
            });

        // 5. قبول التسجيل
        registration.Status = RegistrationStatus.Approved;

        await db.SaveChangesAsync();

        // 6. ✅ إشعار للطالب بالقبول
        await notifier.SendAsync(
            student.Id,
            UserType.Student,
            "✅ تم قبولك في النشاط",
            $"تم قبول طلبك في النشاط \"{activity.Title}\"",
            "activity",
            $"/activities/{activity.LocalActivityId}"
        );

        return Ok(new
        {
            success = true,
            message = "تم قبول تسجيل الطالب في النشاط بنجاح",
            data = new
            {
                activityId = activity.Id,
                activityLocalId = activity.LocalActivityId,
                activityTitle = activity.Title,
                studentId = student.Id,
                studentLocalNumber = student.LocalStudentNumber,
                studentName = student.Name,
                status = registration.Status.ToString(),
                statusName = "مقبول",
            }
        });
    }

    // ============================================
    // رفض تسجيل طالب في نشاط
    // ============================================

    [HttpPost("{localActivityId:int}/registrations/{studentLocalNumber:int}/reject")]
    public async Task<IActionResult> RejectRegistration(int localActivityId, int studentLocalNumber, [FromBody] RejectRequest? request)
    {
        // 1. التحقق من وجود النشاط
        var activity = await db.Activities
            .FirstOrDefaultAsync(a => a.SchoolId == SchoolId &&
                                      a.LocalActivityId == localActivityId);

        if (activity is null)
            return NotFound(new
            {
                success = false,
                message = $"لا يوجد نشاط برقم {localActivityId} في هذه المدرسة"
            });

        // 2. التحقق من وجود الطالب
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalStudentNumber == studentLocalNumber &&
                                      s.IsActive);

        if (student is null)
            return NotFound(new
            {
                success = false,
                message = $"لا يوجد طالب برقم {studentLocalNumber} في هذه المدرسة"
            });

        // 3. البحث عن التسجيل
        var registration = await db.ActivityRegistrations
            .FirstOrDefaultAsync(r => r.ActivityId == activity.Id &&
                                      r.StudentId == student.Id);

        if (registration is null)
            return NotFound(new
            {
                success = false,
                message = $"الطالب {studentLocalNumber} غير مسجل في هذا النشاط"
            });

        // 4. التحقق من أن الحالة "قيد الانتظار"
        if (registration.Status != RegistrationStatus.Pending)
            return BadRequest(new
            {
                success = false,
                message = $"حالة التسجيل الحالية هي {registration.Status}، لا يمكن رفضها"
            });

        // 5. رفض التسجيل
        registration.Status = RegistrationStatus.Rejected;
        registration.RejectionReason = request?.Reason ?? "لم يتم تقديم سبب";

        await db.SaveChangesAsync();

        // 6. ✅ إشعار للطالب بالرفض مع السبب
        await notifier.SendAsync(
            student.Id,
            UserType.Student,
            "❌ تم رفض طلبك في النشاط",
            $"تم رفض طلبك في النشاط \"{activity.Title}\". السبب: {registration.RejectionReason}",
            "activity",
            $"/activities/{activity.LocalActivityId}"
        );

        return Ok(new
        {
            success = true,
            message = "تم رفض تسجيل الطالب في النشاط بنجاح",
            data = new
            {
                activityId = activity.Id,
                activityLocalId = activity.LocalActivityId,
                activityTitle = activity.Title,
                studentId = student.Id,
                studentLocalNumber = student.LocalStudentNumber,
                studentName = student.Name,
                status = registration.Status.ToString(),
                statusName = "مرفوض",
                rejectionReason = registration.RejectionReason,
            }
        });
    }
}