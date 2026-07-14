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
[Route("api/manager")]
[Authorize(Roles = Roles.Manager)]
public class ManagerController(
    AppDbContext db,
    NotificationService notifier,
    ReportCardService reportCards,
    PromotionService promotionService) : ControllerBase
{
    private int SchoolId => User.GetSchoolId();

    // ============================================
    // إدارة الصفوف (Grades) - باستخدام LocalGradeNumber
    // ============================================

    [HttpPost("grades")]
    public async Task<IActionResult> CreateGrade(GradeRequest request)
    {
        var school = await db.Schools.FindAsync(SchoolId);
        if (school is null)
            return BadRequest(new { message = "المدرسة غير موجودة" });

        var existingGrade = await db.Grades
            .AnyAsync(g => g.Name == request.Name && g.SchoolId == SchoolId);

        if (existingGrade)
            return BadRequest(new { message = $"الصف '{request.Name}' موجود بالفعل" });

        var usedNumbers = await db.Grades
            .Where(g => g.SchoolId == SchoolId)
            .Select(g => g.LocalGradeNumber)
            .ToListAsync();

        int newLocalNumber = 1;
        while (usedNumbers.Contains(newLocalNumber))
        {
            newLocalNumber++;
        }

        var grade = new Grade
        {
            SchoolId = SchoolId,
            Name = request.Name,
            LocalGradeNumber = newLocalNumber,
            AcademicYear = DateTime.Now.Year
        };

        db.Grades.Add(grade);
        await db.SaveChangesAsync();

        return Created($"api/manager/grades/{newLocalNumber}", new
        {
            message = "تم إنشاء الصف بنجاح",
            grade = new
            {
                grade.Id,
                grade.Name,
                grade.LocalGradeNumber,
                grade.AcademicYear,
                grade.SchoolId,
                SchoolName = school.Name
            }
        });
    }

    [HttpGet("grades")]
    public async Task<IActionResult> GetGrades()
    {
        var grades = await db.Grades
            .Include(g => g.Sections)
            .Where(g => g.SchoolId == SchoolId)
            .OrderBy(g => g.LocalGradeNumber)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.LocalGradeNumber,
                g.AcademicYear,
                g.SchoolId,
                Sections = g.Sections
                    .OrderBy(s => s.LocalSectionNumber)
                    .Select(s => new
                    {
                        s.Id,
                        s.Name,
                        s.LocalSectionNumber,
                        s.CounselorId,
                        LocalCounselorNumber = db.EmployeeSchools
                            .Where(es => es.EmployeeId == s.CounselorId && 
                                         es.SchoolId == SchoolId && 
                                         es.IsActive)
                            .Select(es => (int?)es.LocalEmployeeNumber)
                            .FirstOrDefault(),
                        CounselorName = s.Counselor != null ? s.Counselor.Name : null,
                        Teachers = db.TeacherGrades
                            .Where(tg => tg.SectionId == s.Id)
                            .Select(tg => new
                            {
                                tg.TeacherId,
                                TeacherName = tg.Teacher!.Name,
                                LocalTeacherNumber = db.EmployeeSchools
                                    .Where(es => es.EmployeeId == tg.TeacherId && 
                                                 es.SchoolId == SchoolId && 
                                                 es.IsActive)
                                    .Select(es => (int?)es.LocalEmployeeNumber)
                                    .FirstOrDefault(),
                                tg.SubjectId,
                                LocalSubjectId = db.Subjects
                                    .Where(sub => sub.Id == tg.SubjectId)
                                    .Select(sub => sub.LocalSubjectId)
                                    .FirstOrDefault(),
                                SubjectName = tg.Subject!.Name
                            })
                            .ToList()
                    }).ToList()
            })
            .ToListAsync();

        return Ok(grades);
    }

    [HttpGet("grades/{localGradeNumber:int}")]
    public async Task<IActionResult> GetGrade(int localGradeNumber)
    {
        var grade = await db.Grades
            .Include(g => g.Sections)
                .ThenInclude(s => s.TeacherGrades)
                    .ThenInclude(tg => tg.Teacher)
            .Include(g => g.Sections)
                .ThenInclude(s => s.TeacherGrades)
                    .ThenInclude(tg => tg.Subject)
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.LocalGradeNumber == localGradeNumber);

        if (grade is null)
            return NotFound(new { message = $"لا يوجد صف برقم {localGradeNumber} في هذه المدرسة" });

        var result = new
        {
            grade.Id,
            grade.Name,
            grade.LocalGradeNumber,
            grade.AcademicYear,
            grade.SchoolId,
            Sections = grade.Sections
                .OrderBy(s => s.LocalSectionNumber)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.LocalSectionNumber,
                    s.CounselorId,
                    LocalCounselorNumber = db.EmployeeSchools
                        .Where(es => es.EmployeeId == s.CounselorId && 
                                     es.SchoolId == SchoolId && 
                                     es.IsActive)
                        .Select(es => (int?)es.LocalEmployeeNumber)
                        .FirstOrDefault(),
                    CounselorName = s.Counselor != null ? s.Counselor.Name : null,
                    Teachers = s.TeacherGrades.Select(tg => new
                    {
                        tg.TeacherId,
                        TeacherName = tg.Teacher!.Name,
                        LocalTeacherNumber = db.EmployeeSchools
                            .Where(es => es.EmployeeId == tg.TeacherId && 
                                         es.SchoolId == SchoolId && 
                                         es.IsActive)
                            .Select(es => (int?)es.LocalEmployeeNumber)
                            .FirstOrDefault(),
                        tg.SubjectId,
                        LocalSubjectId = db.Subjects
                            .Where(sub => sub.Id == tg.SubjectId)
                            .Select(sub => sub.LocalSubjectId)
                            .FirstOrDefault(),
                        SubjectName = tg.Subject!.Name,
                        tg.CreatedAt
                    }).ToList()
                }).ToList()
        };

        return Ok(result);
    }

    [HttpPut("grades/{localGradeNumber:int}")]
    public async Task<IActionResult> UpdateGrade(int localGradeNumber, GradeRequest request)
    {
        var grade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.LocalGradeNumber == localGradeNumber);

        if (grade is null)
            return NotFound(new { message = $"لا يوجد صف برقم {localGradeNumber} في هذه المدرسة" });

        var existingGrade = await db.Grades
            .AnyAsync(g => g.Name == request.Name && 
                           g.SchoolId == SchoolId && 
                           g.LocalGradeNumber != localGradeNumber);

        if (existingGrade)
            return BadRequest(new { message = $"الصف '{request.Name}' موجود بالفعل في هذه المدرسة" });

        grade.Name = request.Name;
        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "تم تحديث اسم الصف بنجاح",
            grade = new
            {
                grade.Id,
                grade.Name,
                grade.LocalGradeNumber,
                grade.AcademicYear,
                grade.SchoolId
            }
        });
    }

    [HttpDelete("grades/{localGradeNumber:int}")]
    public async Task<IActionResult> DeleteGrade(int localGradeNumber)
    {
        var grade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.LocalGradeNumber == localGradeNumber);

        if (grade is null)
            return NotFound(new { message = $"لا يوجد صف برقم {localGradeNumber} في هذه المدرسة" });

        var sections = await db.Sections
            .Include(s => s.TeacherGrades)
            .Include(s => s.Students)
            .Where(s => s.GradeId == grade.Id)
            .ToListAsync();

        var allTeacherGrades = sections
            .SelectMany(s => s.TeacherGrades ?? new List<TeacherGrade>())
            .ToList();

        if (allTeacherGrades.Any())
            db.TeacherGrades.RemoveRange(allTeacherGrades);

        var subjectIds = allTeacherGrades
            .Select(tg => tg.SubjectId)
            .Distinct()
            .ToList();

        foreach (var subjectId in subjectIds)
        {
            var hasOtherGrades = await db.TeacherGrades
                .AnyAsync(tg => tg.SubjectId == subjectId);

            if (!hasOtherGrades)
            {
                var subject = await db.Subjects
                    .FirstOrDefaultAsync(s => s.Id == subjectId && s.SchoolId == SchoolId);

                if (subject is not null)
                {
                    var teacherSubjects = await db.TeacherSubjects
                        .Where(ts => ts.SubjectId == subjectId)
                        .ToListAsync();

                    if (teacherSubjects.Any())
                        db.TeacherSubjects.RemoveRange(teacherSubjects);

                    db.Subjects.Remove(subject);
                }
            }
        }

        foreach (var section in sections)
        {
            if (section.Students != null && section.Students.Any())
                db.Students.RemoveRange(section.Students);
        }

        if (sections.Any())
            db.Sections.RemoveRange(sections);

        db.Grades.Remove(grade);
        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "تم حذف الصف وجميع البيانات المرتبطة بنجاح",
            localGradeNumber = localGradeNumber,
            gradeName = grade.Name,
            deletedSections = sections.Count,
            deletedTeacherGrades = allTeacherGrades.Count,
            deletedSubjects = subjectIds.Count
        });
    }

    // ============================================
    // إدارة الشعب (Sections) - باستخدام Local IDs
    // ============================================

    [HttpPost("grades/{localGradeNumber:int}/sections")]
    public async Task<IActionResult> CreateSection(int localGradeNumber, SectionRequest request)
    {
        var grade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.LocalGradeNumber == localGradeNumber);

        if (grade is null)
            return BadRequest(new { message = $"لا يوجد صف برقم {localGradeNumber} في هذه المدرسة" });

        var existingSection = await db.Sections
            .AnyAsync(s => s.Name == request.Name && s.GradeId == grade.Id && s.SchoolId == SchoolId);

        if (existingSection)
            return BadRequest(new { message = $"الشعبة '{request.Name}' موجودة بالفعل في هذا الصف" });

        int? counselorId = null;
        if (request.LocalCounselorId.HasValue)
        {
            var counselorSchool = await db.EmployeeSchools
                .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                           es.LocalEmployeeNumber == request.LocalCounselorId.Value &&
                                           es.Role == EmployeeRole.Counselor &&
                                           es.IsActive);

            if (counselorSchool is null)
                return BadRequest(new { message = $"لا يوجد موجه برقم {request.LocalCounselorId.Value} في هذه المدرسة" });

            counselorId = counselorSchool.EmployeeId;
        }

        var usedNumbers = await db.Sections
            .Where(s => s.GradeId == grade.Id)
            .Select(s => s.LocalSectionNumber)
            .ToListAsync();

        int newLocalNumber = 1;
        while (usedNumbers.Contains(newLocalNumber))
        {
            newLocalNumber++;
        }

        var section = new Section
        {
            Name = request.Name,
            GradeId = grade.Id,
            SchoolId = SchoolId,
            CounselorId = counselorId,
            LocalSectionNumber = newLocalNumber
        };

        db.Sections.Add(section);
        await db.SaveChangesAsync();

        string? counselorName = null;
        if (counselorId.HasValue)
        {
            var counselor = await db.Employees.FindAsync(counselorId.Value);
            counselorName = counselor?.Name;
        }

        return Created($"api/manager/grades/{localGradeNumber}/sections/{newLocalNumber}", new
        {
            message = "تم إنشاء الشعبة بنجاح",
            section = new
            {
                section.Id,
                section.Name,
                section.LocalSectionNumber,
                section.GradeId,
                GradeName = grade.Name,
                LocalGradeNumber = localGradeNumber,
                section.SchoolId,
                section.CounselorId,
                LocalCounselorId = request.LocalCounselorId,
                CounselorName = counselorName
            }
        });
    }

    [HttpGet("grades/{localGradeNumber:int}/sections")]
    public async Task<IActionResult> GetSectionsByGrade(int localGradeNumber)
    {
        var grade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.LocalGradeNumber == localGradeNumber);

        if (grade is null)
            return NotFound(new { message = $"لا يوجد صف برقم {localGradeNumber} في هذه المدرسة" });

        var sections = await db.Sections
            .Where(s => s.GradeId == grade.Id && s.SchoolId == SchoolId)
            .OrderBy(s => s.LocalSectionNumber)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.LocalSectionNumber,
                GradeId = grade.Id,
                GradeName = grade.Name,
                LocalGradeNumber = grade.LocalGradeNumber,
                s.CounselorId,
                LocalCounselorNumber = db.EmployeeSchools
                    .Where(es => es.EmployeeId == s.CounselorId && 
                                 es.SchoolId == SchoolId && 
                                 es.IsActive)
                    .Select(es => (int?)es.LocalEmployeeNumber)
                    .FirstOrDefault(),
                CounselorName = s.Counselor != null ? s.Counselor.Name : null,
                s.CreatedAt,
                StudentsCount = db.Students.Count(st => st.SectionId == s.Id && st.IsActive)
            })
            .ToListAsync();

        return Ok(new
        {
            localGradeNumber = localGradeNumber,
            gradeName = grade.Name,
            sections = sections
        });
    }

    [HttpGet("grades/{localGradeNumber:int}/sections/{localSectionNumber:int}")]
    public async Task<IActionResult> GetSection(int localGradeNumber, int localSectionNumber)
    {
        var grade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.LocalGradeNumber == localGradeNumber);

        if (grade is null)
            return NotFound(new { message = $"لا يوجد صف برقم {localGradeNumber} في هذه المدرسة" });

        var section = await db.Sections
            .Include(s => s.Grade)
            .Include(s => s.Counselor)
            .Include(s => s.TeacherGrades)
                .ThenInclude(tg => tg.Teacher)
            .Include(s => s.TeacherGrades)
                .ThenInclude(tg => tg.Subject)
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.GradeId == grade.Id &&
                                      s.LocalSectionNumber == localSectionNumber);

        if (section is null)
            return NotFound(new { message = $"لا توجد شعبة برقم {localSectionNumber} في الصف {localGradeNumber}" });

        return Ok(new
        {
            section.Id,
            section.Name,
            section.LocalSectionNumber,
            section.SchoolId,
            section.CreatedAt,
            GradeId = section.GradeId,
            GradeName = section.Grade?.Name,
            LocalGradeNumber = section.Grade != null ? section.Grade.LocalGradeNumber : (int?)null,
            AcademicYear = section.Grade != null ? section.Grade.AcademicYear : (int?)null,
            CounselorId = section.CounselorId,
            LocalCounselorNumber = db.EmployeeSchools
                .Where(es => es.EmployeeId == section.CounselorId && 
                             es.SchoolId == SchoolId && 
                             es.IsActive)
                .Select(es => (int?)es.LocalEmployeeNumber)
                .FirstOrDefault(),
            CounselorName = section.Counselor != null ? section.Counselor.Name : null,
            Teachers = section.TeacherGrades.Select(tg => new
            {
                tg.TeacherId,
                TeacherName = tg.Teacher != null ? tg.Teacher.Name : null,
                LocalTeacherNumber = db.EmployeeSchools
                    .Where(es => es.EmployeeId == tg.TeacherId && 
                                 es.SchoolId == SchoolId && 
                                 es.IsActive)
                    .Select(es => (int?)es.LocalEmployeeNumber)
                    .FirstOrDefault(),
                tg.SubjectId,
                LocalSubjectId = db.Subjects
                    .Where(sub => sub.Id == tg.SubjectId)
                    .Select(sub => sub.LocalSubjectId)
                    .FirstOrDefault(),
                SubjectName = tg.Subject != null ? tg.Subject.Name : null,
                CreatedAt = tg.CreatedAt
            }).ToList()
        });
    }

    [HttpPut("grades/{localGradeNumber:int}/sections/{localSectionNumber:int}")]
    public async Task<IActionResult> UpdateSection(int localGradeNumber, int localSectionNumber, SectionUpdateRequest request)
    {
        var grade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.LocalGradeNumber == localGradeNumber);

        if (grade is null)
            return NotFound(new { message = $"لا يوجد صف برقم {localGradeNumber} في هذه المدرسة" });

        var section = await db.Sections
            .Include(s => s.Grade)
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.GradeId == grade.Id &&
                                      s.LocalSectionNumber == localSectionNumber);

        if (section is null)
            return NotFound(new { message = $"لا توجد شعبة برقم {localSectionNumber} في الصف {localGradeNumber}" });

        var existingSection = await db.Sections
            .AnyAsync(s => s.Name == request.Name && 
                           s.GradeId == section.GradeId && 
                           s.SchoolId == SchoolId && 
                           s.LocalSectionNumber != localSectionNumber);

        if (existingSection)
            return BadRequest(new { message = $"الشعبة '{request.Name}' موجودة بالفعل في هذا الصف" });

        section.Name = request.Name;

        if (request.LocalCounselorId.HasValue)
        {
            var counselorSchool = await db.EmployeeSchools
                .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                           es.LocalEmployeeNumber == request.LocalCounselorId.Value &&
                                           es.Role == EmployeeRole.Counselor &&
                                           es.IsActive);

            if (counselorSchool is null)
                return BadRequest(new { message = $"لا يوجد موجه برقم {request.LocalCounselorId.Value} في هذه المدرسة" });

            section.CounselorId = counselorSchool.EmployeeId;
        }
        else
        {
            section.CounselorId = null;
        }

        await db.SaveChangesAsync();

        string? counselorName = null;
        if (section.CounselorId.HasValue)
        {
            var counselor = await db.Employees.FindAsync(section.CounselorId.Value);
            counselorName = counselor?.Name;
        }

        return Ok(new
        {
            message = "تم تحديث الشعبة بنجاح",
            section = new
            {
                section.Id,
                section.Name,
                section.LocalSectionNumber,
                section.GradeId,
                GradeName = section.Grade?.Name,
                LocalGradeNumber = section.Grade != null ? section.Grade.LocalGradeNumber : (int?)null,
                section.SchoolId,
                section.CounselorId,
                LocalCounselorId = request.LocalCounselorId,
                CounselorName = counselorName
            }
        });
    }

    [HttpDelete("grades/{localGradeNumber:int}/sections/{localSectionNumber:int}")]
    public async Task<IActionResult> DeleteSection(int localGradeNumber, int localSectionNumber)
    {
        var grade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.LocalGradeNumber == localGradeNumber);

        if (grade is null)
            return NotFound(new { message = $"لا يوجد صف برقم {localGradeNumber} في هذه المدرسة" });

        var section = await db.Sections
            .Include(s => s.Students)
            .Include(s => s.TeacherGrades)
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.GradeId == grade.Id &&
                                      s.LocalSectionNumber == localSectionNumber);

        if (section is null)
            return NotFound(new { message = $"لا توجد شعبة برقم {localSectionNumber} في الصف {localGradeNumber}" });

        if (section.TeacherGrades.Any())
            db.TeacherGrades.RemoveRange(section.TeacherGrades);

        if (section.Students.Any())
            db.Students.RemoveRange(section.Students);

        db.Sections.Remove(section);
        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "تم حذف الشعبة وجميع البيانات المرتبطة بنجاح",
            localGradeNumber = localGradeNumber,
            localSectionNumber = localSectionNumber,
            sectionName = section.Name,
            gradeName = grade.Name
        });
    }

    // ============================================
    // إدارة المواد (Subjects) - باستخدام LocalSubjectId
    // ============================================

    [HttpPost("subjects")]
    public async Task<IActionResult> CreateSubject(SubjectRequest request)
    {
        var school = await db.Schools.FindAsync(SchoolId);
        if (school is null)
            return BadRequest(new { message = "المدرسة غير موجودة" });

        var existingSubject = await db.Subjects
            .AnyAsync(s => s.Name == request.Name && s.SchoolId == SchoolId);

        if (existingSubject)
            return BadRequest(new { message = $"المادة '{request.Name}' موجودة بالفعل في هذه المدرسة" });

        var maxLocalId = await db.Subjects
            .Where(s => s.SchoolId == SchoolId)
            .Select(s => (int?)s.LocalSubjectId)
            .MaxAsync() ?? 0;

        int newLocalId = maxLocalId + 1;

        var subject = new Subject
        {
            Name = request.Name,
            SchoolId = SchoolId,
            LocalSubjectId = newLocalId
        };

        db.Subjects.Add(subject);
        await db.SaveChangesAsync();

        return Created($"api/manager/subjects/{newLocalId}", new
        {
            message = "تم إضافة المادة بنجاح",
            subject = new
            {
                subject.Id,
                subject.Name,
                subject.LocalSubjectId,
                subject.SchoolId,
                SchoolName = school.Name
            }
        });
    }

    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects()
    {
        var subjects = await db.Subjects
            .Where(s => s.SchoolId == SchoolId)
            .OrderBy(s => s.LocalSubjectId)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.LocalSubjectId,
                s.SchoolId,
                Teachers = db.TeacherSubjects
                    .Where(t => t.SubjectId == s.Id)
                    .Select(t => new
                    {
                        t.TeacherId,
                        TeacherName = t.Teacher!.Name,
                        LocalTeacherNumber = db.EmployeeSchools
                            .Where(es => es.EmployeeId == t.TeacherId && 
                                         es.SchoolId == SchoolId && 
                                         es.IsActive)
                            .Select(es => (int?)es.LocalEmployeeNumber)
                            .FirstOrDefault(),
                        t.CreatedAt
                    })
                    .ToList(),
                Sections = db.TeacherGrades
                    .Where(tg => tg.SubjectId == s.Id)
                    .Select(tg => new
                    {
                        tg.SectionId,
                        SectionName = tg.Section != null ? tg.Section.Name : null,
                        LocalSectionNumber = tg.Section != null ? tg.Section.LocalSectionNumber : 0,
                        GradeId = tg.Section != null ? tg.Section.GradeId : 0,
                        LocalGradeNumber = tg.Section != null && tg.Section.Grade != null ? 
                            tg.Section.Grade.LocalGradeNumber : 0,
                        GradeName = tg.Section != null && tg.Section.Grade != null ?
                            tg.Section.Grade.Name : null,
                        TeacherId = tg.TeacherId,
                        TeacherName = tg.Teacher != null ? tg.Teacher.Name : null,
                        LocalTeacherNumber = db.EmployeeSchools
                            .Where(es => es.EmployeeId == tg.TeacherId && 
                                         es.SchoolId == SchoolId && 
                                         es.IsActive)
                            .Select(es => (int?)es.LocalEmployeeNumber)
                            .FirstOrDefault(),
                        tg.CreatedAt
                    })
                    .ToList()
            })
            .ToListAsync();

        return Ok(subjects);
    }

    [HttpGet("subjects/{localSubjectId:int}")]
    public async Task<IActionResult> GetSubject(int localSubjectId)
    {
        var subject = await db.Subjects
            .Include(s => s.Teacher)
            .Include(s => s.TeacherSubjects)
                .ThenInclude(ts => ts.Teacher)
            .Include(s => s.TeacherGrades)
                .ThenInclude(tg => tg.Teacher)
            .Include(s => s.TeacherGrades)
                .ThenInclude(tg => tg.Section)
                    .ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalSubjectId == localSubjectId);

        if (subject is null)
            return NotFound(new { message = $"لا توجد مادة برقم {localSubjectId} في هذه المدرسة" });

        var teachers = subject.TeacherSubjects != null ? 
            subject.TeacherSubjects
                .Select(ts => new
                {
                    ts.TeacherId,
                    TeacherName = ts.Teacher?.Name,
                    LocalTeacherNumber = db.EmployeeSchools
                        .Where(es => es.EmployeeId == ts.TeacherId && 
                                     es.SchoolId == SchoolId && 
                                     es.IsActive)
                        .Select(es => (int?)es.LocalEmployeeNumber)
                        .FirstOrDefault(),
                    ts.CreatedAt
                })
                .ToList<object>() : new List<object>();

        var sections = subject.TeacherGrades != null ?
            subject.TeacherGrades
                .Select(tg => new
                {
                    tg.SectionId,
                    SectionName = tg.Section?.Name,
                    LocalSectionNumber = tg.Section != null ? tg.Section.LocalSectionNumber : 0,
                    GradeId = tg.Section != null ? tg.Section.GradeId : 0,
                    LocalGradeNumber = tg.Section != null && tg.Section.Grade != null ? 
                        tg.Section.Grade.LocalGradeNumber : 0,
                    GradeName = tg.Section != null && tg.Section.Grade != null ?
                        tg.Section.Grade.Name : null,
                    TeacherId = tg.TeacherId,
                    TeacherName = tg.Teacher?.Name,
                    LocalTeacherNumber = db.EmployeeSchools
                        .Where(es => es.EmployeeId == tg.TeacherId && 
                                     es.SchoolId == SchoolId && 
                                     es.IsActive)
                        .Select(es => (int?)es.LocalEmployeeNumber)
                        .FirstOrDefault(),
                    tg.CreatedAt
                })
                .ToList<object>() : new List<object>();

        return Ok(new
        {
            subject.Id,
            subject.Name,
            subject.LocalSubjectId,
            subject.SchoolId,
            TeacherId = subject.TeacherId,
            TeacherName = subject.Teacher?.Name,
            LocalTeacherNumber = subject.TeacherId.HasValue ? 
                await db.EmployeeSchools
                    .Where(es => es.EmployeeId == subject.TeacherId.Value && 
                                 es.SchoolId == SchoolId && 
                                 es.IsActive)
                    .Select(es => (int?)es.LocalEmployeeNumber)
                    .FirstOrDefaultAsync() : null,
            Teachers = teachers,
            Sections = sections,
            CreatedAt = subject.TeacherSubjects != null && subject.TeacherSubjects.Any() ? 
                subject.TeacherSubjects.First().CreatedAt : DateTime.UtcNow
        });
    }

    [HttpPut("subjects/{localSubjectId:int}")]
    public async Task<IActionResult> UpdateSubject(int localSubjectId, SubjectRequest request)
    {
        var subject = await db.Subjects
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalSubjectId == localSubjectId);

        if (subject is null)
            return NotFound(new { message = $"لا توجد مادة برقم {localSubjectId} في هذه المدرسة" });

        var existingSubject = await db.Subjects
            .AnyAsync(s => s.Name == request.Name && 
                           s.SchoolId == SchoolId && 
                           s.LocalSubjectId != localSubjectId);

        if (existingSubject)
            return BadRequest(new { message = $"المادة '{request.Name}' موجودة بالفعل في هذه المدرسة" });

        subject.Name = request.Name;
        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "تم تحديث المادة بنجاح",
            subject = new
            {
                subject.Id,
                subject.Name,
                subject.LocalSubjectId,
                subject.SchoolId
            }
        });
    }

    [HttpDelete("subjects/{localSubjectId:int}")]
    public async Task<IActionResult> DeleteSubject(int localSubjectId)
    {
        var subject = await db.Subjects
            .Include(s => s.TeacherSubjects)
            .Include(s => s.TeacherGrades)
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalSubjectId == localSubjectId);

        if (subject is null)
            return NotFound(new { message = $"لا توجد مادة برقم {localSubjectId} في هذه المدرسة" });

        if (subject.TeacherSubjects != null && subject.TeacherSubjects.Any())
            db.TeacherSubjects.RemoveRange(subject.TeacherSubjects);

        if (subject.TeacherGrades != null && subject.TeacherGrades.Any())
            db.TeacherGrades.RemoveRange(subject.TeacherGrades);

        db.Subjects.Remove(subject);
        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "تم حذف المادة وجميع البيانات المرتبطة بنجاح",
            localSubjectId = localSubjectId,
            subjectName = subject.Name,
            schoolId = SchoolId
        });
    }

    // ============================================
    // ربط المعلم بالمادة (باستخدام Local IDs)
    // ============================================

    [HttpPost("assign-teacher-to-subject")]
