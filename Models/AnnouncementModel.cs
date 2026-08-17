// Models/Announcement.cs
namespace SchoolManagement.Api.Models;

// Models/AnnouncementModel.cs
public class Announcement
{
    public int Id { get; set; }
    public int LocalAnnouncementId { get; set; }  // ✅ Local ID للإعلان
    public int SchoolId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public AnnouncementType Type { get; set; }
    public AnnouncementAudience Audience { get; set; }
    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;

    // العلاقات
    public School? School { get; set; }
    public Employee? CreatedBy { get; set; }
}