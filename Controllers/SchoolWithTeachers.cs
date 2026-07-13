using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Auth;
using SchoolManagement.Api.Data;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/schools")]
[Authorize]
public class SchoolWithTeachersController : ControllerBase
{
    private readonly AppDbContext _db;
    private int UserId => User.GetUserId();
    private UserType CurrentUserType => User.GetUserType();

    public SchoolWithTeachersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("School")]
    public async Task<IActionResult> GetSchools()
    {
        var schoolIds = await GetUserSchoolIdsAsync();
        
        if (!schoolIds.Any())
            return Unauthorized(new { message = "لا توجد مدارس مرتبطة بك" });

        var schools = await _db.Schools
            .Where(s => schoolIds.Contains(s.Id))
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Type,
                TypeName = GetSchoolTypeName(s.Type.ToString()),
                s.Address,
                s.Phone,
                s.CreatedAt,
                // ✅ إحصائيات
                Statistics = new
                {
                    EmployeesCount = _db.EmployeeSchools.Count(es => es.SchoolId == s.Id && es.IsActive),
                    TeachersCount = _db.EmployeeSchools.Count(es => es.SchoolId == s.Id && es.IsActive && es.Role == EmployeeRole.Teacher),
                    SectionsCount = _db.Sections.Count(sec => sec.SchoolId == s.Id),
                    StudentsCount = _db.Students.Count(st => st.SchoolId == s.Id),
                    SubjectsCount = _db.Subjects.Count(sub => sub.SchoolId == s.Id)
                },
                // ✅ قائمة الأساتذة
                Teachers = _db.EmployeeSchools
                    .Where(es => es.SchoolId == s.Id && es.IsActive && es.Role == EmployeeRole.Teacher)
                    .Select(es => new
                    {
                        es.EmployeeId,
                        es.Employee.Name,
                        es.Employee.Email,
                        es.Employee.Phone,
                        es.LocalEmployeeNumber,
                        es.Employee.NationalId,
                        es.Employee.Address,
                        es.Employee.BirthDate,
                        es.Employee.Qualification,
                        es.CreatedAt,
                        // ✅ الشعب التي يدرس فيها
                        Sections = _db.TeacherGrades
                            .Where(tg => tg.TeacherId == es.EmployeeId)
                            .Select(tg => new
                            {
                                tg.SectionId,
                                SectionName = tg.Section != null ? tg.Section.Name : null,
                                LocalSectionNumber = tg.Section != null ? tg.Section.LocalSectionNumber : 0,
                                GradeName = tg.Section != null && tg.Section.Grade != null ? tg.Section.Grade.Name : null,
                                LocalGradeNumber = tg.Section != null && tg.Section.Grade != null ? tg.Section.Grade.LocalGradeNumber : 0
                            })
                            .Distinct()
                            .ToList(),
                        // ✅ المواد التي يدرسها (مع استعلام فرعي)
                        Subjects = _db.TeacherSubjects
                            .Where(ts => ts.TeacherId == es.EmployeeId)
                            .Select(ts => new
                            {
                                ts.SubjectId,
                                SubjectName = ts.Subject != null ? ts.Subject.Name : null,
                                LocalSubjectId = _db.Subjects
                                    .Where(s => s.Id == ts.SubjectId)
                                    .Select(s => s.LocalSubjectId)
                                    .FirstOrDefault()
                            })
                            .Distinct()
                            .ToList()
                    })
                    .ToList()
            })
            .AsNoTracking()
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب المدارس مع الأساتذة بنجاح",
            data = new
            {
                userType = CurrentUserType.ToString(),
                schools = schools
            }
        });
    }
    // ============================================
    // دوال مساعدة
    // ============================================

    private async Task<List<int>> GetUserSchoolIdsAsync()
    {
        var userId = UserId;
        var userType = CurrentUserType;
        
        switch (userType)
        {
            case UserType.Student:
                var student = await _db.Students.FindAsync(userId);
                return student != null ? new List<int> { student.SchoolId } : new List<int>();

            case UserType.Employee:
                return await _db.EmployeeSchools
                    .Where(es => es.EmployeeId == userId && es.IsActive)
                    .Select(es => es.SchoolId)
                    .ToListAsync();

            case UserType.Admin:
                return await _db.Schools.Select(s => s.Id).ToListAsync();

            default:
                return new List<int>();
        }
    }

    private string GetSchoolTypeName(string type)
    {
        if (string.IsNullOrEmpty(type))
            return "غير محدد";

        return type switch
        {
            "Primary" => "ابتدائي",
            "Preparatory" => "إعدادي",
            "Secondary" => "ثانوي",
            "PrimaryPreparatory" => "ابتدائي وإعدادي",
            "PreparatorySecondary" => "إعدادي وثانوي",
            "AllStages" => "جميع المراحل",
            _ => type
        };
    }

    private string GetRoleName(EmployeeRole role)
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
}