public async Task<IActionResult> AssignTeacherToSubject(TeacherSubjectLocalRequest request)
{
    // البحث عن المعلم باستخدام LocalEmployeeNumber
    var teacherSchool = await db.EmployeeSchools
        .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                   es.LocalEmployeeNumber == request.TeacherLocalNumber &&
                                   es.Role == EmployeeRole.Teacher &&
                                   es.IsActive);

    if (teacherSchool is null)
        return BadRequest(new { message = $"لا يوجد معلم برقم {request.TeacherLocalNumber} في هذه المدرسة" });

    // البحث عن المادة باستخدام LocalSubjectId
    var subject = await db.Subjects
        .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                  s.LocalSubjectId == request.LocalSubjectId);

    if (subject is null)
        return BadRequest(new { message = $"لا توجد مادة برقم {request.LocalSubjectId} في هذه المدرسة" });

    var teacherId = teacherSchool.EmployeeId;
    var subjectId = subject.Id;

    // ✅ التحقق من عدم وجود علاقة مكررة
    var exists = await db.TeacherSubjects
        .AnyAsync(t => t.TeacherId == teacherId && t.SubjectId == subjectId);

    if (exists)
        return BadRequest(new { message = "هذا المعلم مرتبط بالفعل بهذه المادة" });

    // ✅ حساب LocalTeacherSubjectId
    var maxLocalId = await db.TeacherSubjects
        .Where(ts => ts.SchoolId == SchoolId)
        .Select(ts => (int?)ts.LocalTeacherSubjectId)
        .MaxAsync() ?? 0;

    int newLocalId = maxLocalId + 1;

    var teacherSubject = new TeacherSubject
    {
        TeacherId = teacherId,
        SubjectId = subjectId,
        SchoolId = SchoolId,
        LocalTeacherSubjectId = newLocalId,
        CreatedAt = DateTime.UtcNow
    };

    db.TeacherSubjects.Add(teacherSubject);
    await db.SaveChangesAsync();

    var teacher = await db.Employees.FindAsync(teacherId);

    return Ok(new
    {
        message = "تم ربط المعلم بالمادة بنجاح",
        teacherLocalNumber = request.TeacherLocalNumber,
        teacherName = teacher?.Name,
        localSubjectId = request.LocalSubjectId,
        subjectName = subject.Name,
        localTeacherSubjectId = newLocalId  // ✅ Local ID للعلاقة
    });
}

    // ============================================
    // ربط المعلم بالشعبة (باستخدام Local IDs)
    // ============================================

    [HttpPost("assign-teacher-to-section")]
    public async Task<IActionResult> AssignTeacherToSection(TeacherGradeLocalRequest request)
    {
        var teacherSchool = await db.EmployeeSchools
            .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                       es.LocalEmployeeNumber == request.TeacherLocalNumber &&
                                       es.Role == EmployeeRole.Teacher &&
                                       es.IsActive);

        if (teacherSchool is null)
            return BadRequest(new { message = $"لا يوجد معلم برقم {request.TeacherLocalNumber} في هذه المدرسة" });

        var subject = await db.Subjects
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalSubjectId == request.LocalSubjectId);

        if (subject is null)
            return BadRequest(new { message = $"لا توجد مادة برقم {request.LocalSubjectId} في هذه المدرسة" });

        var section = await db.Sections
            .Include(s => s.Grade)
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalSectionNumber == request.LocalSectionNumber);

        if (section is null)
            return BadRequest(new { message = $"لا توجد شعبة برقم {request.LocalSectionNumber} في هذه المدرسة" });

        var teacherId = teacherSchool.EmployeeId;
        var subjectId = subject.Id;

        var exists = await db.TeacherGrades
            .AnyAsync(tg => tg.TeacherId == teacherId &&
                           tg.SubjectId == subjectId &&
                           tg.SectionId == section.Id);

        if (exists)
            return BadRequest(new { message = "هذا المعلم مرتبط بالفعل بهذه المادة في هذه الشعبة" });

        var teacherGrade = new TeacherGrade
        {
            TeacherId = teacherId,
            SubjectId = subjectId,
            SectionId = section.Id
        };

        db.TeacherGrades.Add(teacherGrade);
        await db.SaveChangesAsync();

        var teacher = await db.Employees.FindAsync(teacherId);

        return Ok(new
        {
            message = "تم ربط المعلم بالشعبة بنجاح",
            teacherLocalNumber = request.TeacherLocalNumber,
            teacherName = teacher?.Name,
            localSubjectId = request.LocalSubjectId,
            subjectName = subject.Name,
            sectionId = section.Id,
            sectionName = section.Name,
            localSectionNumber = section.LocalSectionNumber,
            gradeId = section.GradeId,
            localGradeNumber = section.Grade?.LocalGradeNumber,
            gradeName = section.Grade?.Name
        });
    }

    // ============================================
    // جلب مواد المعلم (باستخدام Local IDs)
    // ============================================

    [HttpGet("teacher-subjects/{localTeacherNumber:int}")]
