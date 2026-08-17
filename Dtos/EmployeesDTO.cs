using System.ComponentModel.DataAnnotations;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Dtos;

// ============= طلب إنشاء موظف =============
public record EmployeeCreateRequest(
    [Required] string Name,
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password,
    [Required] EmployeeRole Role,
    [Required, MinLength(10), MaxLength(14)] string NationalId,
    [Required] int SchoolId,  // ← أضفنا SchoolId
    string? Phone,
    string? Address,
    DateTime? BirthDate,
    string? Qualification,
    string? Photo
);

// ============= طلب تحديث موظف =============
public record EmployeeUpdateRequest(
    string? Name,
    [EmailAddress] string? Email,
    [MinLength(10), MaxLength(14)] string? NationalId,
    string? Phone,
    string? Address,
    DateTime? BirthDate,
    [MinLength(6)] string? Password,
    string? Qualification,
    EmployeeRole? Role  // ← يمكن تغيير الدور
);

// ============= طلب ربط موظف بمدرسة =============
public record AssignEmployeeToSchoolRequest(
    [Required] int EmployeeId,
    [Required] int SchoolId,
    [Required] EmployeeRole Role
);

// ============= طلب إلغاء ربط موظف من مدرسة =============
public record UnassignEmployeeFromSchoolRequest(
    [Required] int EmployeeId,
    [Required] int SchoolId
);

// ============= طلب تحديث دور موظف في مدرسة =============
public record UpdateEmployeeRoleRequest(
    [Required] int EmployeeId,
    [Required] int SchoolId,
    [Required] EmployeeRole Role
);