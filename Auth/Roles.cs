using System.Security.Claims;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Auth;


public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Principal";
    public const string Secretary = "Secretary";
    public const string Counselor = "Counselor";
    public const string Librarian = "Librarian";
    public const string ActivitySupervisor = "ActivitySupervisor";
    public const string Teacher = "Teacher";
    public const string Student = "Student";

    public static UserType GetUserType(this ClaimsPrincipal user)
    {
        var role = user.GetRole();
        
        return role switch
        {
            Roles.Admin => UserType.Admin,
            Roles.Student => UserType.Student,
            _ => UserType.Employee
        };
    }
    
}


public static class ClaimsExtensions
{
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static string GetRole(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Role)!;


    public static int GetSchoolId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue("schoolId"), out var id) ? id : 0;
}