public async Task<IActionResult> GetTeacherSubjects(int localTeacherNumber)
{
    var teacherSchool = await db.EmployeeSchools
        .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                   es.LocalEmployeeNumber == localTeacherNumber &&
                                   es.Role == EmployeeRole.Teacher &&
                                   es.IsActive);

    if (teacherSchool is null)
        return NotFound(new { message = $"لا يوجد معلم برقم {localTeacherNumber} في هذه المدرسة" });

    var teacherId = teacherSchool.EmployeeId;
    var teacher = await db.Employees.FindAsync(teacherId);

    var teacherSubjects = await db.TeacherSubjects
        .Include(ts => ts.Subject)
        .Where(ts => ts.TeacherId == teacherId && ts.SchoolId == SchoolId)
        .Select(ts => new
        {
            ts.Id,
            LocalTeacherSubjectId = ts.LocalTeacherSubjectId,  // ✅ Local ID
            ts.SubjectId,
            LocalSubjectId = ts.Subject != null ? ts.Subject.LocalSubjectId : 0,
            SubjectName = ts.Subject != null ? ts.Subject.Name : null,
            ts.CreatedAt
        })
        .ToListAsync();

    return Ok(new
    {
        localTeacherNumber = localTeacherNumber,
        teacherId = teacherId,
        teacherName = teacher?.Name,
        subjects = teacherSubjects,
        totalSubjects = teacherSubjects.Count
    });
}

    // ============================================
    // جلب معلمي الشعبة (باستخدام Local IDs)
    // ============================================

    [HttpGet("section-teachers/{localSectionNumber:int}")]
