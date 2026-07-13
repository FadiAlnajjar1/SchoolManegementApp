// Dtos/StudentResponse.cs
using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Api.Dtos;

public class StudentResponse
{
    public int Id { get; set; }
    public int LocalStudentNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int SchoolId { get; set; }
    public int? SectionId { get; set; }
    public string? SectionName { get; set; }
    public int SectionLocalNumber { get; set; }
    public int GradeLocalNumber { get; set; }
    public string? GradeName { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public string? BloodType { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Address { get; set; }
    public DateTime CreatedAt { get; set; }
}
// Dtos/StudentDTO.cs


public class StudentCreateRequest
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public DateTime? BirthDate { get; set; }
    public string? Address { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public string? BloodType { get; set; }
}

public class StudentUpdateRequesting
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public string? Address { get; set; }
    public string? BloodType { get; set; }
    public DateTime? BirthDate { get; set; }
    public int? LocalSectionNumber { get; set; }
}

public class StudentResponsing
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public int LocalStudentNumber { get; set; }
    public int SchoolId { get; set; }
    public int? SectionId { get; set; }
    public string? SectionName { get; set; }
    public int SectionLocalNumber { get; set; }
    public int GradeLocalNumber { get; set; }
    public string? GradeName { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public string? BloodType { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Address { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StudentUpdateRequestin
{
    public string? Name { get; set; }
    public int LocalStudentNumber { get; set; }
    
    [EmailAddress]
    public string? Email { get; set; }
    
    [MinLength(6)]
    public string? Password { get; set; }
    
    public int? SectionId { get; set; }
    
    public string? GuardianName { get; set; }
    
    public string? GuardianPhone { get; set; }
    
    public string? BloodType { get; set; }
    
    public string? ChronicDiseases { get; set; }
    
    public string? Allergies { get; set; }
    
    public string? HealthNotes { get; set; }
    
    public DateTime? BirthDate { get; set; }
    
    public string? Address { get; set; }
}
// Dtos/StudentCreateRequest.cs


public class StudentCreateRequestin
{
    [Required]
    public string Name { get; set; } = string.Empty;
    
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;
    
    public int? SectionId { get; set; }
    
    public string? GuardianName { get; set; }
    
    public string? GuardianPhone { get; set; }
    
    public string? BloodType { get; set; }
    
    public string? ChronicDiseases { get; set; }
    
    public string? Allergies { get; set; }
    
    public string? HealthNotes { get; set; }
    
    public DateTime? BirthDate { get; set; }
    
    public string? Address { get; set; }
}