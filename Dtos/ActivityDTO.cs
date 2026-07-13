// Dtos/ActivityDto.cs
namespace SchoolManagement.Api.Dtos;

public class ActivityDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string? Type { get; set; }
    public int? Capacity { get; set; }
    public int? SupervisorId { get; set; }
    public string Category { get; set; } = "activity";
}