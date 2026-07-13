using System.Text.Json.Serialization;

namespace SchoolManagement.Api.Models;

public class TeacherSubject
{
    public int Id { get; set; }
    public int LocalTeacherSubjectId { get; set; }  // ✅ Local ID لعلاقة المعلم بالمادة
    public int TeacherId { get; set; }
    public int SubjectId { get; set; }
    public int SchoolId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    [JsonIgnore]
    public Employee? Teacher { get; set; }
    
    [JsonIgnore]
    public Subject? Subject { get; set; }
}