public async Task<IActionResult> GetSectionTeachers(int localSectionNumber)
{
    var section = await db.Sections
        .Include(s => s.Grade)
        .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                  s.LocalSectionNumber == localSectionNumber);

    if (section is null)
        return NotFound(new { message = $"لا توجد شعبة برقم {localSectionNumber} في هذه المدرسة" });

    var teachers = await db.TeacherGrades
        .Where(tg => tg.SectionId == section.Id)
        .Select(tg => new
        {
            tg.TeacherId,
            TeacherName = tg.Teacher != null ? tg.Teacher.Name : null,
            LocalTeacherNumber = db.EmployeeSchools
                .Where(es => es.EmployeeId == tg.TeacherId && 
                             es.SchoolId == SchoolId && 
                             es.IsActive)
                .Select(es => (int?)es.LocalEmployeeNumber)
                .FirstOrDefault(),
            tg.SubjectId,
            LocalSubjectId = db.Subjects
                .Where(s => s.Id == tg.SubjectId)
                .Select(s => s.LocalSubjectId)
                .FirstOrDefault(),
            SubjectName = db.Subjects
                .Where(s => s.Id == tg.SubjectId)
                .Select(s => s.Name)
                .FirstOrDefault(),
            tg.CreatedAt
        })
        .ToListAsync();

    return Ok(new
    {
        sectionId = section.Id,
        sectionName = section.Name,
        localSectionNumber = section.LocalSectionNumber,
        gradeId = section.GradeId,
        localGradeNumber = section.Grade?.LocalGradeNumber,
        gradeName = section.Grade?.Name,
        teachers = teachers
    });
}

    // ============================================
    // إدارة الموظفين - باستخدام Local IDs
    // ============================================

    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees()
    {
        var employees = await db.EmployeeSchools
            .Where(es => es.SchoolId == SchoolId && es.IsActive)
            .Include(es => es.Employee)
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
                es.IsActive,
                es.CreatedAt
            })
            .ToListAsync();

        return Ok(employees);
    }

    [HttpGet("employees/{localEmployeeNumber:int}")]
    public async Task<IActionResult> GetEmployee(int localEmployeeNumber)
    {
        var employeeSchool = await db.EmployeeSchools
            .Include(es => es.Employee)
            .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                      es.LocalEmployeeNumber == localEmployeeNumber &&
                                      es.IsActive);

        if (employeeSchool is null)
            return NotFound(new { message = $"لا يوجد موظف برقم {localEmployeeNumber} في هذه المدرسة" });

        var employee = employeeSchool.Employee;
        if (employee is null)
            return NotFound(new { message = "الموظف غير موجود" });

        return Ok(new
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
            localEmployeeNumber = employeeSchool.LocalEmployeeNumber,
            role = employeeSchool.Role,
            roleName = GetRoleName(employeeSchool.Role)
        });
    }

    // ============================================
    // إدارة الطلاب - باستخدام Local IDs
    // ============================================

    [HttpGet("students")]
    public async Task<IActionResult> GetStudents()
    {
        var students = await db.Students
            .Where(s => s.SchoolId == SchoolId)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Email,
                s.LocalStudentNumber,
                s.SchoolId,
                s.SectionId,
                SectionName = s.Section != null ? s.Section.Name : null,
                SectionLocalNumber = s.Section != null ? s.Section.LocalSectionNumber : 0,
                GradeLocalNumber = s.Section != null && s.Section.Grade != null ? 
                    s.Section.Grade.LocalGradeNumber : 0,
                GradeName = s.Section != null && s.Section.Grade != null ? s.Section.Grade.Name : null,
                s.GuardianName,
                s.GuardianPhone,
                s.BloodType,
                s.BirthDate,
                s.Address,
                s.CreatedAt
            })
            .ToListAsync();

        return Ok(students);
    }

    [HttpGet("students/{localStudentNumber:int}")]
    public async Task<IActionResult> GetStudent(int localStudentNumber)
    {
        var student = await db.Students
            .Include(s => s.Section)
                .ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalStudentNumber == localStudentNumber);

        if (student is null)
            return NotFound(new { message = $"لا يوجد طالب برقم {localStudentNumber} في هذه المدرسة" });

        return Ok(new
        {
            student.Id,
            student.Name,
            student.Email,
            student.LocalStudentNumber,
            student.SchoolId,
            student.SectionId,
            SectionName = student.Section?.Name,
            SectionLocalNumber = student.Section?.LocalSectionNumber ?? 0,
            GradeLocalNumber = student.Section?.Grade?.LocalGradeNumber ?? 0,
            GradeName = student.Section?.Grade?.Name,
            student.GuardianName,
            student.GuardianPhone,
            student.BloodType,
            student.BirthDate,
            student.Address,
            student.CreatedAt
        });
    }

    [HttpPost("students")]
    public async Task<IActionResult> CreateStudent(StudentCreateRequest request)
    {
        var school = await db.Schools.FindAsync(SchoolId);
        if (school is null)
            return BadRequest(new { message = "المدرسة غير موجودة" });

        if (await db.Students.AnyAsync(s => s.Email == request.Email))
            return BadRequest(new { message = "البريد الإلكتروني موجود مسبقاً" });

        var maxLocalNumber = await db.Students
            .Where(s => s.SchoolId == SchoolId)
            .Select(s => (int?)s.LocalStudentNumber)
            .MaxAsync() ?? 0;

        int newLocalNumber = maxLocalNumber + 1;

        var student = new Student
        {
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            SchoolId = SchoolId,
            LocalStudentNumber = newLocalNumber,
            BirthDate = request.BirthDate,
            Address = request.Address ?? "",
            GuardianName = request.GuardianName ?? "",
            GuardianPhone = request.GuardianPhone ?? "",
            BloodType = request.BloodType ?? "",
        };

        db.Students.Add(student);
        await db.SaveChangesAsync();

        await notifier.SendAsync(
            student.Id,
            UserType.Student,
            "مرحباً في المدرسة",
            $"تم تسجيلك في مدرسة '{school.Name}' برقم طالب {newLocalNumber}",
            "registration"
        );

        return Created($"api/manager/students/{newLocalNumber}", new
        {
            message = "تم إنشاء الطالب بنجاح",
            student = new
            {
                student.Id,
                student.Name,
                student.Email,
                student.LocalStudentNumber,
                student.SchoolId,
                SchoolName = school.Name,
                student.BirthDate,
                student.Address,
                student.GuardianName,
                student.GuardianPhone,
                student.CreatedAt
            }
        });
    }
    // ============================================
