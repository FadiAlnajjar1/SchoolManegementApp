namespace SchoolManagement.Api.Models;

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public UserType UserType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Type { get; set; } = "general";
    public string? ActionUrl { get; set; }  // ✅ رابط الصفحة المرتبطة
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }   // ✅ وقت القراءة
}