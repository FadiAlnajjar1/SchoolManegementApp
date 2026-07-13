using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Auth;
using SchoolManagement.Api.Data;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/feed")]
[Authorize]  // ✅ يتطلب تسجيل دخول
public class FeedController : ControllerBase
{
    private readonly AppDbContext _db;
    private int UserId => User.GetUserId();
    private UserType CurrentUserType => User.GetUserType();

    public FeedController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetFeed()
    {
        var now = DateTime.UtcNow;
        
        // ✅ جلب جميع المدارس حسب نوع المستخدم
        var schoolIds = await GetUserSchoolIdsAsync();
        
        if (!schoolIds.Any())
            return Unauthorized(new { message = "لا توجد مدارس مرتبطة بك" });

        // ✅ الإعلانات من جميع المدارس
        var announcements = await _db.Announcements
            .Where(a => schoolIds.Contains(a.SchoolId) && 
                       a.IsActive &&
                       (a.Audience == AnnouncementAudience.All || 
                        a.Audience == AnnouncementAudience.Students ||
                        a.Audience == AnnouncementAudience.Teachers) &&
                       (a.ExpiryDate == null || a.ExpiryDate > now))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                LocalAnnouncementId = a.LocalAnnouncementId,
                a.Title,
                Description = a.Body,
                Date = a.CreatedAt.ToString("yyyy-MM-dd"),
                a.ExpiryDate,
                SchoolName = a.School != null ? a.School.Name : null,
                Type = "announcement"
            })
            .ToListAsync();

        // ✅ الأنشطة من جميع المدارس
        var activities = await _db.Activities
            .Where(a => schoolIds.Contains(a.SchoolId))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                LocalActivityId = a.LocalActivityId,
                Title = a.Name,
                Description = a.Description ?? a.Schedule ?? "",
                Date = a.CreatedAt.ToString("yyyy-MM-dd"),
                ExpiryDate = (DateTime?)null,
                SchoolName = a.School != null ? a.School.Name : null,
                Type = "activity"
            })
            .ToListAsync();

        // ✅ دمج الإعلانات والأنشطة
        var allItems = new List<object>();
        allItems.AddRange(announcements);
        allItems.AddRange(activities);

        var sortedFeed = allItems
            .OrderByDescending(x => DateTime.Parse(((dynamic)x).Date))
            .ToList();

        return Ok(new
        {
            success = true,
            message = "تم جلب البيانات بنجاح",
            data = new
            {
                userType = CurrentUserType.ToString(),
                schools = await GetUserSchoolsAsync(),
                announcements = announcements,
                activities = activities,
                feed = sortedFeed
            }
        });
    }

    // ============================================
    // دوال مساعدة
    // ============================================

    private async Task<List<int>> GetUserSchoolIdsAsync()
    {
        var userId = UserId;
        var userType = CurrentUserType;
        
        switch (userType)
        {
            case UserType.Student:
                var student = await _db.Students.FindAsync(userId);
                return student != null ? new List<int> { student.SchoolId } : new List<int>();

            case UserType.Employee:
                return await _db.EmployeeSchools
                    .Where(es => es.EmployeeId == userId && es.IsActive)
                    .Select(es => es.SchoolId)
                    .ToListAsync();

            case UserType.Admin:
                return await _db.Schools.Select(s => s.Id).ToListAsync();

            default:
                return new List<int>();
        }
    }

    private async Task<List<object>> GetUserSchoolsAsync()
    {
        var userId = UserId;
        var userType = CurrentUserType;
        
        switch (userType)
        {
            case UserType.Student:
                var student = await _db.Students
                    .Include(s => s.School)
                    .FirstOrDefaultAsync(s => s.Id == userId);
                    
                return student != null 
                    ? new List<object> { new { student.SchoolId, SchoolName = student.School?.Name } }
                    : new List<object>();

            case UserType.Employee:
                return await _db.EmployeeSchools
                    .Where(es => es.EmployeeId == userId && es.IsActive)
                    .Include(es => es.School)
                    .Select(es => new
                    {
                        es.SchoolId,
                        SchoolName = es.School != null ? es.School.Name : null,
                        es.LocalEmployeeNumber,
                        Role = es.Role.ToString()
                    })
                    .ToListAsync<object>();

            case UserType.Admin:
                return await _db.Schools
                    .Select(s => new { s.Id, SchoolName = s.Name })
                    .ToListAsync<object>();

            default:
                return new List<object>();
        }
    }
}