// دوال مساعدة
// ============================================

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

    [HttpPut("students/{localStudentNumber:int}")]
    public async Task<IActionResult> UpdateStudent(int localStudentNumber, StudentUpdateRequesting request)
    {
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalStudentNumber == localStudentNumber);

        if (student is null)
            return NotFound(new { message = $"لا يوجد طالب برقم {localStudentNumber} في هذه المدرسة" });

        if (!string.IsNullOrWhiteSpace(request.Name))
            student.Name = request.Name;

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var existingEmail = await db.Students
                .AnyAsync(s => s.Email == request.Email && s.Id != student.Id);

            if (existingEmail)
                return BadRequest(new { message = "البريد الإلكتروني مستخدم بالفعل" });

            student.Email = request.Email;
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
            student.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        if (!string.IsNullOrWhiteSpace(request.GuardianName))
            student.GuardianName = request.GuardianName;

        if (!string.IsNullOrWhiteSpace(request.GuardianPhone))
            student.GuardianPhone = request.GuardianPhone;

        if (!string.IsNullOrWhiteSpace(request.Address))
            student.Address = request.Address;

        if (!string.IsNullOrWhiteSpace(request.BloodType))
            student.BloodType = request.BloodType;

        if (request.BirthDate.HasValue)
            student.BirthDate = request.BirthDate;

        if (request.LocalSectionNumber.HasValue)
        {
            var section = await db.Sections
                .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                          s.LocalSectionNumber == request.LocalSectionNumber.Value);

            if (section is null)
                return BadRequest(new { message = $"لا توجد شعبة برقم {request.LocalSectionNumber} في هذه المدرسة" });

            student.SectionId = section.Id;
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "تم تحديث بيانات الطالب بنجاح",
            student = new
            {
                student.Id,
                student.Name,
                student.Email,
                student.LocalStudentNumber,
                student.SchoolId,
                student.BirthDate,
                student.Address,
                student.GuardianName,
                student.GuardianPhone,
                student.CreatedAt
            }
        });
    }

    [HttpDelete("students/{localStudentNumber:int}")]
