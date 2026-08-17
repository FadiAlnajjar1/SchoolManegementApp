// DTO للمعلم
public class TeacherDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string Position { get; set; }
    public DateTime HireDate { get; set; }
    public bool IsActive { get; set; }
    public string SectionName { get; set; }
    public List<string> Subjects { get; set; }
}

// DTO للمدرسة
public class SchoolWithTeachersDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string TypeName { get; set; }
    public string Address { get; set; }
    public string Phone { get; set; }
    public DateTime CreatedAt { get; set; }
    public int EmployeesCount { get; set; }
    public int SectionsCount { get; set; }
    public int StudentsCount { get; set; }
    public List<TeacherDto> Teachers { get; set; }
}