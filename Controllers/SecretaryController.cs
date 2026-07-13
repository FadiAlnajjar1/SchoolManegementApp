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
[Route("api/secretary")]
[Authorize(Roles = Roles.Secretary)]
public class SecretaryController(
    AppDbContext db,
    IWebHostEnvironment env,
    NotificationService notifier) : ControllerBase
{
    private int SchoolId => User.GetSchoolId();
    private int SecretaryId => User.GetUserId();

    // ============================================
    // جلب الإعلانات والأنشطة معاً (API موحد)
    // ============================================

//     [HttpGet("feed")]
// public async Task<IActionResult> GetFeed()
// {
//     var now = DateTime.UtcNow;
    
//     // ✅ الإعلانات كـ DTOs
//     var announcements = await db.Announcements
//         .Where(a => a.SchoolId == SchoolId && 
//                    a.IsActive &&
//                    (a.ExpiryDate == null || a.ExpiryDate > now))
//         .OrderByDescending(a => a.CreatedAt)
//         .Select(a => new AnnouncementDto
//         {
//             Id = a.Id,
//             Title = a.Title,
//             Description = a.Body,
//             Date = a.CreatedAt.ToString("yyyy-MM-dd"),
//             ExpiryDate = a.ExpiryDate,
//             Audience = a.Audience.ToString(),
//             Type = a.Type.ToString(),
//             CreatedBy = a.CreatedBy != null ? a.CreatedBy.Name : null,
//             Category = "announcement"
//         })
//         .ToListAsync();  // ← List<AnnouncementDto>

//     // ✅ الأنشطة كـ DTOs
//     var activities = await db.Activities
//         .Where(a => a.SchoolId == SchoolId)
//         .OrderByDescending(a => a.CreatedAt)
//         .Select(a => new ActivityDto
//         {
//             Id = a.Id,
//             Title = a.Name,
//             Description = a.Schedule ?? "",
//             Date = a.CreatedAt.ToString("yyyy-MM-dd"),
//             Type = a.Type.ToString(),
//             Capacity = a.Capacity,
//             SupervisorId = a.SupervisorId,
//             Category = "activity"
//         })
//         .ToListAsync();  // ← List<ActivityDto>

//     // ✅ دمج DTOs في Feed
//     var feed = new List<FeedItemDto>();

//     foreach (var item in announcements)
//     {
//         feed.Add(new FeedItemDto
//         {
//             Id = item.Id,
//             Title = item.Title,
//             Description = item.Description,
//             Date = item.Date,
//             Category = item.Category,
//             Type = item.Type,
//             ExpiryDate = item.ExpiryDate,
//             Audience = item.Audience,
//             CreatedBy = item.CreatedBy
//         });
//     }

//     foreach (var item in activities)
//     {
//         feed.Add(new FeedItemDto
//         {
//             Id = item.Id,
//             Title = item.Title,
//             Description = item.Description,
//             Date = item.Date,
//             Category = item.Category,
//             Type = item.Type,
//             Capacity = item.Capacity
//         });
//     }

//     // ✅ ترتيب وتصفية
//     var sortedFeed = feed
//         .OrderByDescending(x => DateTime.Parse(x.Date))
//         .ToList();

//     // ✅ الاستجابة
//     return Ok(new
//     {
//         success = true,
//         message = "تم جلب البيانات بنجاح",
//         data = new
//         {
//             announcements = announcements,  // ← List<AnnouncementDto>
//             activities = activities,        // ← List<ActivityDto>
//             feed = sortedFeed               // ← List<FeedItemDto>
//         }
//     });
// }

    // ============================================
    // إنشاء إعلان جديد (لأمين السر)
    // ============================================
[HttpPost("announcements")]
public async Task<IActionResult> CreateAnnouncement(AnnouncementRequest request)
{
    // 1. التحقق من صلاحية التاريخ
    if (request.ExpiryDate.HasValue && request.ExpiryDate < DateTime.UtcNow)
        return BadRequest(new { success = false, message = "تاريخ الانتهاء يجب أن يكون في المستقبل" });

    // ✅ حساب LocalAnnouncementId (يبدأ من 1)
    var maxLocalId = await db.Announcements
        .Where(a => a.SchoolId == SchoolId && a.LocalAnnouncementId > 0)
        .Select(a => (int?)a.LocalAnnouncementId)
        .MaxAsync() ?? 0;

    int newLocalId = maxLocalId + 1;

    // 2. إنشاء الإعلان
    var announcement = new Announcement
    {
        SchoolId = SchoolId,
        LocalAnnouncementId = newLocalId,  // ✅ تعيين Local ID
        Title = request.Title,
        Body = request.Body,
        Audience = request.Audience,
        Type = request.Type,
        CreatedById = SecretaryId,
        ExpiryDate = request.ExpiryDate,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    db.Announcements.Add(announcement);
    await db.SaveChangesAsync();

    // 3. إشعار للمستخدمين المستهدفين
    await NotifyAnnouncementAsync(announcement);

    // ✅ استخدام LocalAnnouncementId في الـ Response
    return Created($"api/secretary/announcements/{announcement.LocalAnnouncementId}", new
    {
        success = true,
        message = "تم إنشاء الإعلان بنجاح",
        data = new
        {
            Id = announcement.Id,
            LocalId = announcement.LocalAnnouncementId,  // ✅ Local ID
            Title = announcement.Title,
            Description = announcement.Body,
            Date = announcement.CreatedAt.ToString("yyyy-MM-dd"),
            ExpiryDate = announcement.ExpiryDate,
            Audience = announcement.Audience.ToString(),
            Type = announcement.Type.ToString(),
            CreatedBy = User.Identity?.Name,
            Category = "announcement"
        }
    });
}

[HttpGet("announcements")]
public async Task<IActionResult> GetAnnouncements()
{
    var now = DateTime.UtcNow;
    
    var announcements = await db.Announcements
        .Where(a => a.SchoolId == SchoolId && 
                   a.IsActive &&
                   (a.ExpiryDate == null || a.ExpiryDate > now))
        .OrderByDescending(a => a.CreatedAt)
        .Select(a => new 
        {
            Id = a.Id,
            LocalId = a.LocalAnnouncementId,  // ✅ Local ID
            a.Title,
            Description = a.Body,
            Date = a.CreatedAt.ToString("yyyy-MM-dd"),
            a.ExpiryDate
        })
        .ToListAsync();

    return Ok(new
    {
        success = true,
        message = "تم جلب الإعلانات بنجاح",
        data = new
        {
            announcements = announcements  // ✅ تغيير الاسم من activities إلى announcements
        }
    });
}

[HttpGet("announcements/{localId:int}")]
public async Task<IActionResult> GetAnnouncement(int localId)
{
    var announcement = await db.Announcements
        .Where(a => a.SchoolId == SchoolId && a.LocalAnnouncementId == localId)
        .Select(a => new
        {
            Id = a.Id,
            LocalId = a.LocalAnnouncementId,
            a.Title,
            Description = a.Body,
            Date = a.CreatedAt.ToString("yyyy-MM-dd"),
            a.ExpiryDate,
            Audience = a.Audience.ToString(),
            Type = a.Type.ToString(),
            CreatedBy = a.CreatedBy != null ? a.CreatedBy.Name : null,
            a.IsActive
        })
        .FirstOrDefaultAsync();

    if (announcement is null)
        return NotFound(new { success = false, message = $"لا يوجد إعلان برقم {localId}" });

    return Ok(new
    {
        success = true,
        message = "تم جلب الإعلان بنجاح",
        data = announcement
    });
}

[HttpPut("announcements/{localId:int}")]
public async Task<IActionResult> UpdateAnnouncement(int localId, AnnouncementRequest request)
{
    var announcement = await db.Announcements
        .FirstOrDefaultAsync(a => a.SchoolId == SchoolId && a.LocalAnnouncementId == localId);

    if (announcement is null)
        return NotFound(new { success = false, message = $"لا يوجد إعلان برقم {localId}" });

    if (request.ExpiryDate.HasValue && request.ExpiryDate < DateTime.UtcNow)
        return BadRequest(new { success = false, message = "تاريخ الانتهاء يجب أن يكون في المستقبل" });

    announcement.Title = request.Title;
    announcement.Body = request.Body;
    announcement.Audience = request.Audience;
    announcement.Type = request.Type;
    announcement.ExpiryDate = request.ExpiryDate;

    await db.SaveChangesAsync();

    return Ok(new
    {
        success = true,
        message = "تم تحديث الإعلان بنجاح",
        data = new
        {
            Id = announcement.Id,
            LocalId = announcement.LocalAnnouncementId,
            announcement.Title,
            Description = announcement.Body,
            Date = announcement.CreatedAt.ToString("yyyy-MM-dd"),
            announcement.ExpiryDate,
            Audience = announcement.Audience.ToString(),
            Type = announcement.Type.ToString(),
            CreatedBy = User.Identity?.Name
        }
    });
}

[HttpDelete("announcements/{localId:int}")]
public async Task<IActionResult> DeleteAnnouncement(int localId)
{
    var announcement = await db.Announcements
        .FirstOrDefaultAsync(a => a.SchoolId == SchoolId && a.LocalAnnouncementId == localId);
    
    if (announcement is null)
        return NotFound(new { success = false, message = $"لا يوجد إعلان برقم {localId}" });

    db.Announcements.Remove(announcement);
    await db.SaveChangesAsync();

    return Ok(new
    {
        success = true,
        message = $"تم حذف الإعلان رقم {localId} بنجاح",
        data = new
        {
            LocalId = localId,
            Title = announcement.Title
        }
    });
}

// ============================================
// دوال مساعدة
// ============================================

private async Task NotifyAnnouncementAsync(Announcement announcement)
{
    // إرسال إشعارات للمستخدمين المستهدفين
    switch (announcement.Audience)
    {
        case AnnouncementAudience.All:
            // إشعار للجميع
            break;
        case AnnouncementAudience.Students:
            var students = await db.Students
                .Where(s => s.SchoolId == SchoolId && s.IsActive)
                .ToListAsync();
            
            foreach (var student in students)
            {
                await notifier.SendAsync(
                    student.Id,
                    UserType.Student,
                    announcement.Title,
                    announcement.Body,
                    "announcement");
            }
            break;
        case AnnouncementAudience.Teachers:
            var teachers = await db.EmployeeSchools
                .Where(es => es.SchoolId == SchoolId && 
                            es.Role == EmployeeRole.Teacher && 
                            es.IsActive)
                .Join(db.Employees,
                    es => es.EmployeeId,
                    e => e.Id,
                    (es, e) => e)
                .ToListAsync();
            
            foreach (var teacher in teachers)
            {
                await notifier.SendAsync(
                    teacher.Id,
                    UserType.Employee,
                    announcement.Title,
                    announcement.Body,
                    "announcement");
            }
            break;
        case AnnouncementAudience.Parents:
            var parents = await db.Students
                .Where(s => s.SchoolId == SchoolId && s.IsActive)
                .ToListAsync();
            
            foreach (var student in parents)
            {
                await notifier.SendToGuardianAsync(
                    student,
                    announcement.Title,
                    announcement.Body,
                    "announcement");
            }
            break;
    }
}
}
