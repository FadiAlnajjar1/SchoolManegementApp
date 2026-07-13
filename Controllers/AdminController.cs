using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Auth;
using SchoolManagement.Api.Data;
using SchoolManagement.Api.Dtos;
using SchoolManagement.Api.Models;
using SchoolManagement.Api.Services;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)]
public class AdminController(
    AppDbContext db,
    SchoolRulesService rules,
    NotificationService notifier) : ControllerBase
{
    // ============================================
    // إدارة المدارس
    // ============================================

    [HttpPost("schools")]
    public async Task<IActionResult> CreateSchool(SchoolRequest request)
    {
        // 1. التحقق من وجود مدرسة بنفس الاسم
        var existingSchoolByName = await db.Schools
            .AnyAsync(s => s.Name == request.Name);

        if (existingSchoolByName)
            return BadRequest(new { message = "يوجد مدرسة بنفس الاسم بالفعل" });

        // 2. التحقق من وجود مدرسة بنفس رقم الهاتف
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            var existingSchoolByPhone = await db.Schools
                .AnyAsync(s => s.Phone == request.Phone);

            if (existingSchoolByPhone)
                return BadRequest(new { message = "رقم الهاتف مستخدم بالفعل من قبل مدرسة أخرى" });
        }

        // 3. الحصول على AdminId
        var admin = await db.Admins.FirstOrDefaultAsync();
        if (admin is null)
            return BadRequest(new { message = "لا يوجد مدير في النظام" });

        // 4. إنشاء المدرسة
        var school = new School
        {
            Name = request.Name,
            Type = request.Type,
            Address = request.Address ?? "",
            Phone = request.Phone ?? "",
            AdminId = admin.Id
        };

        db.Schools.Add(school);
        await db.SaveChangesAsync();

        // 5. إنشاء MarkConfig للمدرسة
        db.MarkConfigs.Add(new MarkConfig { SchoolId = school.Id });
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSchools), new { id = school.Id }, school);
    }

    [HttpGet("schools")]
    public async Task<IActionResult> GetSchools()
    {
        var schools = await db.Schools
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Type,
                TypeName = GetSchoolTypeName(s.Type),
                s.Address,
                s.Phone,
                s.CreatedAt,
                EmployeesCount = db.EmployeeSchools.Count(es => es.SchoolId == s.Id && es.IsActive),
                SectionsCount = db.Sections.Count(sec => sec.SchoolId == s.Id),
                StudentsCount = db.Students.Count(st => st.SchoolId == s.Id)
            })
            .ToListAsync();

        return Ok(schools);
    }

    [HttpGet("schools/{id:int}")]
    public async Task<IActionResult> GetSchool(int id)
    {
        var school = await db.Schools
            .Where(s => s.Id == id)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Type,
                TypeName = GetSchoolTypeName(s.Type),
                s.Address,
                s.Phone,
                s.CreatedAt,
                Employees = db.EmployeeSchools
                    .Where(es => es.SchoolId == s.Id && es.IsActive)
                    .Select(es => new
                    {
                        es.LocalEmployeeNumber,
                        es.EmployeeId,
                        EmployeeName = es.Employee != null ? es.Employee.Name : null,
                        es.Employee!.Email,
                        es.Role,
                        RoleName = GetRoleName(es.Role),
                        es.CreatedAt
                    })
                    .ToList(),
                Sections = db.Sections
                    .Where(sec => sec.SchoolId == s.Id)
                    .Select(sec => new
                    {
                        sec.Id,
                        sec.Name,
                        sec.GradeId,
                        GradeName = sec.Grade != null ? sec.Grade.Name : null
                    })
                    .ToList(),
                StudentsCount = db.Students.Count(st => st.SchoolId == s.Id)
            })
            .FirstOrDefaultAsync();

        if (school is null)
            return NotFound(new { message = "المدرسة غير موجودة" });

        return Ok(school);
    }

    [HttpPatch("schools/{id:int}")]
    public async Task<IActionResult> UpdateSchool(int id, SchoolRequest request)
    {
        var school = await db.Schools.FindAsync(id);
        if (school is null)
            return NotFound(new { message = "المدرسة غير موجودة" });

        // تحديث الاسم
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var existingSchoolByName = await db.Schools
                .AnyAsync(s => s.Name == request.Name && s.Id != id);

            if (existingSchoolByName)
                return BadRequest(new { message = "يوجد مدرسة بنفس الاسم بالفعل" });

            school.Name = request.Name;
        }

        // تحديث النوع
        school.Type = request.Type;

        // تحديث العنوان
        if (!string.IsNullOrWhiteSpace(request.Address))
            school.Address = request.Address;

        // تحديث رقم الهاتف
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            var existingSchoolByPhone = await db.Schools
                .AnyAsync(s => s.Phone == request.Phone && s.Id != id);

            if (existingSchoolByPhone)
                return BadRequest(new { message = "رقم الهاتف مستخدم بالفعل من قبل مدرسة أخرى" });

            school.Phone = request.Phone;
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "تم تحديث المدرسة بنجاح",
            school = new
            {
                school.Id,
                school.Name,
                school.Type,
                TypeName = GetSchoolTypeName(school.Type),
                school.Address,
                school.Phone,
                school.CreatedAt
            }
        });
    }

    [HttpDelete("schools/{id:int}")]
    public async Task<IActionResult> DeleteSchool(int id)
    {
        var school = await db.Schools
            .Include(s => s.EmployeeSchools)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (school is null)
            return NotFound(new { message = "المدرسة غير موجودة" });

        // 1. التحقق من وجود موظفين نشطين في المدرسة
        var activeEmployees = school.EmployeeSchools?
            .Where(es => es.IsActive)
            .ToList() ?? new List<EmployeeSchool>();

        if (activeEmployees.Any())
        {
            return BadRequest(new
            {
                message = "لا يمكن حذف المدرسة لأن بها موظفين نشطين",
                employees = activeEmployees.Select(es => new
                {
                    es.LocalEmployeeNumber,
                    es.EmployeeId,
                    EmployeeName = es.Employee != null ? es.Employee.Name : null,
                    es.Role,
                    RoleName = GetRoleName(es.Role)
                }).ToList()
            });
        }

        // 2. حذف EmployeeSchools (غير النشطة)
        var employeeSchools = await db.EmployeeSchools
            .Where(es => es.SchoolId == id)
            .ToListAsync();

        if (employeeSchools.Any())
            db.EmployeeSchools.RemoveRange(employeeSchools);

        // 3. حذف TeacherAssignments
        var teacherAssignments = await db.TeacherAssignments
            .Where(t => t.SchoolId == id)
            .ToListAsync();

        if (teacherAssignments.Any())
            db.TeacherAssignments.RemoveRange(teacherAssignments);

        // 4. حذف الشعب والطلاب
        var sections = await db.Sections
            .Where(s => s.SchoolId == id)
            .ToListAsync();

        foreach (var section in sections)
        {
            // حذف TeacherGrades المرتبطة
            var teacherGrades = await db.TeacherGrades
                .Where(tg => tg.SectionId == section.Id)
                .ToListAsync();

            if (teacherGrades.Any())
                db.TeacherGrades.RemoveRange(teacherGrades);

            // حذف الطلاب
            var students = await db.Students
                .Where(st => st.SectionId == section.Id)
                .ToListAsync();

            if (students.Any())
                db.Students.RemoveRange(students);
        }

        if (sections.Any())
            db.Sections.RemoveRange(sections);

        // 5. حذف المواد
        var subjects = await db.Subjects
            .Where(s => s.SchoolId == id)
            .ToListAsync();

        if (subjects.Any())
        {
            var subjectIds = subjects.Select(s => s.Id).ToList();
            var teacherSubjects = await db.TeacherSubjects
                .Where(t => subjectIds.Contains(t.SubjectId))
                .ToListAsync();

            if (teacherSubjects.Any())
                db.TeacherSubjects.RemoveRange(teacherSubjects);

            db.Subjects.RemoveRange(subjects);
        }

        // 6. حذف الصفوف
        var grades = await db.Grades
            .Where(g => g.SchoolId == id)
            .ToListAsync();

        if (grades.Any())
            db.Grades.RemoveRange(grades);

        // 7. حذف MarkConfigs
        var markConfigs = await db.MarkConfigs
            .Where(c => c.SchoolId == id)
            .ToListAsync();

        if (markConfigs.Any())
            db.MarkConfigs.RemoveRange(markConfigs);

        // 8. حذف الطلاب الذين ليس لديهم Section
        var studentsWithoutSection = await db.Students
            .Where(st => st.SchoolId == id && st.SectionId == null)
            .ToListAsync();

        if (studentsWithoutSection.Any())
            db.Students.RemoveRange(studentsWithoutSection);

        // 9. حذف المدرسة
        db.Schools.Remove(school);
        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "تم حذف المدرسة وجميع البيانات المرتبطة بنجاح",
            schoolId = id
        });
    }

    // ============================================
    // إدارة الموظفين (مع LocalEmployeeNumber)
    // ============================================

    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployee(EmployeeCreateRequest request)
    {
        // 1. التحقق من وجود المدرسة
        var school = await db.Schools.FindAsync(request.SchoolId);
        if (school is null)
            return BadRequest(new { message = "المدرسة غير موجودة" });

        // 2. التحقق من عدم وجود موظف بنفس الرقم الوطني
        var existingEmployee = await db.Employees
            .FirstOrDefaultAsync(e => e.NationalId == request.NationalId);

        Employee? employee;
        bool isNewEmployee = false;

        // 3. إذا كان الموظف موجوداً بالفعل
        if (existingEmployee is not null)
        {
            employee = existingEmployee;

            // التحقق من أن الإيميل مطابق
            // if (employee.Email != request.Email)
            //     return BadRequest(new { message = "الإيميل يجب أن يكون مطابقاً للإيميل المسجل" });

            // التحقق من أنه ليس مفصولاً
            if (employee.IsDismissed)
                return BadRequest(new { message = "هذا الموظف مفصول ولا يمكن إضافته" });
        }
        else
        {
            // 4. إنشاء موظف جديد
            employee = new Employee
            {
                Name = request.Name,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                NationalId = request.NationalId,
                Phone = request.Phone ?? "",
                Address = request.Address ?? "",
                BirthDate = request.BirthDate,
                Qualification = request.Qualification ?? "",
                Photo = request.Photo ?? "",
                CreatedAt = DateTime.UtcNow
            };

            db.Employees.Add(employee);
            isNewEmployee = true;
            await db.SaveChangesAsync();
        }

        // 5. التحقق من الأدوار الفريدة (على مستوى المدرسة)
        if (IsUniqueRole(request.Role))
        {
            var existingInSchool = await db.EmployeeSchools
                .AnyAsync(es => es.SchoolId == request.SchoolId &&
                               es.Role == request.Role &&
                               es.IsActive);

            if (existingInSchool)
                return BadRequest(new { message = $"الوظيفة '{GetRoleName(request.Role)}' مشغولة بالفعل في هذه المدرسة" });
        }

        // 6. التحقق من عدم وجود نفس الموظف في نفس المدرسة
        var existingInSameSchool = await db.EmployeeSchools
            .AnyAsync(es => es.EmployeeId == employee.Id &&
                           es.SchoolId == request.SchoolId &&
                           es.IsActive);

        if (existingInSameSchool)
            return BadRequest(new { message = "هذا الموظف موجود بالفعل في هذه المدرسة" });

        // 7. حساب الرقم المحلي الجديد (أول رقم غير مستخدم)
        var usedNumbers = await db.EmployeeSchools
            .Where(es => es.SchoolId == request.SchoolId)
            .Select(es => es.LocalEmployeeNumber)
            .ToListAsync();

        int newLocalNumber = 1;
        while (usedNumbers.Contains(newLocalNumber))
        {
            newLocalNumber++;
        }

        // 8. إضافة الموظف إلى المدرسة
        var employeeSchool = new EmployeeSchool
        {
            EmployeeId = employee.Id,
            SchoolId = request.SchoolId,
            LocalEmployeeNumber = newLocalNumber,
            Role = request.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.EmployeeSchools.Add(employeeSchool);

        // 9. إذا كان معلم، أضف TeacherAssignment
        if (request.Role == EmployeeRole.Teacher)
        {
            db.TeacherAssignments.Add(new TeacherAssignment
            {
                EmployeeId = employee.Id,
                SchoolId = request.SchoolId
            });
        }

        await db.SaveChangesAsync();

        return Created($"api/admin/schools/{request.SchoolId}/employees/{newLocalNumber}", new
        {
            message = isNewEmployee ? "تم إنشاء الموظف وربطه بالمدرسة بنجاح" : "تم ربط الموظف بالمدرسة بنجاح",
            employee = new
            {
                employee.Id,
                employee.Name,
                employee.Email,
                employee.NationalId,
                employee.Phone,
                employee.Address,
                employee.BirthDate,
                employee.Qualification,
                employee.CreatedAt,
                school = new
                {
                    school.Id,
                    school.Name,
                    localNumber = newLocalNumber,
                    Role = request.Role.ToString(),
                    RoleName = GetRoleName(request.Role)
                }
            }
        });
    }

    // [HttpGet("employees/{id:int}")]
    // public async Task<IActionResult> GetEmployee(int id)
    // {
    //     var employee = await db.Employees
    //         .Include(e => e.EmployeeSchools)
    //             .ThenInclude(es => es.School)
    //         .FirstOrDefaultAsync(e => e.Id == id);

    //     if (employee is null)
    //         return NotFound(new { message = "الموظف غير موجود" });

    //     return Ok(new
    //     {
    //         employee.Id,
    //         employee.Name,
    //         employee.Email,
    //         employee.NationalId,
    //         employee.Phone,
    //         employee.Address,
    //         employee.BirthDate,
    //         employee.Qualification,
    //         employee.CreatedAt,
    //         Schools = employee.EmployeeSchools
    //             .Where(es => es.IsActive)
    //             .Select(es => new
    //             {
    //                 es.SchoolId,
    //                 es.LocalEmployeeNumber,
    //                 SchoolName = es.School != null ? es.School.Name : null,
    //                 es.Role,
    //                 RoleName = GetRoleName(es.Role),
    //                 es.CreatedAt
    //             })
    //             .ToList()
    //     });
    // }

    [HttpGet("schools/{schoolId:int}/employees")]
    public async Task<IActionResult> GetSchoolEmployees(int schoolId)
    {
        var school = await db.Schools.FindAsync(schoolId);
        if (school is null)
            return NotFound(new { message = "المدرسة غير موجودة" });

        var employees = await db.EmployeeSchools
            .Include(es => es.Employee)
            .Where(es => es.SchoolId == schoolId && es.IsActive)
            .OrderBy(es => es.LocalEmployeeNumber)
            .Select(es => new
            {
                es.LocalEmployeeNumber,
                es.EmployeeId,
                es.Employee!.Name,
                es.Employee.Email,
                es.Employee.NationalId,
                es.Employee.Phone,
                es.Role,
                RoleName = GetRoleName(es.Role),
                es.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            schoolId = schoolId,
            schoolName = school.Name,
            totalEmployees = employees.Count,
            employees = employees
        });
    }

    [HttpGet("schools/{schoolId:int}/employees/{localNumber:int}")]
    public async Task<IActionResult> GetEmployeeByLocalNumber(int schoolId, int localNumber)
    {
        var employeeSchool = await db.EmployeeSchools
            .Include(es => es.Employee)
            .Include(es => es.School)
            .FirstOrDefaultAsync(es => es.SchoolId == schoolId &&
                                      es.LocalEmployeeNumber == localNumber &&
                                      es.IsActive);

        if (employeeSchool is null)
            return NotFound(new { message = $"لا يوجد موظف برقم {localNumber} في هذه المدرسة" });

        return Ok(new
        {
            schoolId = employeeSchool.SchoolId,
            schoolName = employeeSchool.School?.Name,
            localNumber = employeeSchool.LocalEmployeeNumber,
            employeeId = employeeSchool.EmployeeId,
            employeeName = employeeSchool.Employee?.Name,
            employeeEmail = employeeSchool.Employee?.Email,
            employeeNationalId = employeeSchool.Employee?.NationalId,
            role = employeeSchool.Role,
            roleName = GetRoleName(employeeSchool.Role),
            createdAt = employeeSchool.CreatedAt
        });
    }

    // [HttpPut("employees/{id:int}")]
    // public async Task<IActionResult> UpdateEmployee(int id, EmployeeUpdateRequest request)
    // {
    //     var employee = await db.Employees.FindAsync(id);
    //     if (employee is null)
    //         return NotFound(new { message = "الموظف غير موجود" });

    //     // 1. التحقق من عدم تكرار البريد الإلكتروني
    //     if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != employee.Email)
    //     {
    //         var existingEmail = await db.Employees
    //             .AnyAsync(e => e.Email == request.Email && e.Id != id);

    //         if (existingEmail)
    //             return BadRequest(new { message = "البريد الإلكتروني مستخدم بالفعل" });

    //         employee.Email = request.Email;
    //     }

    //     // 2. التحقق من عدم تكرار الرقم الوطني
    //     if (!string.IsNullOrWhiteSpace(request.NationalId) && request.NationalId != employee.NationalId)
    //     {
    //         var existingNationalId = await db.Employees
    //             .AnyAsync(e => e.NationalId == request.NationalId && e.Id != id);

    //         if (existingNationalId)
    //             return BadRequest(new { message = "الرقم الوطني مستخدم بالفعل" });

    //         employee.NationalId = request.NationalId;
    //     }

    //     // 3. التحقق من عدم تكرار رقم الهاتف
    //     if (!string.IsNullOrWhiteSpace(request.Phone) && request.Phone != employee.Phone)
    //     {
    //         var existingPhone = await db.Employees
    //             .AnyAsync(e => e.Phone == request.Phone && e.Id != id);

    //         if (existingPhone)
    //             return BadRequest(new { message = "رقم الهاتف مستخدم بالفعل" });

    //         employee.Phone = request.Phone;
    //     }

    //     // 4. تحديث باقي الحقول
    //     if (!string.IsNullOrWhiteSpace(request.Name))
    //         employee.Name = request.Name;

    //     if (!string.IsNullOrWhiteSpace(request.Address))
    //         employee.Address = request.Address;

    //     if (request.BirthDate.HasValue)
    //         employee.BirthDate = request.BirthDate;

    //     if (!string.IsNullOrWhiteSpace(request.Qualification))
    //         employee.Qualification = request.Qualification;

    //     if (!string.IsNullOrWhiteSpace(request.Password))
    //         employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

    //     await db.SaveChangesAsync();

    //     return Ok(new
    //     {
    //         message = "تم تحديث بيانات الموظف بنجاح",
    //         employee = new
    //         {
    //             employee.Id,
    //             employee.Name,
    //             employee.Email,
    //             employee.NationalId,
    //             employee.Phone,
    //             employee.Address,
    //             employee.BirthDate,
    //             employee.Qualification,
    //             employee.CreatedAt
    //         }
    //     });
    // }

    // [HttpDelete("employees/{id:int}")]
    // public async Task<IActionResult> DeleteEmployee(int id)
    // {
    //     var employee = await db.Employees
    //         .Include(e => e.EmployeeSchools)
    //         .FirstOrDefaultAsync(e => e.Id == id);

    //     if (employee is null)
    //         return NotFound(new { message = "الموظف غير موجود" });

    //     // 1. التحقق من أن الموظف ليس له علاقات نشطة
    //     var activeSchools = employee.EmployeeSchools?
    //         .Where(es => es.IsActive)
    //         .ToList() ?? new List<EmployeeSchool>();

    //     if (activeSchools.Any())
    //     {
    //         return BadRequest(new
    //         {
    //             message = "لا يمكن حذف الموظف لأنه يعمل في مدارس أخرى",
    //             schools = activeSchools.Select(es => new
    //             {
    //                 es.SchoolId,
    //                 es.LocalEmployeeNumber,
    //                 SchoolName = es.School != null ? es.School.Name : null,
    //                 es.Role,
    //                 RoleName = GetRoleName(es.Role)
    //             }).ToList()
    //         });
    //     }

    //     // 2. حذف البيانات المرتبطة
    //     var employeeAttendances = await db.EmployeeAttendances
    //         .Where(a => a.EmployeeId == id)
    //         .ToListAsync();

    //     if (employeeAttendances.Any())
    //         db.EmployeeAttendances.RemoveRange(employeeAttendances);

    //     var leaves = await db.Leaves
    //         .Where(l => l.EmployeeId == id)
    //         .ToListAsync();

    //     if (leaves.Any())
    //         db.Leaves.RemoveRange(leaves);

    //     var teacherAssignments = await db.TeacherAssignments
    //         .Where(t => t.EmployeeId == id)
    //         .ToListAsync();

    //     if (teacherAssignments.Any())
    //         db.TeacherAssignments.RemoveRange(teacherAssignments);

    //     var teacherSubjects = await db.TeacherSubjects
    //         .Where(t => t.TeacherId == id)
    //         .ToListAsync();

    //     if (teacherSubjects.Any())
    //         db.TeacherSubjects.RemoveRange(teacherSubjects);

    //     var teacherGrades = await db.TeacherGrades
    //         .Where(t => t.TeacherId == id)
    //         .ToListAsync();

    //     if (teacherGrades.Any())
    //         db.TeacherGrades.RemoveRange(teacherGrades);

    //     // 3. حذف EmployeeSchools (جميعها غير نشطة)
    //     var employeeSchools = await db.EmployeeSchools
    //         .Where(es => es.EmployeeId == id)
    //         .ToListAsync();

    //     if (employeeSchools.Any())
    //         db.EmployeeSchools.RemoveRange(employeeSchools);

    //     // 4. حذف الموظف
    //     db.Employees.Remove(employee);
    //     await db.SaveChangesAsync();

    //     return Ok(new
    //     {
    //         message = "تم حذف الموظف بنجاح",
    //         employeeId = id,
    //         deletedAttendances = employeeAttendances.Count,
    //         deletedLeaves = leaves.Count,
    //         deletedAssignments = teacherAssignments.Count
    //     });
    // }

    [HttpPut("employees/{id:int}/school/{schoolId:int}/role")]
    public async Task<IActionResult> UpdateEmployeeRole(int id, int schoolId, [FromBody] EmployeeRole newRole)
    {
        var employeeSchool = await db.EmployeeSchools
            .FirstOrDefaultAsync(es => es.EmployeeId == id && es.SchoolId == schoolId && es.IsActive);

        if (employeeSchool is null)
            return NotFound(new { message = "الموظف غير مرتبط بهذه المدرسة" });

        // التحقق من الأدوار الفريدة
        if (IsUniqueRole(newRole))
        {
            var existingRole = await db.EmployeeSchools
                .AnyAsync(es => es.Role == newRole && es.SchoolId == schoolId && es.EmployeeId != id && es.IsActive);

            if (existingRole)
                return BadRequest(new { message = $"الدور '{GetRoleName(newRole)}' مشغول بالفعل في هذه المدرسة" });
        }

        employeeSchool.Role = newRole;
        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "تم تحديث دور الموظف بنجاح",
            employeeId = id,
            schoolId = schoolId,
            localNumber = employeeSchool.LocalEmployeeNumber,
            newRole = newRole.ToString(),
            newRoleName = GetRoleName(newRole)
        });
    }
    // ============================================
