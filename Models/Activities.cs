namespace SchoolManagement.Api.Models;


// Models/Activity.cs
public class Activity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int LocalActivityId { get; set; }
    public ActivityType Type { get; set; }
    public string Schedule { get; set; } = string.Empty;
    public string Description { get; set; } = " " ;
    public int Capacity { get; set; }
    public int? SupervisorId { get; set; }  // ← المشرف على النشاط
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public School? School { get; set; }
    public Employee? Supervisor { get; set; }  // ← العلاقة مع الموظف
    public ICollection<ActivityRegistration>? Registrations { get; set; }
}


public class ActivityRegistration
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public Activity? Activity { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
