// Models/OtpCode.cs
namespace SchoolManagement.Api.Models;

public class OtpCode
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public int Attempts { get; set; } = 0;
}