public async Task<IActionResult> DeleteStudent(int localStudentNumber)
{
    var student = await db.Students
        .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                  s.LocalStudentNumber == localStudentNumber);

    if (student is null)
        return NotFound(new { message = $"لا يوجد طالب برقم {localStudentNumber} في هذه المدرسة" });

    // ✅ حذف العلامات المرتبطة
    var marks = await db.Marks
        .Where(m => m.StudentId == student.Id)
        .ToListAsync();
    if (marks.Any())
        db.Marks.RemoveRange(marks);

    // ✅ حذف بطاقات التقارير المرتبطة
    var reportCards = await db.ReportCards
        .Where(r => r.StudentId == student.Id)
        .ToListAsync();
    if (reportCards.Any())
        db.ReportCards.RemoveRange(reportCards);

    // ✅ حذف الحضور المرتبط
    var attendances = await db.StudentAttendances
        .Where(a => a.StudentId == student.Id)
        .ToListAsync();
    if (attendances.Any())
        db.StudentAttendances.RemoveRange(attendances);

    // ✅ حذف التحذيرات المرتبطة
    var warnings = await db.Warnings
        .Where(w => w.StudentId == student.Id)
        .ToListAsync();
    if (warnings.Any())
        db.Warnings.RemoveRange(warnings);

    // ✅ حذف العقوبات المرتبطة
    var punishments = await db.Punishments
        .Where(p => p.StudentId == student.Id)
        .ToListAsync();
    if (punishments.Any())
        db.Punishments.RemoveRange(punishments);

    // ✅ حذف تسجيلات الأنشطة المرتبطة
    var activityRegistrations = await db.ActivityRegistrations
        .Where(r => r.StudentId == student.Id)
        .ToListAsync();
    if (activityRegistrations.Any())
        db.ActivityRegistrations.RemoveRange(activityRegistrations);

    // ✅ حذف عضوية المكتبة (إذا وجدت)
    var libraryMember = await db.LibraryMembers
        .FirstOrDefaultAsync(m => m.StudentId == student.Id);
    if (libraryMember is not null)
    {
        // حذف استعارات الكتب المرتبطة
        var bookLoans = await db.BookLoans
            .Where(l => l.MemberId == libraryMember.Id)
            .ToListAsync();
        if (bookLoans.Any())
            db.BookLoans.RemoveRange(bookLoans);

        // حذف حجوزات الكتب المرتبطة
        var bookReservations = await db.BookReservations
            .Where(r => r.MemberId == libraryMember.Id)
            .ToListAsync();
        if (bookReservations.Any())
            db.BookReservations.RemoveRange(bookReservations);

        db.LibraryMembers.Remove(libraryMember);
    }
    
    // ✅ حذف الطالب
    db.Students.Remove(student);
    await db.SaveChangesAsync();

    return Ok(new
    {
        success = true,
        message = "تم حذف الطالب وجميع البيانات المرتبطة بنجاح",
        localStudentNumber = localStudentNumber,
        studentName = student.Name
    });
}

    // ============================================
    // حضور الموظفين - باستخدام Local IDs
    // ============================================

    [HttpPost("employee-attendance")]
    public async Task<IActionResult> TakeEmployeeAttendance(EmployeeAttendanceLocalRequest request)
    {
        foreach (var entry in request.Entries)
        {
            var employeeSchool = await db.EmployeeSchools
                .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                          es.LocalEmployeeNumber == entry.LocalEmployeeNumber &&
                                          es.IsActive);

            if (employeeSchool is null)
                return BadRequest(new { message = $"لا يوجد موظف برقم {entry.LocalEmployeeNumber} في هذه المدرسة" });

            var employeeId = employeeSchool.EmployeeId;

            var onLeave = entry.Status == AttendanceStatus.Absent &&
                          await db.Leaves.AnyAsync(l => l.EmployeeId == employeeId &&
                                                        l.StartDate <= request.Date && request.Date <= l.EndDate);

            var existing = await db.EmployeeAttendances
                .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == request.Date);

            if (existing is not null)
            {
                existing.Status = entry.Status;
                existing.OnLeave = onLeave;
            }
            else
            {
                db.EmployeeAttendances.Add(new EmployeeAttendance
                {
                    EmployeeId = employeeId,
                    Date = request.Date,
                    Status = entry.Status,
                    OnLeave = onLeave,
                });
            }
        }
        await db.SaveChangesAsync();
        return Ok(new { message = "تم تسجيل حضور الموظفين" });
    }

    [HttpGet("employee-attendance")]
    public async Task<IActionResult> GetEmployeeAttendance([FromQuery] DateOnly? date, [FromQuery] int? localEmployeeNumber)
    {
        int? employeeId = null;
        if (localEmployeeNumber.HasValue)
        {
            var employeeSchool = await db.EmployeeSchools
                .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                          es.LocalEmployeeNumber == localEmployeeNumber.Value &&
                                          es.IsActive);
            if (employeeSchool is not null)
                employeeId = employeeSchool.EmployeeId;
        }

        var query = db.EmployeeAttendances
            .Where(a => db.EmployeeSchools.Any(es => es.EmployeeId == a.EmployeeId &&
                                                    es.SchoolId == SchoolId &&
                                                    es.IsActive));

        if (date is not null)
            query = query.Where(a => a.Date == date);

        if (employeeId is not null)
            query = query.Where(a => a.EmployeeId == employeeId);

        var attendance = await query
            .OrderByDescending(a => a.Date)
            .Take(500)
            .Select(a => new
            {
                a.Id,
                a.EmployeeId,
                LocalEmployeeNumber = db.EmployeeSchools
                    .Where(es => es.EmployeeId == a.EmployeeId && es.SchoolId == SchoolId)
                    .Select(es => (int?)es.LocalEmployeeNumber)
                    .FirstOrDefault(),
                a.Date,
                a.Status,
                a.OnLeave
            })
            .ToListAsync();

        return Ok(attendance);
    }

    // ============================================
    // العقوبات - باستخدام Local IDs
    // ============================================

    [HttpPost("punishments")]
    public async Task<IActionResult> CreatePunishment(PunishmentLocalRequest request)
    {
        int? studentId = null;
        int? employeeId = null;

        if (request.LocalStudentNumber.HasValue)
        {
            var student = await db.Students
                .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                          s.LocalStudentNumber == request.LocalStudentNumber.Value);
            if (student is null)
                return BadRequest(new { message = $"لا يوجد طالب برقم {request.LocalStudentNumber} في هذه المدرسة" });
            studentId = student.Id;
        }

        if (request.LocalEmployeeNumber.HasValue)
        {
            var employeeSchool = await db.EmployeeSchools
                .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                          es.LocalEmployeeNumber == request.LocalEmployeeNumber.Value &&
                                          es.IsActive);
            if (employeeSchool is null)
                return BadRequest(new { message = $"لا يوجد موظف برقم {request.LocalEmployeeNumber} في هذه المدرسة" });
            employeeId = employeeSchool.EmployeeId;
        }

        if (studentId is null == (employeeId is null))
            return BadRequest(new { message = "حدد طالباً أو موظفاً (واحد فقط)" });

        var punishment = new Punishment
        {
            StudentId = studentId,
            EmployeeId = employeeId,
            SchoolId = SchoolId,
            Reason = request.Reason,
            IssuedById = User.GetUserId(),
        };

        db.Punishments.Add(punishment);
        await db.SaveChangesAsync();

        if (studentId is not null)
            await notifier.SendAsync(studentId.Value, UserType.Student, "عقوبة", request.Reason, "punishment");
        else
            await notifier.SendAsync(employeeId!.Value, UserType.Employee, "عقوبة", request.Reason, "punishment");

        return Created($"api/manager/punishments/{punishment.Id}", new
        {
            punishment.Id,
            LocalStudentNumber = request.LocalStudentNumber,
            LocalEmployeeNumber = request.LocalEmployeeNumber,
            punishment.Reason,
            punishment.Type,
            punishment.CreatedAt
        });
    }

    // ============================================
    // الشكاوى - باستخدام Local IDs
    // ============================================

    [HttpPatch("complaints/{localComplaintId:int}")]
    public async Task<IActionResult> ResolveComplaint(int localComplaintId, ComplaintResolveRequest request)
    {
        var complaint = await db.Complaints
            .FirstOrDefaultAsync(c => c.Id == localComplaintId && c.SchoolId == SchoolId);

        if (complaint is null)
            return NotFound(new { message = "الشكوى غير موجودة" });

        complaint.Status = request.Status;
        complaint.Resolution = request.Resolution ?? complaint.Resolution;
        await db.SaveChangesAsync();

        await notifier.SendAsync(complaint.FromUserId, complaint.FromUserType,
            "تحديث على شكواك", $"حالة الشكوى: {request.Status}", "complaint");

        return Ok(complaint);
    }

    // ============================================
    // صورة جدول المعلم - باستخدام Local IDs
    // ============================================

    [HttpPost("schedule-images/teacher")]
    public async Task<IActionResult> UploadTeacherScheduleImage([FromForm] TeacherScheduleImageRequest request)
    {
        var school = await db.Schools.FindAsync(SchoolId);
        if (school is null)
            return BadRequest(new { message = "المدرسة غير موجودة" });

        var employeeSchool = await db.EmployeeSchools
            .Include(es => es.Employee)
            .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                       es.LocalEmployeeNumber == request.LocalEmployeeNumber &&
                                       es.Role == EmployeeRole.Teacher &&
                                       es.IsActive);

        if (employeeSchool is null)
            return BadRequest(new { message = $"لا يوجد معلم برقم {request.LocalEmployeeNumber} في هذه المدرسة" });

        var teacher = employeeSchool.Employee;
        if (teacher is null)
            return BadRequest(new { message = "المعلم غير موجود" });

        var imageUrl = await SaveScheduleImageAsync(request.Image);

        var existingImage = await db.ScheduleImages
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.TeacherId == teacher.Id && 
                                      s.Type == ScheduleImageType.Teacher);

        if (existingImage is not null)
        {
            DeleteScheduleImageFile(existingImage.ImageUrl);
            db.ScheduleImages.Remove(existingImage);
            await db.SaveChangesAsync();
        }

        var scheduleImage = new ScheduleImage
        {
            SchoolId = SchoolId,
            GradeId = null,
            SectionId = null,
            TeacherId = teacher.Id,
            ImageUrl = imageUrl,
            Description = request.Description ?? $"جدول حصص المعلم {teacher.Name}",
            Type = ScheduleImageType.Teacher,
            CreatedAt = DateTime.UtcNow
        };

        db.ScheduleImages.Add(scheduleImage);
        await db.SaveChangesAsync();

        return Created($"api/manager/schedule-images/teacher/{scheduleImage.Id}", new
        {
            message = "تم رفع صورة جدول المعلم بنجاح",
            scheduleImage = new
            {
                scheduleImage.Id,
                scheduleImage.ImageUrl,
                scheduleImage.Description,
                teacherId = teacher.Id,
                teacherName = teacher.Name,
                localEmployeeNumber = employeeSchool.LocalEmployeeNumber,
                scheduleImage.CreatedAt
            }
        });
    }

    [HttpGet("schedule-images/teacher/{localEmployeeNumber:int}")]
    public async Task<IActionResult> GetTeacherScheduleImage(int localEmployeeNumber)
    {
        var employeeSchool = await db.EmployeeSchools
            .Include(es => es.Employee)
            .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                       es.LocalEmployeeNumber == localEmployeeNumber &&
                                       es.Role == EmployeeRole.Teacher &&
                                       es.IsActive);

        if (employeeSchool is null)
            return NotFound(new { message = $"لا يوجد معلم برقم {localEmployeeNumber} في هذه المدرسة" });

        var teacher = employeeSchool.Employee;
        if (teacher is null)
            return NotFound(new { message = "المعلم غير موجود" });

        var image = await db.ScheduleImages
            .Where(s => s.SchoolId == SchoolId && 
                        s.TeacherId == teacher.Id && 
                        s.Type == ScheduleImageType.Teacher)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (image is null)
            return NotFound(new { message = "لا توجد صورة جدول لهذا المعلم" });

        return Ok(new
        {
            localEmployeeNumber = localEmployeeNumber,
            teacherId = teacher.Id,
            teacherName = teacher.Name,
            image = new
            {
                image.Id,
                image.ImageUrl,
                image.Description,
                image.CreatedAt
            }
        });
    }

    // ============================================
    // صورة جدول الشعبة - باستخدام Local IDs
    // ============================================

    [HttpGet("schedule-images/section/{localGradeNumber:int}/{localSectionNumber:int}")]
    public async Task<IActionResult> GetSectionScheduleImage(int localGradeNumber, int localSectionNumber)
    {
        var grade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.LocalGradeNumber == localGradeNumber);
        if (grade is null)
            return NotFound(new { message = $"لا يوجد صف برقم {localGradeNumber} في هذه المدرسة" });

        var section = await db.Sections
            .FirstOrDefaultAsync(s => s.GradeId == grade.Id && 
                                      s.LocalSectionNumber == localSectionNumber &&
                                      s.SchoolId == SchoolId);
        
        if (section is null)
            return NotFound(new { message = $"لا توجد شعبة برقم {localSectionNumber} في الصف {localGradeNumber}" });

        var image = await db.ScheduleImages
            .Where(s => s.SchoolId == SchoolId && 
                        s.SectionId == section.Id && 
                        s.Type == ScheduleImageType.Section)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (image is null)
            return NotFound(new { message = "لا توجد صورة جدول لهذه الشعبة" });

        return Ok(new
        {
            localGradeNumber = localGradeNumber,
            gradeName = grade.Name,
            localSectionNumber = localSectionNumber,
            sectionName = section.Name,
            image = new
            {
                image.Id,
                image.ImageUrl,
                image.Description,
                image.CreatedAt
            }
        });
    }

    // ============================================
    // حذف صورة - باستخدام Local IDs
    // ============================================

    [HttpDelete("schedule-images/{id:int}")]
    public async Task<IActionResult> DeleteScheduleImage(int id)
    {
        var image = await db.ScheduleImages
            .FirstOrDefaultAsync(s => s.Id == id && s.SchoolId == SchoolId);

        if (image is null)
            return NotFound(new { message = "الصورة غير موجودة" });

        DeleteScheduleImageFile(image.ImageUrl);
        db.ScheduleImages.Remove(image);
        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف الصورة بنجاح" });
    }

    // ============================================
    // جلب صور الجداول - باستخدام Local IDs
    // ============================================

    [HttpGet("schedule-images")]
    public async Task<IActionResult> GetScheduleImages(
        [FromQuery] ScheduleImageType? type,
        [FromQuery] int? localGradeNumber,
        [FromQuery] int? localSectionNumber)
    {
        var query = db.ScheduleImages
            .Include(s => s.Grade)
            .Include(s => s.Section)
            .Include(s => s.Teacher)
            .Where(s => s.SchoolId == SchoolId);

        if (type.HasValue)
            query = query.Where(s => s.Type == type);

        if (localGradeNumber.HasValue)
        {
            var grade = await db.Grades
                .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                          g.LocalGradeNumber == localGradeNumber.Value);
            if (grade is not null)
                query = query.Where(s => s.GradeId == grade.Id);
        }

        if (localSectionNumber.HasValue && localGradeNumber.HasValue)
        {
            var grade = await db.Grades
                .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                          g.LocalGradeNumber == localGradeNumber.Value);
            if (grade is not null)
            {
                var section = await db.Sections
                    .FirstOrDefaultAsync(s => s.GradeId == grade.Id && 
                                              s.LocalSectionNumber == localSectionNumber.Value &&
                                              s.SchoolId == SchoolId);
                if (section is not null)
                    query = query.Where(s => s.SectionId == section.Id);
            }
        }

        var images = await query
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.ImageUrl,
                s.Description,
                s.Type,
                LocalGradeNumber = s.Grade != null ? s.Grade.LocalGradeNumber : (int?)null,
                GradeName = s.Grade != null ? s.Grade.Name : null,
                LocalSectionNumber = s.Section != null ? s.Section.LocalSectionNumber : (int?)null,
                SectionName = s.Section != null ? s.Section.Name : null,
                TeacherName = s.Teacher != null ? s.Teacher.Name : null,
                s.CreatedAt
            })
            .ToListAsync();

        return Ok(images);
    }

    // ============================================
    // التقارير
    // ============================================

    [HttpGet("reports/overview")]
