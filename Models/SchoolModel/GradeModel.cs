// Models/Grade.cs
public class Grade
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string Name { get; set; } = string.Empty; // "الصف الأول", "الصف الثاني", ...
    public int Level { get; set; }
    public int LocalGradeNumber { get; set; }  // ← الرقم المحلي للصف داخل المدرسة
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public School? School { get; set; }
    public ICollection<Section>? Sections { get; set; }
}