// Dtos/FeedItemDto.cs
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Dtos;

public class FeedItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;  // "announcement" أو "activity"
    public string? Type { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Audience { get; set; }
    public int? Capacity { get; set; }
    public string? CreatedBy { get; set; }
}
// Dtos/FeedResponseDto.cs

public class FeedResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public FeedDataDto Data { get; set; } = new();
}

public class FeedDataDto
{
    public List<AnnouncementDto> Announcements { get; set; } = new();
    public List<Activity> Activities { get; set; } = new();
    public List<FeedItemDto> Feed { get; set; } = new();
}