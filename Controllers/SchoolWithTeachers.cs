using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Data;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/schools")]
public class SchoolWithTeachersController : ControllerBase
{
    private readonly AppDbContext _db;

    public SchoolWithTeachersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetSchools()
    {
        // ✅ 1. جلب جميع المدارس
        var schools = await _db.Schools
            .AsNoTracking()
            .ToListAsync();

        var result = new List<object>();

        foreach (var school in schools)
        {
            // ✅ 2. جلب إحصائيات المدرسة
            var employeesCount = await _db.EmployeeSchools
                .CountAsync(es => es.SchoolId == school.Id && es.IsActive);

            var teachersCount = await _db.EmployeeSchools
                .CountAsync(es => es.SchoolId == school.Id && es.IsActive && es.Role == EmployeeRole.Teacher);

            var sectionsCount = await _db.Sections
                .CountAsync(sec => sec.SchoolId == school.Id);

            var studentsCount = await _db.Students
                .CountAsync(st => st.SchoolId == school.Id);

            var subjectsCount = await _db.Subjects
                .CountAsync(sub => sub.SchoolId == school.Id);

            // ✅ 3. جلب المعلمين مع تفاصيلهم
            var teacherSchools = await _db.EmployeeSchools
                .Where(es => es.SchoolId == school.Id && es.IsActive && es.Role == EmployeeRole.Teacher)
                .Include(es => es.Employee)
                .AsNoTracking()
                .ToListAsync();

            var teachers = new List<object>();

            foreach (var teacherSchool in teacherSchools)
            {
                var employee = teacherSchool.Employee;
                if (employee is null) continue;

                // ✅ 4. جلب الشعب التي يدرس فيها المعلم
                var sections = await _db.TeacherGrades
                    .Where(tg => tg.TeacherId == employee.Id)
                    .Include(tg => tg.Section)
                        .ThenInclude(s => s!.Grade)
                    .Select(tg => new
                    {
                        tg.SectionId,
                        SectionName = tg.Section != null ? tg.Section.Name : null,
                        LocalSectionNumber = tg.Section != null ? tg.Section.LocalSectionNumber : 0,
                        GradeName = tg.Section != null && tg.Section.Grade != null ? tg.Section.Grade.Name : null,
                        LocalGradeNumber = tg.Section != null && tg.Section.Grade != null ? tg.Section.Grade.LocalGradeNumber : 0
                    })
                    .Distinct()
                    .AsNoTracking()
                    .ToListAsync();

                // ✅ 5. جلب المواد التي يدرسها المعلم
                var subjects = await _db.TeacherSubjects
                    .Where(ts => ts.TeacherId == employee.Id)
                    .Include(ts => ts.Subject)
                    .Select(ts => new
                    {
                        ts.SubjectId,
                        SubjectName = ts.Subject != null ? ts.Subject.Name : null,
                        LocalSubjectId = ts.Subject != null ? ts.Subject.LocalSubjectId : 0
                    })
                    .Distinct()
                    .AsNoTracking()
                    .ToListAsync();

                teachers.Add(new
                {
                    teacherSchool.EmployeeId,
                    employee.Name,
                    employee.Email,
                    employee.Phone,
                    teacherSchool.LocalEmployeeNumber,
                    employee.NationalId,
                    employee.Address,
                    employee.BirthDate,
                    employee.Qualification,
                    employee.CreatedAt,
                    Sections = sections,
                    Subjects = subjects
                });
            }

            // ✅ 6. تجميع بيانات المدرسة
            result.Add(new
            {
                school.Id,
                school.Name,
                TypeName = GetSchoolTypeName(school.Type.ToString()),
                school.Address,
                school.Phone,
                school.CreatedAt,
                Statistics = new
                {
                    EmployeesCount = employeesCount,
                    TeachersCount = teachersCount,
                    SectionsCount = sectionsCount,
                    StudentsCount = studentsCount,
                    SubjectsCount = subjectsCount
                },
                Teachers = teachers
            });
        }

        return Ok(new
        {
            success = true,
            message = "تم جلب المدارس مع الأساتذة بنجاح",
            data = result
        });
    }

    // ============================================
    // دوال مساعدة
    // ============================================

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
}