public async Task<IActionResult> Overview()
{
    var today = DateOnly.FromDateTime(DateTime.Today);

    var employeesCount = await db.EmployeeSchools
        .CountAsync(es => es.SchoolId == SchoolId && es.IsActive);

    var employeesWithDismissalWarning = await db.EmployeeSchools
        .Where(es => es.SchoolId == SchoolId && es.IsActive)
        .Join(db.Employees,
            es => es.EmployeeId,
            e => e.Id,
            (es, e) => e)
        .CountAsync(e => e.DismissalWarning && !e.IsDismissed);

    return Ok(new
    {
        statistics = new
        {
            Students = await db.Students.CountAsync(s => s.SchoolId == SchoolId),
            Employees = employeesCount,
            Sections = await db.Sections.CountAsync(s => s.SchoolId == SchoolId),
            Subjects = await db.Subjects.CountAsync(s => s.SchoolId == SchoolId),
            StudentsWithDismissalWarning = await db.Students.CountAsync(s => s.SchoolId == SchoolId && s.DismissalWarning),
            EmployeesWithDismissalWarning = employeesWithDismissalWarning,
            OpenComplaints = await db.Complaints.CountAsync(c => c.SchoolId == SchoolId && c.Status == ComplaintStatus.Open),
            AbsentStudentsToday = await db.StudentAttendances.CountAsync(a =>
                a.Date == today && a.Status == AttendanceStatus.Absent &&
                db.Students.Any(s => s.Id == a.StudentId && s.SchoolId == SchoolId)),
        }
    });
}

[HttpGet("reports/student-absence")]
public async Task<IActionResult> StudentAbsenceReport()
{
    var report = await db.StudentAttendances
        .Where(a => db.Students.Any(s => s.Id == a.StudentId && s.SchoolId == SchoolId))
        .GroupBy(a => a.StudentId)
        .Select(g => new
        {
            StudentId = g.Key,
            StudentLocalNumber = db.Students  // ✅ Local ID
                .Where(s => s.Id == g.Key)
                .Select(s => s.LocalStudentNumber)
                .FirstOrDefault(),
            StudentName = db.Students  // ✅ إضافة اسم الطالب
                .Where(s => s.Id == g.Key)
                .Select(s => s.Name)
                .FirstOrDefault(),
            Total = g.Count(),
            Present = g.Count(a => a.Status == AttendanceStatus.Present),
            Absent = g.Count(a => a.Status == AttendanceStatus.Absent),
            Justified = g.Count(a => a.Status == AttendanceStatus.Justified),
            AttendanceRate = g.Count() > 0 ? 
                (decimal)g.Count(a => a.Status == AttendanceStatus.Present) / g.Count() * 100 : 0
        })
        .OrderByDescending(r => r.Absent)
        .ToListAsync();

    return Ok(new
    {
        success = true,
        message = "تم جلب تقرير غياب الطلاب بنجاح",
        data = report
    });
}

[HttpGet("reports/health-records")]
public async Task<IActionResult> HealthRecords()
{
    var records = await db.Students
        .Where(s => s.SchoolId == SchoolId)
        .Select(s => new
        {
            s.Id,
            StudentLocalNumber = s.LocalStudentNumber,  // ✅ Local ID
            s.Name,
            s.BloodType,
            s.ChronicDiseases,
            s.Allergies,
            s.HealthNotes,
            GuardianPhone = s.GuardianPhone,
            SectionName = s.Section != null ? s.Section.Name : null,
            LocalSectionNumber = s.Section != null ? s.Section.LocalSectionNumber : 0,  // ✅ Local ID
            GradeName = s.Section != null && s.Section.Grade != null ? s.Section.Grade.Name : null,
            LocalGradeNumber = s.Section != null && s.Section.Grade != null ? s.Section.Grade.LocalGradeNumber : 0  // ✅ Local ID
        })
        .ToListAsync();

    return Ok(new
    {
        success = true,
        message = "تم جلب السجلات الصحية للطلاب بنجاح",
        data = records
    });
}

// ============================================
// إعدادات العلامات
// ============================================

[HttpPut("mark-config")]
public async Task<IActionResult> UpdateMarkConfig(MarkConfigRequest request)
{
    var config = await db.MarkConfigs.FirstOrDefaultAsync(c => c.SchoolId == SchoolId);
    if (config is null)
    {
        config = new MarkConfig { SchoolId = SchoolId };
        db.MarkConfigs.Add(config);
    }
    config.MaxOral = request.MaxOral;
    config.MaxQuiz1 = request.MaxQuiz1;
    config.MaxQuiz2 = request.MaxQuiz2;
    config.MaxHomework = request.MaxHomework;
    config.MaxFinalExam = request.MaxFinalExam;
    config.PassPercent = request.PassPercent;
    await db.SaveChangesAsync();
    
    return Ok(new
    {
        success = true,
        message = "تم تحديث إعدادات العلامات بنجاح",
        data = config
    });
}

[HttpGet("mark-config")]
public async Task<IActionResult> GetMarkConfig()
{
    var config = await db.MarkConfigs.FirstOrDefaultAsync(c => c.SchoolId == SchoolId) 
                 ?? new MarkConfig { SchoolId = SchoolId };
    
    return Ok(new
    {
        success = true,
        message = "تم جلب إعدادات العلامات بنجاح",
        data = config
    });
}

// ============================================
// Feed - الإعلانات والأنشطة مع Local IDs
// ============================================