// تعديل وحذف الموظفين باستخدام LocalEmployeeNumber
// ============================================

[HttpPut("schools/{schoolId:int}/employees/{localNumber:int}")]
public async Task<IActionResult> UpdateEmployeeByLocalNumber(
    int schoolId, 
    int localNumber, 
    EmployeeUpdateRequest request)
{
    // 1. البحث عن الموظف في المدرسة
    var employeeSchool = await db.EmployeeSchools
        .Include(es => es.Employee)
        .FirstOrDefaultAsync(es => es.SchoolId == schoolId &&
                                  es.LocalEmployeeNumber == localNumber &&
                                  es.IsActive);

    if (employeeSchool is null)
        return NotFound(new { message = $"لا يوجد موظف برقم {localNumber} في هذه المدرسة" });

    var employee = employeeSchool.Employee;
    if (employee is null)
        return NotFound(new { message = "الموظف غير موجود" });

    // 2. التحقق من عدم تكرار البريد الإلكتروني
    if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != employee.Email)
    {
        var existingEmail = await db.Employees
            .AnyAsync(e => e.Email == request.Email && e.Id != employee.Id);

        if (existingEmail)
            return BadRequest(new { message = "البريد الإلكتروني مستخدم بالفعل" });

        employee.Email = request.Email;
    }

    // 3. التحقق من عدم تكرار الرقم الوطني
    if (!string.IsNullOrWhiteSpace(request.NationalId) && request.NationalId != employee.NationalId)
    {
        var existingNationalId = await db.Employees
            .AnyAsync(e => e.NationalId == request.NationalId && e.Id != employee.Id);

        if (existingNationalId)
            return BadRequest(new { message = "الرقم الوطني مستخدم بالفعل" });

        employee.NationalId = request.NationalId;
    }

    // 4. التحقق من عدم تكرار رقم الهاتف
    if (!string.IsNullOrWhiteSpace(request.Phone) && request.Phone != employee.Phone)
    {
        var existingPhone = await db.Employees
            .AnyAsync(e => e.Phone == request.Phone && e.Id != employee.Id);

        if (existingPhone)
            return BadRequest(new { message = "رقم الهاتف مستخدم بالفعل" });

        employee.Phone = request.Phone;
    }

    // 5. تحديث باقي الحقول
    if (!string.IsNullOrWhiteSpace(request.Name))
        employee.Name = request.Name;

    if (!string.IsNullOrWhiteSpace(request.Address))
        employee.Address = request.Address;

    if (request.BirthDate.HasValue)
        employee.BirthDate = request.BirthDate;

    if (!string.IsNullOrWhiteSpace(request.Qualification))
        employee.Qualification = request.Qualification;

    if (!string.IsNullOrWhiteSpace(request.Password))
        employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

    // 6. تحديث الدور إذا تم إرساله
    if (request.Role.HasValue && request.Role.Value != employeeSchool.Role)
    {
        // التحقق من الأدوار الفريدة
        if (IsUniqueRole(request.Role.Value))
        {
            var existingRole = await db.EmployeeSchools
                .AnyAsync(es => es.Role == request.Role.Value &&
                               es.SchoolId == schoolId &&
                               es.EmployeeId != employee.Id &&
                               es.IsActive);

            if (existingRole)
                return BadRequest(new { message = $"الدور '{GetRoleName(request.Role.Value)}' مشغول بالفعل في هذه المدرسة" });
        }

        // إذا كان الدور الجديد معلم، أضف TeacherAssignment
        if (request.Role.Value == EmployeeRole.Teacher)
        {
            var existingAssignment = await db.TeacherAssignments
                .FirstOrDefaultAsync(t => t.EmployeeId == employee.Id && t.SchoolId == schoolId);

            if (existingAssignment is null)
            {
                db.TeacherAssignments.Add(new TeacherAssignment
                {
                    EmployeeId = employee.Id,
                    SchoolId = schoolId
                });
            }
        }
        else
        {
            // إذا لم يعد معلم، احذف TeacherAssignment
            var assignments = await db.TeacherAssignments
                .Where(t => t.EmployeeId == employee.Id && t.SchoolId == schoolId)
                .ToListAsync();

            if (assignments.Any())
                db.TeacherAssignments.RemoveRange(assignments);
        }

        employeeSchool.Role = request.Role.Value;
    }

    await db.SaveChangesAsync();

    return Ok(new
    {
        message = "تم تحديث بيانات الموظف بنجاح",
        employee = new
        {
            employee.Id,
            employee.Name,
            employee.Email,
            employee.NationalId,
            employee.Phone,
            employee.Address,
            employee.BirthDate,
            employee.Qualification,
            schoolId = schoolId,
            localNumber = localNumber,
            role = employeeSchool.Role.ToString(),
            roleName = GetRoleName(employeeSchool.Role),
            employee.CreatedAt
        }
    });
}

