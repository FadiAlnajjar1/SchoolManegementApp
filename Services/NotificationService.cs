using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Data;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Services;

public class NotificationService(AppDbContext db, ILogger<NotificationService> logger)
{
    public async Task SendAsync(int userId, UserType userType, string title, string body, string type = "general")
    {
        // 1. حفظ الإشعار في قاعدة البيانات
        db.Notifications.Add(new Models.Notification
        {
            UserId = userId,
            UserType = userType,
            Title = title,
            Body = body,
            Type = type,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // 2. جلب FCM Token
        var fcmToken = userType switch
        {
            UserType.Admin => (await db.Admins.FindAsync(userId))?.FcmToken,
            UserType.Employee => (await db.Employees.FindAsync(userId))?.FcmToken,
            UserType.Student => (await db.Students.FindAsync(userId))?.FcmToken,
            _ => null,
        };
        
        // 3. إرسال الإشعار الفوري
        await PushAsync(fcmToken, title, body);
    }

    public async Task SendToGuardianAsync(Student student, string title, string body, string type = "guardian")
    {
        // 1. حفظ الإشعار في قاعدة البيانات
        db.Notifications.Add(new Models.Notification
        {
            UserId = student.Id,
            UserType = UserType.Student,
            Title = title,
            Body = body,
            Type = type,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // 2. إرسال الإشعار الفوري لولي الأمر
        await PushAsync(student.GuardianFcmToken, title, body);
    }

    // ============================================
    // إرسال إشعار لمدير المدرسة
    // ============================================

    public async Task SendToSchoolManagersAsync(int schoolId, string title, string body, string type = "general")
    {
        // جلب جميع المديرين في المدرسة
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
            await SendAsync(manager.Id, UserType.Employee, title, body, type);
        }
    }

    // ============================================
    // إرسال إشعار لجميع الموظفين في مدرسة
    // ============================================

    public async Task SendToAllEmployeesInSchoolAsync(int schoolId, string title, string body, string type = "general")
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
            await SendAsync(employee.Id, UserType.Employee, title, body, type);
        }
    }

    // ============================================
    // إرسال إشعار لجميع الطلاب في مدرسة
    // ============================================

    public async Task SendToAllStudentsInSchoolAsync(int schoolId, string title, string body, string type = "general")
    {
        var students = await db.Students
            .Where(s => s.SchoolId == schoolId && s.IsActive)
            .ToListAsync();

        foreach (var student in students)
        {
            await SendAsync(student.Id, UserType.Student, title, body, type);
        }
    }

    // ============================================
    // إرسال إشعار لطلاب شعبة معينة
    // ============================================

    public async Task SendToSectionStudentsAsync(int sectionId, string title, string body, string type = "general")
    {
        var students = await db.Students
            .Where(s => s.SectionId == sectionId && s.IsActive)
            .ToListAsync();

        foreach (var student in students)
        {
            await SendAsync(student.Id, UserType.Student, title, body, type);
        }
    }

    // ============================================
    // إرسال إشعار للموجه (Counselor)
    // ============================================

    public async Task SendToCounselorAsync(int counselorId, string title, string body, string type = "general")
    {
        await SendAsync(counselorId, UserType.Employee, title, body, type);
    }

    // ============================================
    // إرسال إشعار لمجموعة من المستخدمين
    // ============================================

    public async Task SendToManyAsync(List<int> userIds, UserType userType, string title, string body, string type = "general")
    {
        foreach (var userId in userIds)
        {
            await SendAsync(userId, userType, title, body, type);
        }
    }

    // ============================================
    // دوال مساعدة
    // ============================================

    private async Task PushAsync(string? fcmToken, string title, string body)
    {
        if (!FirebaseInitializer.IsReady || string.IsNullOrWhiteSpace(fcmToken)) 
            return;

        try
        {
            await FirebaseMessaging.DefaultInstance.SendAsync(new Message
            {
                Token = fcmToken,
                Notification = new FirebaseAdmin.Messaging.Notification 
                { 
                    Title = title, 
                    Body = body 
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
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FCM push failed for token: {Token}", fcmToken);
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
    // تحديث حالة القراءة
    // ============================================

    public async Task MarkAsReadAsync(int notificationId, int userId)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        
        if (notification is not null)
        {
            notification.IsRead = true;
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
}