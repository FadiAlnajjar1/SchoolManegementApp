// Services/AnnouncementCleanupService.cs
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Data;

namespace SchoolManagement.Api.Services;

public class AnnouncementCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AnnouncementCleanupService> _logger;

    public AnnouncementCleanupService(IServiceProvider serviceProvider, ILogger<AnnouncementCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredAnnouncementsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تنظيف الإعلانات المنتهية");
            }

            // التشغيل كل 6 ساعات
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task CleanupExpiredAnnouncementsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var now = DateTime.UtcNow;
        
        // حذف الإعلانات المنتهية
        var expiredAnnouncements = await db.Announcements
            .Where(a => a.ExpiryDate != null && a.ExpiryDate < now && a.IsActive)
            .ToListAsync();

        if (expiredAnnouncements.Any())
        {
            foreach (var announcement in expiredAnnouncements)
            {
                announcement.IsActive = false;
            }
            
            // أو حذفها نهائياً:
            // db.Announcements.RemoveRange(expiredAnnouncements);
            
            await db.SaveChangesAsync();
            _logger.LogInformation($"تم تعطيل {expiredAnnouncements.Count} إعلان منتهي");
        }
    }
}