[HttpGet("feed")]
public async Task<IActionResult> GetFeed()
{
    var now = DateTime.UtcNow;
    
    var announcements = await db.Announcements
        .Where(a => a.SchoolId == SchoolId && 
                   a.IsActive &&
                   (a.Audience == AnnouncementAudience.All || 
                    a.Audience == AnnouncementAudience.Students) &&
                   (a.ExpiryDate == null || a.ExpiryDate > now))
        .OrderByDescending(a => a.CreatedAt)
        .Select(a => new
        {
            a.Id,
            LocalId = a.LocalAnnouncementId,  // ✅ Local ID
            a.Title,
            Description = a.Body,
            Date = a.CreatedAt.ToString("yyyy-MM-dd"),
            a.ExpiryDate,
            Type = "announcement"
        })
        .ToListAsync();

    var activities = await db.Activities
        .Where(a => a.SchoolId == SchoolId)
        .OrderByDescending(a => a.CreatedAt)
        .Select(a => new
        {
            a.Id,
            LocalId = a.LocalActivityId,  // ✅ Local ID
            Title = a.Name,
            Description = a.Description ?? a.Schedule ?? "",
            Date = a.CreatedAt.ToString("yyyy-MM-dd"),
            ExpiryDate = (DateTime?)null,
            Type = "activity"
        })
        .ToListAsync();

    var allItems = new List<object>();
    allItems.AddRange(announcements);
    allItems.AddRange(activities);

    var sortedFeed = allItems
        .OrderByDescending(x => DateTime.Parse(((dynamic)x).Date))
        .ToList();

    return Ok(new
    {
        success = true,
        message = "تم جلب البيانات بنجاح",
        data = new
        {
            announcements = announcements,
            activities = activities,
            feed = sortedFeed
        }
    });
}

// ============================================
// الملف الكامل للموجه - باستخدام Local IDs
// ============================================

[HttpGet("counselors/{localEmployeeNumber:int}/full-profile")]
public async Task<IActionResult> GetCounselorFullProfile(int localEmployeeNumber)
{
    var counselorSchool = await db.EmployeeSchools
        .Include(es => es.Employee)
        .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                   es.LocalEmployeeNumber == localEmployeeNumber &&
                                   es.Role == EmployeeRole.Counselor &&
                                   es.IsActive);

    if (counselorSchool is null)
        return NotFound(new { message = $"لا يوجد موجه برقم {localEmployeeNumber} في هذه المدرسة" });

    var counselor = counselorSchool.Employee;
    if (counselor is null)
        return NotFound(new { message = "الموجه غير موجود" });

    var counselorInfo = new
    {
        counselor.Id,
        counselor.Name,
        counselor.Email,
        counselor.Phone,
        counselor.CreatedAt,
        LocalEmployeeNumber = counselorSchool.LocalEmployeeNumber  // ✅ Local ID
    };

    var sections = await db.Sections
        .Include(s => s.Grade)
        .Where(s => s.CounselorId == counselor.Id)
        .Select(s => new
        {
            s.Id,
            s.Name,
            LocalSectionNumber = s.LocalSectionNumber,  // ✅ Local ID
            GradeName = s.Grade != null ? s.Grade.Name : null,
            LocalGradeNumber = s.Grade != null ? s.Grade.LocalGradeNumber : 0,  // ✅ Local ID
            StudentsCount = db.Students.Count(x => x.SectionId == s.Id)
        })
        .ToListAsync();

    var warnings = await db.Warnings
        .Include(w => w.Student)
        .Where(w => db.Students.Any(s => s.Id == w.StudentId && s.SectionId != null &&
            db.Sections.Any(x => x.Id == s.SectionId && x.CounselorId == counselor.Id)))
        .OrderByDescending(w => w.CreatedAt).Take(100)
        .Select(w => new
        {
            w.Id,
            w.StudentId,
            StudentName = w.Student != null ? w.Student.Name : null,
            StudentLocalNumber = w.Student != null ? w.Student.LocalStudentNumber : 0,  // ✅ Local ID
            w.Type,
            w.Reason,
            w.CreatedAt
        })
        .ToListAsync();

    var summons = await db.GuardianSummons
        .Include(s => s.Student)
        .Where(s => db.Students.Any(st => st.Id == s.StudentId && st.SectionId != null &&
            db.Sections.Any(x => x.Id == st.SectionId && x.CounselorId == counselor.Id)))
        .OrderByDescending(s => s.CreatedAt).Take(100)
        .Select(s => new
        {
            s.Id,
            s.StudentId,
            StudentName = s.Student != null ? s.Student.Name : null,
            StudentLocalNumber = s.Student != null ? s.Student.LocalStudentNumber : 0,  // ✅ Local ID
            s.Reason,
            s.Date,
            s.CreatedAt
        })
        .ToListAsync();

    var recentAttendance = await db.StudentAttendances
        .Include(a => a.Student)
        .Where(a => db.Students.Any(s => s.Id == a.StudentId && s.SectionId != null &&
            db.Sections.Any(x => x.Id == s.SectionId && x.CounselorId == counselor.Id)))
        .OrderByDescending(a => a.Date).Take(100)
        .Select(a => new
        {
            a.StudentId,
            StudentName = a.Student != null ? a.Student.Name : null,
            StudentLocalNumber = a.Student != null ? a.Student.LocalStudentNumber : 0,  // ✅ Local ID
            a.Date,
            a.Status
        })
        .ToListAsync();

    var totalStudents = await db.Students
        .CountAsync(s => s.SectionId != null &&
            db.Sections.Any(x => x.Id == s.SectionId && x.CounselorId == counselor.Id));

    var totalWarnings = warnings.Count;
    var totalSummons = summons.Count;

    return Ok(new
    {
        success = true,
        message = "تم جلب الملف الكامل للموجه بنجاح",
        data = new
        {
            Counselor = counselorInfo,
            Statistics = new
            {
                TotalSections = sections.Count,
                TotalStudents = totalStudents,
                TotalWarnings = totalWarnings,
                TotalSummons = totalSummons
            },
            Sections = sections,
            Warnings = warnings,
            Summons = summons,
            RecentAttendance = recentAttendance
        }
    });
}

// ============================================
// ترقية الطلاب (Promotion)
// ============================================

[HttpPost("promote-students")]
public async Task<IActionResult> PromoteStudents(PromoteRequest request)
{
    try
    {
        if (request.Semester != 2)
        {
            return BadRequest(new { success = false, message = "لا يمكن الترقية إلا في نهاية الفصل الدراسي الثاني" });
        }

        var result = await promotionService.PromoteStudentsAsync(
            SchoolId,
            request.CurrentGradeNumber,
            request.CurrentAcademicYear,
            request.NextAcademicYear,
            request.Semester);

        return Ok(new
        {
            success = true,
            message = "تمت ترقية الطلاب بنجاح",
            data = result
        });
    }
    catch (Exception ex)
    {
        return BadRequest(new { success = false, message = ex.Message });
    }
}

[HttpGet("promotion-report")]
public async Task<IActionResult> GetPromotionReport(
    [FromQuery] int localGradeNumber,
    [FromQuery] int academicYear,
    [FromQuery] int semester = 2)
{
    var grade = await db.Grades
        .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                  g.LocalGradeNumber == localGradeNumber &&
                                  g.AcademicYear == academicYear);

    if (grade is null)
        return NotFound(new { success = false, message = "الصف غير موجود" });

    var students = await db.Students
        .Include(s => s.Section)
        .ThenInclude(s => s!.Grade)
        .Where(s => s.SchoolId == SchoolId && 
                    s.Section != null &&
                    s.Section.GradeId == grade.Id &&
                    s.IsActive)
        .ToListAsync();

    var studentResults = new List<PromotionReportStudentDto>();
    foreach (var student in students)
    {
        var finalAverage = await promotionService.GetStudentFinalAverageAsync(student.Id);
        var semester1Average = await promotionService.GetStudentSemesterAverageAsync(student.Id, 1);
        var semester2Average = await promotionService.GetStudentSemesterAverageAsync(student.Id, 2);

        studentResults.Add(new PromotionReportStudentDto
        {
            Id = student.Id,
            Name = student.Name,
            LocalStudentNumber = student.LocalStudentNumber,  // ✅ Local ID
            Average = finalAverage,
            Semester1Average = semester1Average,
            Semester2Average = semester2Average,
            Passed = finalAverage >= 50,
            SectionName = student.Section?.Name,
            SectionLocalNumber = student.Section?.LocalSectionNumber ?? 0,  // ✅ Local ID
            GradeName = student.Section?.Grade?.Name ?? "",
            GradeLocalNumber = student.Section?.Grade?.LocalGradeNumber ?? 0  // ✅ Local ID
        });
    }

    var response = new PromotionReportResponse
    {
        GradeName = grade.Name,
        LocalGradeNumber = grade.LocalGradeNumber,  // ✅ Local ID
        TotalStudents = students.Count,
        PassedCount = studentResults.Count(s => s.Passed),
        FailedCount = studentResults.Count(s => !s.Passed),
        Students = studentResults.OrderByDescending(s => s.Average).ToList()
    };

    return Ok(new
    {
        success = true,
        message = "تم جلب تقرير الترقية بنجاح",
        data = response
    });
}
// ============================================
// دوال مساعدة لحفظ وحذف الصور
// ============================================

private async Task<string> SaveScheduleImageAsync(IFormFile image)
{
    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "schedules");
    
    if (!Directory.Exists(uploadsFolder))
        Directory.CreateDirectory(uploadsFolder);

    var uniqueFileName = $"{Guid.NewGuid()}_{image.FileName}";
    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

    using (var fileStream = new FileStream(filePath, FileMode.Create))
    {
        await image.CopyToAsync(fileStream);
    }

    return $"/uploads/schedules/{uniqueFileName}";
}

private void DeleteScheduleImageFile(string imageUrl)
{
    if (string.IsNullOrEmpty(imageUrl))
        return;

    var fileName = Path.GetFileName(imageUrl);
    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "schedules", fileName);

    if (System.IO.File.Exists(filePath))
        System.IO.File.Delete(filePath);
}
}