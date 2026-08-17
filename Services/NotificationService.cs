using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Data;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Services;

public class NotificationService(AppDbContext db, ILogger<NotificationService> logger)
{
    // ============================================
    // إرسال إشعار لمستخدم واحد
    // ============================================

    public async Task SendAsync(int userId, UserType userType, string title, string body, string type = "general", string? actionUrl = null)
    {
        // 1. حفظ الإشعار في قاعدة البيانات
        var notification = new Models.Notification
        {
            UserId = userId,
            UserType = userType,
            Title = title,
            Body = body,
            Type = type,
            ActionUrl = actionUrl,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        // 2. جلب جميع FcmTokens النشطة للمستخدم
        var fcmTokens = await db.FcmTokens
            .Where(t => t.UserId == userId && t.fcmToken != null)
            .Select(t => t.fcmToken!)
            .ToListAsync();

        // 3. إرسال الإشعار لكل الأجهزة
        if (fcmTokens.Any())
        {
            await PushAsync(fcmTokens, title, body, notification.Id);
        }
    }

    // ============================================
    // إرسال إشعار لولي الأمر
    // ============================================
    
    public async Task SendToGuardianAsync(Student student, string title, string body, string type = "guardian", string? actionUrl = null)
    {
        // 1. حفظ الإشعار في قاعدة البيانات
        var notification = new Models.Notification
        {
            UserId = student.Id,
            UserType = UserType.Student,
            Title = title,
            Body = body,
            Type = type,
            ActionUrl = actionUrl,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

    
    }

    // ============================================
    // إرسال إشعار لأمين المكتبة
    // ============================================

    public async Task SendToLibrarianAsync(int schoolId, string title, string body, string type = "library", string? actionUrl = null)
    {
        try
        {
            var librarians = await db.EmployeeSchools
                .Include(es => es.Employee)
                .Where(es => es.SchoolId == schoolId && 
                            es.Role == EmployeeRole.Librarian && 
                            es.IsActive &&
                            es.Employee != null &&
                            !es.Employee.IsDismissed)
                .Select(es => es.Employee!)
                .ToListAsync();

            if (librarians is null || !librarians.Any())
            {
                logger.LogWarning("No librarians found for school {SchoolId}", schoolId);
                return;
            }

            foreach (var librarian in librarians)
            {
                await SendAsync(librarian.Id, UserType.Employee, title, body, type, actionUrl);
            }

            logger.LogInformation("Notification sent to {Count} librarians in school {SchoolId}", librarians.Count, schoolId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending notification to librarians in school {SchoolId}", schoolId);
        }
    }

    // ============================================
    // إرسال إشعار لمدير المدرسة
    // ============================================

    public async Task SendToSchoolManagersAsync(int schoolId, string title, string body, string type = "general", string? actionUrl = null)
    {
        var managers = await db.EmployeeSchools
            .Include(es => es.Employee)
            .Where(es => es.SchoolId == schoolId && 
                         es.Role == EmployeeRole.Principal && 
                         es.IsActive &&
                         es.Employee != null &&
                         !es.Employee.IsDismissed)
            .Select(es => es.Employee!)
            .ToListAsync();

        foreach (var manager in managers)
        {
            await SendAsync(manager.Id, UserType.Employee, title, body, type, actionUrl);
        }
    }

    // ============================================
    // إرسال إشعار للموجه (Counselor)
    // ============================================

    public async Task SendToCounselorsInSchoolAsync(int schoolId, string title, string body, string type = "general", string? actionUrl = null)
    {
        var counselors = await db.EmployeeSchools
            .Include(es => es.Employee)
            .Where(es => es.SchoolId == schoolId && 
                         es.Role == EmployeeRole.Counselor && 
                         es.IsActive &&
                         es.Employee != null &&
                         !es.Employee.IsDismissed)
            .Select(es => es.Employee!)
            .ToListAsync();

        foreach (var counselor in counselors)
        {
            await SendAsync(counselor.Id, UserType.Employee, title, body, type, actionUrl);
        }
    }

    // ============================================
    // إرسال إشعار لجميع المعلمين في مدرسة
    // ============================================

    public async Task SendToTeachersInSchoolAsync(int schoolId, string title, string body, string type = "general", string? actionUrl = null)
    {
        var teachers = await db.EmployeeSchools
            .Include(es => es.Employee)
            .Where(es => es.SchoolId == schoolId && 
                         es.Role == EmployeeRole.Teacher && 
                         es.IsActive &&
                         es.Employee != null &&
                         !es.Employee.IsDismissed)
            .Select(es => es.Employee!)
            .ToListAsync();

        foreach (var teacher in teachers)
        {
            await SendAsync(teacher.Id, UserType.Employee, title, body, type, actionUrl);
        }
    }

    // ============================================
    // إرسال إشعار لجميع الموظفين في مدرسة
    // ============================================

    public async Task SendToAllEmployeesInSchoolAsync(int schoolId, string title, string body, string type = "general", string? actionUrl = null)
    {
        var employees = await db.EmployeeSchools
            .Include(es => es.Employee)
            .Where(es => es.SchoolId == schoolId && 
                         es.IsActive &&
                         es.Employee != null &&
                         !es.Employee.IsDismissed)
            .Select(es => es.Employee!)
            .ToListAsync();

        foreach (var employee in employees)
        {
            await SendAsync(employee.Id, UserType.Employee, title, body, type, actionUrl);
        }
    }

    // ============================================
    // إرسال إشعار لجميع الطلاب في مدرسة
    // ============================================

    public async Task SendToAllStudentsInSchoolAsync(int schoolId, string title, string body, string type = "general", string? actionUrl = null)
    {
        var students = await db.Students
            .Where(s => s.SchoolId == schoolId && s.IsActive)
            .ToListAsync();

        foreach (var student in students)
        {
            await SendAsync(student.Id, UserType.Student, title, body, type, actionUrl);
        }
    }

    // ============================================
    // إرسال إشعار لطلاب شعبة معينة
    // ============================================

    public async Task SendToSectionStudentsAsync(int sectionId, string title, string body, string type = "general", string? actionUrl = null)
    {
        var students = await db.Students
            .Where(s => s.SectionId == sectionId && s.IsActive)
            .ToListAsync();

        foreach (var student in students)
        {
            await SendAsync(student.Id, UserType.Student, title, body, type, actionUrl);
        }
    }

    // ============================================
    // إرسال إشعار للموجه (Counselor) - فردي
    // ============================================

    public async Task SendToCounselorAsync(int counselorId, string title, string body, string type = "general", string? actionUrl = null)
    {
        await SendAsync(counselorId, UserType.Employee, title, body, type, actionUrl);
    }

    // ============================================
    // إرسال إشعار لمجموعة من المستخدمين
    // ============================================

    public async Task SendToManyAsync(List<int> userIds, UserType userType, string title, string body, string type = "general", string? actionUrl = null)
    {
        foreach (var userId in userIds)
        {
            await SendAsync(userId, userType, title, body, type, actionUrl);
        }
    }

    // ============================================
    // إرسال إشعار لدور معين في المدرسة
    // ============================================

    public async Task SendToRoleInSchoolAsync(int schoolId, EmployeeRole role, string title, string body, string type = "general", string? actionUrl = null)
    {
        var employees = await db.EmployeeSchools
            .Include(es => es.Employee)
            .Where(es => es.SchoolId == schoolId && 
                         es.Role == role && 
                         es.IsActive &&
                         es.Employee != null &&
                         !es.Employee.IsDismissed)
            .Select(es => es.Employee!)
            .ToListAsync();

        foreach (var employee in employees)
        {
            await SendAsync(employee.Id, UserType.Employee, title, body, type, actionUrl);
        }
    }

    // ============================================
    // تسجيل FCM Token
    // ============================================

    public async Task RegisterFcmTokenAsync(int userId, string fcmToken)
    {
        try
        {
            // ✅ البحث عن جهاز مسجل بنفس التوكن
            var existingDevice = await db.FcmTokens
                .FirstOrDefaultAsync(t => t.fcmToken == fcmToken);

            if (existingDevice is not null)
            {
                // ✅ تحديث الجهاز الموجود
                existingDevice.UserId = userId;
            }
            else
            {
                // ✅ إضافة جهاز جديد
                var newDevice = new FcmToken
                {
                    UserId = userId,
                    fcmToken = fcmToken
                };
                db.FcmTokens.Add(newDevice);
            }

            await db.SaveChangesAsync();
            logger.LogInformation("✅ FCM token registered for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error registering FCM token for user {UserId}", userId);
        }
    }

    // ============================================
    // إلغاء تسجيل FCM Token
    // ============================================

    public async Task UnregisterFcmTokenAsync(int userId, string fcmToken)
    {
        try
        {
            var device = await db.FcmTokens
                .FirstOrDefaultAsync(t => t.fcmToken == fcmToken && t.UserId == userId);

            if (device is not null)
            {
                db.FcmTokens.Remove(device);
                await db.SaveChangesAsync();
                logger.LogInformation("✅ FCM token unregistered for user {UserId}", userId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error unregistering FCM token for user {UserId}", userId);
        }
    }

    // ============================================
    // دوال مساعدة
    // ============================================

    private async Task PushAsync(List<string> fcmTokens, string title, string body, int notificationId = 0)
    {
        if (!FirebaseInitializer.IsReady || !fcmTokens.Any()) 
            return;

        try
        {
            var message = new MulticastMessage
            {
                Tokens = fcmTokens,
                Notification = new FirebaseAdmin.Messaging.Notification 
                { 
                    Title = title, 
                    Body = body 
                },
                Data = new Dictionary<string, string>
                {
                    { "notification_id", notificationId.ToString() },
                    { "type", "general" }
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ChannelId = "school_notifications",
                        Icon = "ic_notification",
                        Sound = "default"
                    }
                },
                Apns = new ApnsConfig
                {
                    Aps = new Aps
                    {
                        Alert = new ApsAlert
                        {
                            Title = title,
                            Body = body
                        },
                        Sound = "default"
                    }
                }
            };

            var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);
            
            if (response.FailureCount > 0)
            {
                for (int i = 0; i < response.Responses.Count; i++)
                {
                    if (!response.Responses[i].IsSuccess)
                    {
                        var failedToken = fcmTokens[i];
                        var device = await db.FcmTokens
                            .FirstOrDefaultAsync(t => t.fcmToken == failedToken);
                        
                        if (device is not null)
                        {
                            db.FcmTokens.Remove(device);
                        }
                    }
                }
                await db.SaveChangesAsync();
                logger.LogWarning("⚠️ {FailureCount} FCM tokens failed and removed", response.FailureCount);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FCM push failed for {Count} tokens", fcmTokens.Count);
        }
    }

    // ============================================
    // جلب إشعارات المستخدم
    // ============================================

    public async Task<List<Models.Notification>> GetUserNotificationsAsync(int userId, UserType userType, int take = 50)
    {
        return await db.Notifications
            .Where(n => n.UserId == userId && n.UserType == userType)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    // ============================================
    // جلب إشعارات أمين المكتبة
    // ============================================

    public async Task<List<Models.Notification>> GetLibrarianNotificationsAsync(int schoolId, int take = 50)
    {
        try
        {
            var librarianIds = await db.EmployeeSchools
                .Where(es => es.SchoolId == schoolId && 
                            es.Role == EmployeeRole.Librarian && 
                            es.IsActive)
                .Select(es => es.EmployeeId)
                .ToListAsync();

            if (librarianIds is null || !librarianIds.Any())
                return new List<Models.Notification>();

            return await db.Notifications
                .Where(n => librarianIds.Contains(n.UserId) && n.UserType == UserType.Employee)
                .OrderByDescending(n => n.CreatedAt)
                .Take(take)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting librarian notifications for school {SchoolId}", schoolId);
            return new List<Models.Notification>();
        }
    }

    // ============================================
    // تحديث حالة القراءة
    // ============================================

    public async Task MarkAsReadAsync(int notificationId, int userId)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        
        if (notification is not null)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(int userId, UserType userType)
    {
        var notifications = await db.Notifications
            .Where(n => n.UserId == userId && n.UserType == userType && !n.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    // ============================================
    // حذف الإشعارات
    // ============================================

    public async Task DeleteNotificationAsync(int notificationId, int userId)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        
        if (notification is not null)
        {
            db.Notifications.Remove(notification);
            await db.SaveChangesAsync();
        }
    }

    public async Task DeleteAllNotificationsAsync(int userId, UserType userType)
    {
        var notifications = await db.Notifications
            .Where(n => n.UserId == userId && n.UserType == userType)
            .ToListAsync();

        if (notifications.Any())
        {
            db.Notifications.RemoveRange(notifications);
            await db.SaveChangesAsync();
        }
    }

    // ============================================
    // جلب عدد الإشعارات غير المقروءة
    // ============================================

    public async Task<int> GetUnreadCountAsync(int userId, UserType userType)
    {
        return await db.Notifications
            .CountAsync(n => n.UserId == userId && n.UserType == userType && !n.IsRead);
    }
}