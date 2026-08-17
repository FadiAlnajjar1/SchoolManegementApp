// Dtos/AnnouncementDto.cs
using System.ComponentModel.DataAnnotations;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Dtos;

public class AnnouncementDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public string? Audience { get; set; }
    public string? Type { get; set; }
    public string? CreatedBy { get; set; }
    public string Category { get; set; } = "announcement";
}
public class AnnouncementRequest
{
    [Required]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Body { get; set; } = string.Empty;
    
    [Required]
    public AnnouncementAudience Audience { get; set; }
    
    [Required]
    public AnnouncementType Type { get; set; }
    
    public DateTime? ExpiryDate { get; set; }  // ← تاريخ انتهاء العرض
}