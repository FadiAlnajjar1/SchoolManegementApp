// Dtos/SectionRequest.cs
using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Api.Dtos;

public class SectionRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public int? LocalCounselorId { get; set; }
}

public class SectionUpdateRequest
{
    public string Name { get; set; } = string.Empty;
    public int? LocalCounselorId { get; set; }
}