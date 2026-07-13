using System.ComponentModel.DataAnnotations;
using SchoolManagement.Api.Models;

public record SchoolRequest(
    [Required] string Name,
    [Required] SchoolType Type,
    string? Address,
    string? Phone);