[HttpDelete("schools/{schoolId:int}/employees/{localNumber:int}")]
public async Task<IActionResult> DeleteEmployeeByLocalNumber(int schoolId, int localNumber)
{
    // 1. البحث عن الموظف في المدرسة
    var employeeSchool = await db.EmployeeSchools
        .Include(es => es.Employee)
        .FirstOrDefaultAsync(es => es.SchoolId == schoolId &&
                                  es.LocalEmployeeNumber == localNumber &&
                                  es.IsActive);

    if (employeeSchool is null)
        return NotFound(new { message = $"لا يوجد موظف برقم {localNumber} في هذه المدرسة" });

    var employee = employeeSchool.Employee;
    if (employee is null)
        return NotFound(new { message = "الموظف غير موجود" });

    // 2. التحقق من أن الموظف ليس مدير المدرسة
    if (employeeSchool.Role == EmployeeRole.Principal)
    {
        return BadRequest(new { message = "لا يمكن حذف مدير المدرسة. يمكنك نقله أو تغيير دوره أولاً" });
    }

    // 3. التحقق من أن الموظف ليس له علاقات نشطة في مدارس أخرى
    var activeInOtherSchools = await db.EmployeeSchools
        .AnyAsync(es => es.EmployeeId == employee.Id &&
                       es.SchoolId != schoolId &&
                       es.IsActive);

    if (activeInOtherSchools)
    {
        // إذا كان يعمل في مدارس أخرى، فقط قم بإلغاء الربط مع هذه المدرسة
        employeeSchool.IsActive = false;

        // حذف TeacherAssignment لهذه المدرسة فقط
        var assignments = await db.TeacherAssignments
            .Where(t => t.EmployeeId == employee.Id && t.SchoolId == schoolId)
            .ToListAsync();

        if (assignments.Any())
            db.TeacherAssignments.RemoveRange(assignments);

        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "تم إلغاء ربط الموظف بالمدرسة بنجاح (لا يزال يعمل في مدارس أخرى)",
            employeeId = employee.Id,
            employeeName = employee.Name,
            schoolId = schoolId,
            localNumber = localNumber,
            stillActiveInOtherSchools = true
        });
    }

    // 4. الموظف لا يعمل في مدارس أخرى → حذفه بالكامل
    // حذف البيانات المرتبطة
    var employeeAttendances = await db.EmployeeAttendances
        .Where(a => a.EmployeeId == employee.Id)
        .ToListAsync();

    if (employeeAttendances.Any())
        db.EmployeeAttendances.RemoveRange(employeeAttendances);

    var leaves = await db.Leaves
        .Where(l => l.EmployeeId == employee.Id)
        .ToListAsync();

    if (leaves.Any())
        db.Leaves.RemoveRange(leaves);

    var teacherAssignments = await db.TeacherAssignments
        .Where(t => t.EmployeeId == employee.Id)
        .ToListAsync();

    if (teacherAssignments.Any())
        db.TeacherAssignments.RemoveRange(teacherAssignments);

    var teacherSubjects = await db.TeacherSubjects
        .Where(t => t.TeacherId == employee.Id)
        .ToListAsync();

    if (teacherSubjects.Any())
        db.TeacherSubjects.RemoveRange(teacherSubjects);

    var teacherGrades = await db.TeacherGrades
        .Where(t => t.TeacherId == employee.Id)
        .ToListAsync();

    if (teacherGrades.Any())
        db.TeacherGrades.RemoveRange(teacherGrades);

    // حذف جميع EmployeeSchools
    var allEmployeeSchools = await db.EmployeeSchools
        .Where(es => es.EmployeeId == employee.Id)
        .ToListAsync();

    if (allEmployeeSchools.Any())
        db.EmployeeSchools.RemoveRange(allEmployeeSchools);

    // حذف الموظف
    db.Employees.Remove(employee);
    await db.SaveChangesAsync();

    return Ok(new
    {
        message = "تم حذف الموظف بنجاح",
        employeeId = employee.Id,
        employeeName = employee.Name,
        employeeEmail = employee.Email,
        schoolId = schoolId,
        localNumber = localNumber,
        deletedAttendances = employeeAttendances.Count,
        deletedLeaves = leaves.Count,
        deletedAssignments = teacherAssignments.Count,
        stillActiveInOtherSchools = false
    });
}

