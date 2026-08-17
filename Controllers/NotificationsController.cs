// Controllers/NotificationController.cs
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
[Route("api/notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notificationService;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(
        AppDbContext db,
        NotificationService notificationService,
        ILogger<NotificationController> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _logger = logger;
    }

    // ============================================
    // 1. جلب جميع إشعارات المستخدم
    // ============================================
    
    [HttpGet]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] bool? isRead = null)
    {
        var userId = User.GetUserId();
        var userType = User.GetUserType();

        if (userId <= 0)
            return Unauthorized(new { success = false, message = "المستخدم غير مسجل الدخول" });

        var query = _db.Notifications
            .Where(n => n.UserId == userId && n.UserType == userType);

        if (isRead.HasValue)
            query = query.Where(n => n.IsRead == isRead.Value);

        var totalCount = await query.CountAsync();

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Body,
                n.Type,
                n.ActionUrl,
                n.IsRead,
                n.CreatedAt,
                n.ReadAt,
                TimeAgo = GetTimeAgo(n.CreatedAt)
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب الإشعارات بنجاح",
            data = new
            {
                totalCount,
                unreadCount = await _notificationService.GetUnreadCountAsync(userId, userType),
                notifications
            }
        });
    }

    // ============================================
    // 2. جلب الإشعارات غير المقروءة فقط
    // ============================================
    
    [HttpGet("unread")]
    public async Task<IActionResult> GetUnreadNotifications()
    {
        var userId = User.GetUserId();
        var userType = User.GetUserType();

        if (userId <= 0)
            return Unauthorized(new { success = false, message = "المستخدم غير مسجل الدخول" });

        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId && n.UserType == userType && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Body,
                n.Type,
                n.ActionUrl,
                n.CreatedAt,
                TimeAgo = GetTimeAgo(n.CreatedAt)
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب الإشعارات غير المقروءة بنجاح",
            data = new
            {
                count = notifications.Count,
                notifications
            }
        });
    }

    // ============================================
    // 3. جلب عدد الإشعارات غير المقروءة
    // ============================================
    
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = User.GetUserId();
        var userType = User.GetUserType();

        if (userId <= 0)
            return Unauthorized(new { success = false, message = "المستخدم غير مسجل الدخول" });

        var count = await _notificationService.GetUnreadCountAsync(userId, userType);

        return Ok(new
        {
            success = true,
            message = "تم جلب عدد الإشعارات غير المقروءة بنجاح",
            data = new
            {
                count
            }
        });
    }

    // ============================================
    // 4. تحديد إشعار كمقروء
    // ============================================
    
    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = User.GetUserId();

        if (userId <= 0)
            return Unauthorized(new { success = false, message = "المستخدم غير مسجل الدخول" });

        await _notificationService.MarkAsReadAsync(id, userId);

        return Ok(new
        {
            success = true,
            message = "تم تحديد الإشعار كمقروء"
        });
    }

    // ============================================
    // 5. تحديد جميع الإشعارات كمقروءة
    // ============================================
    
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = User.GetUserId();
        var userType = User.GetUserType();

        if (userId <= 0)
            return Unauthorized(new { success = false, message = "المستخدم غير مسجل الدخول" });

        await _notificationService.MarkAllAsReadAsync(userId, userType);

        return Ok(new
        {
            success = true,
            message = "تم تحديد جميع الإشعارات كمقروءة"
        });
    }

    // ============================================
    // 6. حذف إشعار
    // ============================================
    
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteNotification(int id)
    {
        var userId = User.GetUserId();

        if (userId <= 0)
            return Unauthorized(new { success = false, message = "المستخدم غير مسجل الدخول" });

        await _notificationService.DeleteNotificationAsync(id, userId);

        return Ok(new
        {
            success = true,
            message = "تم حذف الإشعار بنجاح"
        });
    }

    // ============================================
    // 7. حذف جميع الإشعارات
    // ============================================
    
    [HttpDelete("all")]
    public async Task<IActionResult> DeleteAllNotifications()
    {
        var userId = User.GetUserId();
        var userType = User.GetUserType();

        if (userId <= 0)
            return Unauthorized(new { success = false, message = "المستخدم غير مسجل الدخول" });

        await _notificationService.DeleteAllNotificationsAsync(userId, userType);

        return Ok(new
        {
            success = true,
            message = "تم حذف جميع الإشعارات بنجاح"
        });
    }

    // ============================================
    // 8. تسجيل FCM Token (باستخدام الجدول المنفصل)
    // ============================================
    
    [HttpPost("register-fcm")]
    public async Task<IActionResult> RegisterFcmToken([FromBody] RegisterFcmRequest request)
    {
        try
        {
            var userId = User.GetUserId();

            if (userId <= 0)
                return Unauthorized(new { success = false, message = "المستخدم غير مسجل الدخول" });

            if (string.IsNullOrEmpty(request.FcmToken))
                return BadRequest(new { success = false, message = "FCM Token مطلوب" });

            // ✅ استخدام الجدول المنفصل FcmTokens
            await _notificationService.RegisterFcmTokenAsync(userId, request.FcmToken);

            return Ok(new
            {
                success = true,
                message = "تم تسجيل الجهاز بنجاح"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error registering FCM token");
            return StatusCode(500, new { success = false, message = "حدث خطأ أثناء تسجيل الجهاز" });
        }
    }

    // ✅ نسخة محسنة من UnregisterFcmToken
    [HttpDelete("unregister-fcm")]
    public async Task<IActionResult> UnregisterFcmToken([FromBody] UnregisterFcmRequest request)
    {
        try
        {
            var userId = User.GetUserId();

            if (userId <= 0)
                return Unauthorized(new { success = false, message = "المستخدم غير مسجل الدخول" });

            if (string.IsNullOrEmpty(request.FcmToken))
                return BadRequest(new { success = false, message = "FCM Token مطلوب" });

            await _notificationService.UnregisterFcmTokenAsync(userId, request.FcmToken);

            return Ok(new
            {
                success = true,
                message = "تم إلغاء تسجيل الجهاز بنجاح"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error unregistering FCM token");
            return StatusCode(500, new { success = false, message = "حدث خطأ أثناء إلغاء تسجيل الجهاز" });
        }
    }

    // ============================================
    // دالة مساعدة لحساب الوقت المنقضي
    // ============================================
    
    private static string GetTimeAgo(DateTime createdAt)
    {
        var timeSpan = DateTime.UtcNow - createdAt;

        if (timeSpan.TotalSeconds < 60)
            return "الآن";
        if (timeSpan.TotalMinutes < 60)
            return $"منذ {Math.Floor(timeSpan.TotalMinutes)} دقيقة";
        if (timeSpan.TotalHours < 24)
            return $"منذ {Math.Floor(timeSpan.TotalHours)} ساعة";
        if (timeSpan.TotalDays < 30)
            return $"منذ {Math.Floor(timeSpan.TotalDays)} يوم";
        if (timeSpan.TotalDays < 365)
            return $"منذ {Math.Floor(timeSpan.TotalDays / 30)} شهر";
        
        return $"منذ {Math.Floor(timeSpan.TotalDays / 365)} سنة";
    }
}