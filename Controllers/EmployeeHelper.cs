// Helpers/EmployeeHelper.cs
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Helpers;

public static class EmployeeHelper
{
    public static string GetRoleName(EmployeeRole role)
    {
        return role switch
        {
            EmployeeRole.Principal => "مدير المدرسة",
            EmployeeRole.Secretary => "أمين سر",
            EmployeeRole.Counselor => "موجه",
            EmployeeRole.Librarian => "أمين مكتبة",
            EmployeeRole.ActivitySupervisor => "مشرف نشاطات",
            EmployeeRole.Teacher => "معلم",
            _ => role.ToString()
        };
    }

    public static bool IsUniqueRole(EmployeeRole role)
    {
        return role == EmployeeRole.Principal ||
               role == EmployeeRole.Secretary ||
               role == EmployeeRole.Librarian ||
               role == EmployeeRole.ActivitySupervisor;
    }
}