[HttpPatch("schools/{schoolId:int}/employees/{localNumber:int}/dismiss")]
public async Task<IActionResult> DismissEmployee(int schoolId, int localNumber)
{
    // 1. البحث عن الموظف في المدرسة
    var employeeSchool = await db.EmployeeSchools
        .Include(es => es.Employee)
        .FirstOrDefaultAsync(es => es.SchoolId == schoolId &&
                                  es.LocalEmployeeNumber == localNumber &&
                                  es.IsActive);

    if (employeeSchool is null)
        return NotFound(new { message = $"لا يوجد موظف برقم {localNumber} في هذه المدرسة" });

    var employee = employeeSchool.Employee;
    if (employee is null)
        return NotFound(new { message = "الموظف غير موجود" });

    // 2. التحقق من أنه ليس مدير المدرسة
    if (employeeSchool.Role == EmployeeRole.Principal)
    {
        return BadRequest(new { message = "لا يمكن فصل مدير المدرسة" });
    }

    // 3. فصل الموظف
    employee.IsDismissed = true;
    employeeSchool.IsActive = false;

    // 4. حذف TeacherAssignment لهذه المدرسة
    var assignments = await db.TeacherAssignments
        .Where(t => t.EmployeeId == employee.Id && t.SchoolId == schoolId)
        .ToListAsync();

    if (assignments.Any())
        db.TeacherAssignments.RemoveRange(assignments);

    await db.SaveChangesAsync();

    return Ok(new
    {
        message = "تم فصل الموظف بنجاح",
        employee = new
        {
            employee.Id,
            employee.Name,
            employee.Email,
            employee.NationalId,
            schoolId = schoolId,
            localNumber = localNumber,
            role = employeeSchool.Role.ToString(),
            roleName = GetRoleName(employeeSchool.Role),
            isDismissed = true,
            dismissedAt = DateTime.UtcNow
        }
    });
}

    // ============================================
    // نقل الطلاب والموظفين
    // ============================================

    [HttpPatch("transfer/student")]
    public async Task<IActionResult> TransferStudent(TransferRequest1 request)
    {
        var student = await db.Students
            .Include(s => s.School)
            .Include(s => s.Section)
                .ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId);

        if (student is null)
            return NotFound(new { message = "الطالب غير موجود" });

        if (student.SchoolId != request.CurrentSchoolId)
            return BadRequest(new
            {
                message = $"الطالب غير موجود في المدرسة المحددة (CurrentSchoolId: {request.CurrentSchoolId}). هو موجود في المدرسة: {student.SchoolId}"
            });

        var currentSchoolId = student.SchoolId;
        var currentSchoolName = student.School?.Name ?? "غير معروف";
        var currentGradeName = student.Section?.Grade?.Name ?? "غير معروف";
        var currentSectionName = student.Section?.Name ?? "غير معروف";

        var targetSchool = await db.Schools.FindAsync(request.NewSchoolId);
        if (targetSchool is null)
            return BadRequest(new { message = "المدرسة الجديدة غير موجودة" });

        if (request.NewSchoolId == currentSchoolId)
            return BadRequest(new { message = "لا يمكن النقل إلى نفس المدرسة" });

        if (request.GradeId.HasValue)
        {
            var gradeExists = await db.Grades
                .AnyAsync(g => g.Id == request.GradeId.Value && g.SchoolId == request.NewSchoolId);

            if (!gradeExists)
                return BadRequest(new { message = "الصف غير موجود في المدرسة الجديدة" });
        }

        student.SchoolId = request.NewSchoolId;

        if (request.GradeId.HasValue)
        {
            var sectionInNewGrade = await db.Sections
                .FirstOrDefaultAsync(s => s.GradeId == request.GradeId.Value &&
                                        s.SchoolId == request.NewSchoolId);

            student.SectionId = sectionInNewGrade?.Id;
        }
        else if (student.SectionId.HasValue)
        {
            var sectionExists = await db.Sections
                .AnyAsync(s => s.Id == student.SectionId.Value &&
                              s.SchoolId == request.NewSchoolId);

            if (!sectionExists)
            {
                var currentGradeId = student.Section?.GradeId;
                if (currentGradeId.HasValue)
                {
                    var newSection = await db.Sections
                        .FirstOrDefaultAsync(s => s.GradeId == currentGradeId.Value &&
                                                s.SchoolId == request.NewSchoolId);

                    student.SectionId = newSection?.Id;
                }
                else
                {
                    student.SectionId = null;
                }
            }
        }

        await db.SaveChangesAsync();

        var updatedStudent = await db.Students
            .Include(s => s.School)
            .Include(s => s.Section)
                .ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId);

        await notifier.SendAsync(
            student.Id,
            UserType.Student,
            "نقل مدرسي",
            $"تم نقلك من مدرسة '{currentSchoolName}' (الصف: {currentGradeName}) إلى مدرسة '{targetSchool.Name}'",
            "transfer"
        );

        return Ok(new
        {
            message = "تم نقل الطالب بنجاح",
            student = new
            {
                updatedStudent!.Id,
                updatedStudent.Name,
                updatedStudent.Email,
                previousSchool = new
                {
                    id = currentSchoolId,
                    name = currentSchoolName,
                    gradeName = currentGradeName,
                    sectionName = currentSectionName
                },
                newSchool = new
                {
                    id = request.NewSchoolId,
                    name = targetSchool.Name,
                    gradeId = request.GradeId ?? updatedStudent.Section?.GradeId,
                    gradeName = updatedStudent.Section?.Grade?.Name ?? "غير محدد",
                    sectionName = updatedStudent.Section?.Name ?? "غير محدد"
                }
            }
        });
    }

    [HttpPatch("transfer/employee")]
    public async Task<IActionResult> TransferEmployee(TransferEmployeeRequest request)
    {
        var employee = await db.Employees
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId);

        if (employee is null)
            return NotFound(new { message = "الموظف غير موجود" });

        var currentEmployeeSchool = await db.EmployeeSchools
            .Include(es => es.School)
            .FirstOrDefaultAsync(es => es.EmployeeId == request.EmployeeId &&
                                      es.SchoolId == request.CurrentSchoolId &&
                                      es.IsActive);

        if (currentEmployeeSchool is null)
            return BadRequest(new
            {
                message = $"الموظف غير موجود في المدرسة المحددة (CurrentSchoolId: {request.CurrentSchoolId})"
            });

        var currentSchoolId = currentEmployeeSchool.SchoolId;
        var currentSchoolName = currentEmployeeSchool.School?.Name ?? "غير معروف";
        var currentRole = currentEmployeeSchool.Role;
        var currentRoleName = GetRoleName(currentRole);
        var currentLocalNumber = currentEmployeeSchool.LocalEmployeeNumber;

        var targetSchool = await db.Schools.FindAsync(request.NewSchoolId);
        if (targetSchool is null)
            return BadRequest(new { message = "المدرسة الجديدة غير موجودة" });

        if (request.NewSchoolId == currentSchoolId)
            return BadRequest(new { message = "لا يمكن النقل إلى نفس المدرسة" });

        if (!Enum.IsDefined(typeof(EmployeeRole), request.NewRole))
            return BadRequest(new { message = "الوظيفة غير صالحة" });

        // التحقق من عدم وجود علاقة للموظف في المدرسة الجديدة
        var existingInNewSchool = await db.EmployeeSchools
            .AnyAsync(es => es.EmployeeId == request.EmployeeId &&
                           es.SchoolId == request.NewSchoolId &&
                           es.IsActive);

        if (existingInNewSchool)
            return BadRequest(new { message = "الموظف موجود بالفعل في المدرسة الجديدة" });

        // التحقق من الأدوار الفريدة في المدرسة الجديدة
        if (IsUniqueRole(request.NewRole))
        {
            var existingRole = await db.EmployeeSchools
                .AnyAsync(es => es.SchoolId == request.NewSchoolId &&
                               es.Role == request.NewRole &&
                               es.EmployeeId != request.EmployeeId &&
                               es.IsActive);

            if (existingRole)
                return BadRequest(new { message = $"الوظيفة '{GetRoleName(request.NewRole)}' مشغولة بالفعل في المدرسة الجديدة" });
        }

        // حساب الرقم المحلي الجديد
        var usedNumbers = await db.EmployeeSchools
            .Where(es => es.SchoolId == request.NewSchoolId)
            .Select(es => es.LocalEmployeeNumber)
            .ToListAsync();

        int newLocalNumber = 1;
        while (usedNumbers.Contains(newLocalNumber))
        {
            newLocalNumber++;
        }

        // تنفيذ النقل
        currentEmployeeSchool.IsActive = false;

        var newEmployeeSchool = new EmployeeSchool
        {
            EmployeeId = request.EmployeeId,
            SchoolId = request.NewSchoolId,
            LocalEmployeeNumber = newLocalNumber,
            Role = request.NewRole,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.EmployeeSchools.Add(newEmployeeSchool);

        // تحديث TeacherAssignment
        if (request.NewRole == EmployeeRole.Teacher)
        {
            var oldAssignment = await db.TeacherAssignments
                .FirstOrDefaultAsync(t => t.EmployeeId == request.EmployeeId && t.SchoolId == currentSchoolId);

            if (oldAssignment is not null)
                db.TeacherAssignments.Remove(oldAssignment);

            db.TeacherAssignments.Add(new TeacherAssignment
            {
                EmployeeId = request.EmployeeId,
                SchoolId = request.NewSchoolId
            });
        }
        else
        {
            var oldAssignments = await db.TeacherAssignments
                .Where(t => t.EmployeeId == request.EmployeeId && t.SchoolId == currentSchoolId)
                .ToListAsync();

            if (oldAssignments.Any())
                db.TeacherAssignments.RemoveRange(oldAssignments);
        }

        await db.SaveChangesAsync();

        await notifier.SendAsync(
            employee.Id,
            UserType.Employee,
            "نقل وظيفي",
            $"تم نقلك من مدرسة '{currentSchoolName}' (الوظيفة: {currentRoleName}) إلى مدرسة '{targetSchool.Name}' (الوظيفة: {GetRoleName(request.NewRole)})",
            "transfer"
        );

        return Ok(new
        {
            message = "تم نقل الموظف بنجاح",
            employee = new
            {
                employee.Id,
                employee.Name,
                employee.Email,
                employee.NationalId,
                previousSchool = new
                {
                    id = currentSchoolId,
                    name = currentSchoolName,
                    localNumber = currentLocalNumber,
                    role = currentRole.ToString(),
                    roleName = currentRoleName
                },
                newSchool = new
                {
                    id = request.NewSchoolId,
                    name = targetSchool.Name,
                    localNumber = newLocalNumber,
                    role = request.NewRole.ToString(),
                    roleName = GetRoleName(request.NewRole)
                }
            }
        });
    }

    // ============================================
    // دوال مساعدة
    // ============================================

    [HttpGet("roles")]
    public IActionResult GetRoles()
    {
        var roles = Enum.GetValues(typeof(EmployeeRole))
            .Cast<EmployeeRole>()
            .Select(r => new
            {
                Value = r.ToString(),
                Name = GetRoleName(r)
            })
            .ToList();

        return Ok(roles);
    }

    [HttpGet("school-types")]
    public IActionResult GetSchoolTypes()
    {
        var types = Enum.GetValues(typeof(SchoolType))
            .Cast<SchoolType>()
            .Select(t => new
            {
                Value = t.ToString(),
                Name = GetSchoolTypeName(t)
            })
            .ToList();

        return Ok(types);
    }

    private static string GetRoleName(EmployeeRole role)
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

    private bool IsUniqueRole(EmployeeRole role)
    {
        return role == EmployeeRole.Principal ||
               role == EmployeeRole.Secretary ||
               role == EmployeeRole.Librarian ||
               role == EmployeeRole.ActivitySupervisor;
    }

    private static string GetSchoolTypeName(SchoolType type)
    {
        return type switch
        {
            SchoolType.Primary => "ابتدائي",
            SchoolType.Preparatory => "إعدادي",
            SchoolType.Secondary => "ثانوي",
            SchoolType.PrimaryPreparatory => "ابتدائي وإعدادي",
            SchoolType.PreparatorySecondary => "إعدادي وثانوي",
            SchoolType.AllStages => "جميع المراحل",
            _ => type.ToString()
        };
    }
}