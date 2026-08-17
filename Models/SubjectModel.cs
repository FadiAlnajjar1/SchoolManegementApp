using System.Text.Json.Serialization;
using SchoolManagement.Api.Models;

public class Subject
{
    public int Id { get; set; }
    public int LocalSubjectId { get; set; }
    public string Name { get; set; } = "";
    // public int GradeId { get; set; }
    public int? TeacherId { get; set; }
    public int SchoolId { get; set; }
    public Grade? Grade { get; set; }
    public School? School { get; set;}
    public Employee? Teacher { get; set; }
    
    [JsonIgnore]
    public ICollection<TeacherSubject>? TeacherSubjects { get; set; }
    public ICollection<TeacherGrade>? TeacherGrades { get; set; }
}