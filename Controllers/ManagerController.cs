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

    // ✅ التحقق من أن المستوى بين 1 و 12
    if (request.Level < 1 || request.Level > 12)
        return BadRequest(new { message = "المستوى يجب أن يكون بين 1 و 12" });

    // ✅ التحقق من وجود صف بنفس المستوى (بدون AcademicYear)
    var existingGrade = await db.Grades
        .AnyAsync(g => g.Level == request.Level && g.SchoolId == SchoolId);

    if (existingGrade)
        return BadRequest(new { 
            message = $"الصف {GetGradeNameByLevel(request.Level)} موجود بالفعل" 
        });

    // ✅ حساب LocalGradeNumber
    var usedNumbers = await db.Grades
        .Where(g => g.SchoolId == SchoolId)
        .Select(g => g.LocalGradeNumber)
        .ToListAsync();

    int newLocalNumber = 1;
    while (usedNumbers.Contains(newLocalNumber))
    {
        newLocalNumber++;
    }

    var gradeName = GetGradeNameByLevel(request.Level);

    var grade = new Grade
    {
        SchoolId = SchoolId,
        Name = gradeName,
        Level = request.Level,
        LocalGradeNumber = newLocalNumber,
        // ❌ حذف: AcademicYear = currentYear,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    db.Grades.Add(grade);
    await db.SaveChangesAsync();

    return Created($"api/manager/grades/{grade.LocalGradeNumber}", new
    {
        message = $"تم إنشاء {gradeName} بنجاح",
        grade = new
        {
            grade.Id,
            grade.Name,
            grade.Level,
            grade.LocalGradeNumber,
            grade.IsActive,
            grade.SchoolId,
            SchoolName = school.Name
        }
    });
}

// ✅ دالة مساعدة لتحويل المستوى إلى اسم الصف
private string GetGradeNameByLevel(int level)
{
    return level switch
    {
        1 => "الصف الأول",
        2 => "الصف الثاني",
        3 => "الصف الثالث",
        4 => "الصف الرابع",
        5 => "الصف الخامس",
        6 => "الصف السادس",
        7 => "الصف السابع",
        8 => "الصف الثامن",
        9 => "الصف التاسع",
        10 => "الصف العاشر",
        11 => "الصف الحادي عشر",
        12 => "الصف الثاني عشر",
        _ => $"الصف {level}"
    };
}

[HttpGet("grades")]
public async Task<IActionResult> GetGrades()
{
    var grades = await db.Grades
        .Include(g => g.Sections)
        .Where(g => g.SchoolId == SchoolId)
        .OrderBy(g => g.Level)
        .Select(g => new
        {
            g.Id,
            g.Name,
            g.LocalGradeNumber,
            g.Level,
            g.IsActive,
            g.SchoolId,
            g.CreatedAt,
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
        grade.Level,
        grade.IsActive,
        grade.SchoolId,
        grade.CreatedAt,
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

    if (request.Level < 1 || request.Level > 12)
        return BadRequest(new { message = "المستوى يجب أن يكون بين 1 و 12" });

    if (request.Level != grade.Level)
    {
        var existingLevel = await db.Grades
            .AnyAsync(g => g.Level == request.Level && g.SchoolId == SchoolId);
        
        if (existingLevel)
            return BadRequest(new { message = $"المستوى {request.Level} موجود بالفعل" });
    }

    grade.Level = request.Level;
    grade.Name = GetGradeNameByLevel(request.Level);

    await db.SaveChangesAsync();

    return Ok(new
    {
        message = $"تم تحديث الصف إلى {grade.Name} بنجاح",
        grade = new
        {
            grade.Id,
            grade.Name,
            grade.LocalGradeNumber,
            grade.Level,
            grade.IsActive,
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

    // ============================================
    // الطلاب المعرضين للخطر (At-Risk Students)
    // ============================================

    [HttpGet("at-risk-students")]
    public async Task<IActionResult> GetAtRiskStudents(
        [FromQuery] decimal threshold = 50,
        [FromQuery] int? localGradeNumber = null,
        [FromQuery] int? localSectionNumber = null,
        [FromQuery] int? semester = null)
    {
        var markConfig = await db.MarkConfigs
            .FirstOrDefaultAsync(c => c.SchoolId == SchoolId);

        var passPercent = markConfig?.PassPercent ?? 50;

        var query = db.Students
            .Include(s => s.Section)
                .ThenInclude(sec => sec!.Grade)
            .Where(s => s.SchoolId == SchoolId && s.IsActive);

        if (localGradeNumber.HasValue)
        {
            query = query.Where(s => s.Section != null && 
                                     s.Section.Grade != null && 
                                     s.Section.Grade.LocalGradeNumber == localGradeNumber.Value);
        }

        if (localSectionNumber.HasValue)
        {
            query = query.Where(s => s.Section != null && 
                                     s.Section.LocalSectionNumber == localSectionNumber.Value);
        }

        var students = await query.ToListAsync();

        var subjects = await db.Subjects
            .Where(s => s.SchoolId == SchoolId)
            .Select(s => new
            {
                s.Id,
                s.LocalSubjectId,
                s.Name
            })
            .ToListAsync();

        var subjectIds = subjects.Select(s => s.Id).ToList();

        var atRiskStudents = new List<AtRiskStudentDto>();

        foreach (var student in students)
        {
            var marksQuery = db.Marks
                .Where(m => m.StudentId == student.Id && subjectIds.Contains(m.SubjectId));

            if (semester.HasValue)
            {
                marksQuery = marksQuery.Where(m => m.Semester == semester.Value);
            }

            var marks = await marksQuery
                .Include(m => m.Subject)
                .ToListAsync();

            if (marks.Any())
            {
                var average = marks.Average(m => m.Total);
                
                if (average < threshold)
                {
                    var lastReport = await db.PerformanceReports
                        .Where(r => r.StudentId == student.Id)
                        .OrderByDescending(r => r.CreatedAt)
                        .Select(r => new { r.Behavior, r.Notes, r.CreatedAt })
                        .FirstOrDefaultAsync();

                    var failedSubjects = marks.Count(m => m.Total < passPercent);
                    var passedSubjects = marks.Count - failedSubjects;

                    var subjectMarks = marks.Select(m => new SubjectMarkDto
                    {
                        SubjectId = m.SubjectId,
                        LocalSubjectId = m.Subject != null ? m.Subject.LocalSubjectId : 0,
                        SubjectName = m.Subject != null ? m.Subject.Name : "غير معروف",
                        Total = m.Total,
                        IsPassed = m.Total >= passPercent,
                        Semester = m.Semester
                    }).ToList();

                    atRiskStudents.Add(new AtRiskStudentDto
                    {
                        Id = student.Id,
                        Name = student.Name,
                        Email = student.Email,
                        LocalStudentNumber = student.LocalStudentNumber,
                        SectionName = student.Section?.Name,
                        LocalSectionNumber = student.Section?.LocalSectionNumber ?? 0,
                        GradeName = student.Section?.Grade?.Name,
                        LocalGradeNumber = student.Section?.Grade?.LocalGradeNumber ?? 0,
                        GuardianName = student.GuardianName,
                        GuardianPhone = student.GuardianPhone,
                        Average = Math.Round(average, 2),
                        Threshold = threshold,
                        TotalMarks = marks.Count,
                        FailedSubjects = failedSubjects,
                        PassedSubjects = passedSubjects,
                        LastReport = lastReport,
                        SubjectMarks = subjectMarks
                    });
                }
            }
        }

        var totalStudents = students.Count;
        var totalAtRisk = atRiskStudents.Count;
        var averageAtRisk = totalAtRisk > 0 ? Math.Round(atRiskStudents.Average(s => s.Average), 2) : 0;
        var percentageAtRisk = totalStudents > 0 ? Math.Round((double)totalAtRisk / totalStudents * 100, 2) : 0;

        var minAverage = totalAtRisk > 0 ? atRiskStudents.Min(s => s.Average) : 0;
        var maxAverage = totalAtRisk > 0 ? atRiskStudents.Max(s => s.Average) : 0;

        return Ok(new
        {
            success = true,
            message = "تم جلب الطلاب المعرضين للخطر بنجاح",
            data = new
            {
                ReportInfo = new
                {
                    GeneratedAt = DateTime.UtcNow,
                    SchoolId = SchoolId,
                    PassPercent = passPercent,
                    Threshold = threshold,
                    Semester = semester
                },
                Statistics = new
                {
                    TotalStudents = totalStudents,
                    TotalAtRisk = totalAtRisk,
                    AverageAtRisk = averageAtRisk,
                    PercentageAtRisk = percentageAtRisk,
                    MinAverage = minAverage,
                    MaxAverage = maxAverage
                },
                Filters = new
                {
                    LocalGradeNumber = localGradeNumber,
                    LocalSectionNumber = localSectionNumber,
                    Semester = semester
                },
                Students = atRiskStudents
                    .OrderBy(s => s.Average)
                    .ToList()
            }
        });
    }

    [HttpGet("at-risk-students/{localStudentNumber:int}")]
    public async Task<IActionResult> GetAtRiskStudentDetails(int localStudentNumber)
    {
        var student = await db.Students
            .Include(s => s.Section)
                .ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalStudentNumber == localStudentNumber);

        if (student is null)
            return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber}" });

        var subjectIds = await db.Subjects
            .Where(s => s.SchoolId == SchoolId)
            .Select(s => s.Id)
            .ToListAsync();

        var marks = await db.Marks
            .Where(m => m.StudentId == student.Id && subjectIds.Contains(m.SubjectId))
            .Include(m => m.Subject)
            .Select(m => new
            {
                m.Id,
                m.SubjectId,
                SubjectName = m.Subject != null ? m.Subject.Name : null,
                LocalSubjectId = m.Subject != null ? m.Subject.LocalSubjectId : 0,
                m.Semester,
                m.Oral,
                m.Quiz1,
                m.Quiz2,
                m.Homework,
                m.FinalExam,
                m.Total,
                IsPassed = m.Total >= 50,
                m.UpdatedAt
            })
            .ToListAsync();

        var average = marks.Any() ? Math.Round(marks.Average(m => m.Total), 2) : 0;
        var failedSubjects = marks.Count(m => !m.IsPassed);
        var passedSubjects = marks.Count(m => m.IsPassed);

        var performanceReports = await db.PerformanceReports
            .Where(r => r.StudentId == student.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.SubjectId,
                SubjectName = r.Subject != null ? r.Subject.Name : null,
                LocalSubjectId = r.Subject != null ? r.Subject.LocalSubjectId : 0,
                r.Semester,
                r.Behavior,
                r.Notes,
                r.CreatedAt
            })
            .ToListAsync();

        var attendance = await db.StudentAttendances
            .Where(a => a.StudentId == student.Id)
            .OrderByDescending(a => a.Date)
            .Take(100)
            .Select(a => new
            {
                a.Date,
                a.Status
            })
            .ToListAsync();

        var totalAttendance = attendance.Count;
        var presentCount = attendance.Count(a => a.Status == AttendanceStatus.Present);
        var absentCount = attendance.Count(a => a.Status == AttendanceStatus.Absent);
        var attendancePercentage = totalAttendance > 0 
            ? Math.Round((double)presentCount / totalAttendance * 100, 2) 
            : 0;

        var warnings = await db.Warnings
            .Where(w => w.StudentId == student.Id)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new
            {
                w.Id,
                w.Type,
                w.Reason,
                w.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب تفاصيل الطالب المعرض للخطر بنجاح",
            data = new
            {
                Student = new
                {
                    student.Id,
                    student.Name,
                    student.Email,
                    student.LocalStudentNumber,
                    SectionName = student.Section?.Name,
                    LocalSectionNumber = student.Section?.LocalSectionNumber ?? 0,
                    GradeName = student.Section?.Grade?.Name,
                    LocalGradeNumber = student.Section?.Grade?.LocalGradeNumber ?? 0,
                    student.GuardianName,
                    student.GuardianPhone,
                    student.BirthDate,
                    student.Address
                },
                Statistics = new
                {
                    TotalMarks = marks.Count,
                    Average = average,
                    PassedSubjects = passedSubjects,
                    FailedSubjects = failedSubjects,
                    TotalAttendance = totalAttendance,
                    AttendancePercentage = attendancePercentage,
                    TotalWarnings = warnings.Count,
                    TotalReports = performanceReports.Count
                },
                Marks = marks.OrderByDescending(m => m.Semester).ThenBy(m => m.SubjectName).ToList(),
                PerformanceReports = performanceReports,
                Attendance = attendance.Take(30).ToList(),
                Warnings = warnings
            }
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

        var teacherId = teacherSchool.EmployeeId;
        var subjectId = subject.Id;

        var exists = await db.TeacherSubjects
            .AnyAsync(t => t.TeacherId == teacherId && t.SubjectId == subjectId);

        if (exists)
            return BadRequest(new { message = "هذا المعلم مرتبط بالفعل بهذه المادة" });

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
            localTeacherSubjectId = newLocalId
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
                LocalTeacherSubjectId = ts.LocalTeacherSubjectId,
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
    // ============================================
    // إدارة الموظفين - إنشاء، تحديث، حذف (باستخدام Local IDs)
    // ============================================

    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployee(EmployeeCreateLocalRequest request)
    {
        // 1. التحقق من وجود المدرسة
        var school = await db.Schools.FindAsync(SchoolId);
        if (school is null)
            return BadRequest(new { success = false, message = "المدرسة غير موجودة" });

        // 2. التحقق من عدم وجود موظف بنفس البريد الإلكتروني
        if (await db.Employees.AnyAsync(e => e.Email == request.Email))
            return BadRequest(new { success = false, message = "البريد الإلكتروني مستخدم بالفعل" });

        // 3. التحقق من عدم وجود موظف بنفس الرقم الوطني
        if (!string.IsNullOrEmpty(request.NationalId) && 
            await db.Employees.AnyAsync(e => e.NationalId == request.NationalId))
            return BadRequest(new { success = false, message = "الرقم الوطني مستخدم بالفعل" });

        // 4. التحقق من الأدوار الفريدة
        if (IsUniqueRole(request.Role))
        {
            var existingRole = await db.EmployeeSchools
                .AnyAsync(es => es.SchoolId == SchoolId &&
                               es.Role == request.Role &&
                               es.IsActive);

            if (existingRole)
                return BadRequest(new { success = false, message = $"الوظيفة '{GetRoleName(request.Role)}' مشغولة بالفعل في هذه المدرسة" });
        }

        // 5. إنشاء الموظف
        var employee = new Employee
        {
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            NationalId = request.NationalId ?? "",
            Phone = request.Phone ?? "",
            Address = request.Address ?? "",
            BirthDate = request.BirthDate,
            Qualification = request.Qualification ?? "",
            CreatedAt = DateTime.UtcNow
        };

        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        // 6. حساب LocalEmployeeNumber
        var maxLocalNumber = await db.EmployeeSchools
            .Where(es => es.SchoolId == SchoolId)
            .Select(es => (int?)es.LocalEmployeeNumber)
            .MaxAsync() ?? 0;

        int newLocalNumber = maxLocalNumber + 1;

        // 7. ربط الموظف بالمدرسة
        var employeeSchool = new EmployeeSchool
        {
            EmployeeId = employee.Id,
            SchoolId = SchoolId,
            LocalEmployeeNumber = newLocalNumber,
            Role = request.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.EmployeeSchools.Add(employeeSchool);

        // 8. إذا كان معلم، أضف TeacherAssignment
        if (request.Role == EmployeeRole.Teacher)
        {
            db.TeacherAssignments.Add(new TeacherAssignment
            {
                EmployeeId = employee.Id,
                SchoolId = SchoolId
            });
        }

        await db.SaveChangesAsync();

        // 9. إشعار للموظف الجديد
        await notifier.SendAsync(employee.Id, UserType.Employee,
            "مرحباً في المدرسة",
            $"تم تسجيلك في مدرسة '{school.Name}' برقم موظف {newLocalNumber}",
            "registration");

        return Created($"api/manager/employees/{newLocalNumber}", new
        {
            success = true,
            message = "تم إنشاء الموظف وربطه بالمدرسة بنجاح",
            data = new
            {
                employee.Id,
                employee.Name,
                employee.Email,
                LocalEmployeeNumber = newLocalNumber,
                employee.NationalId,
                employee.Phone,
                employee.Address,
                employee.BirthDate,
                employee.Qualification,
                Role = request.Role.ToString(),
                RoleName = GetRoleName(request.Role),
                SchoolId = SchoolId,
                SchoolName = school.Name,
                employee.CreatedAt
            }
        });
    }

    [HttpPut("employees/{localEmployeeNumber:int}")]
    public async Task<IActionResult> UpdateEmployee(int localEmployeeNumber, EmployeeUpdateLocalRequest request)
    {
        // 1. البحث عن الموظف في المدرسة
        var employeeSchool = await db.EmployeeSchools
            .Include(es => es.Employee)
            .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                      es.LocalEmployeeNumber == localEmployeeNumber &&
                                      es.IsActive);

        if (employeeSchool is null)
            return NotFound(new { success = false, message = $"لا يوجد موظف برقم {localEmployeeNumber} في هذه المدرسة" });

        var employee = employeeSchool.Employee;
        if (employee is null)
            return NotFound(new { success = false, message = "الموظف غير موجود" });

        // 2. تحديث البيانات الأساسية
        if (!string.IsNullOrWhiteSpace(request.Name))
            employee.Name = request.Name;

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var existingEmail = await db.Employees
                .AnyAsync(e => e.Email == request.Email && e.Id != employee.Id);
            
            if (existingEmail)
                return BadRequest(new { success = false, message = "البريد الإلكتروني مستخدم بالفعل" });

            employee.Email = request.Email;
        }

        if (!string.IsNullOrWhiteSpace(request.NationalId))
        {
            var existingNationalId = await db.Employees
                .AnyAsync(e => e.NationalId == request.NationalId && e.Id != employee.Id);
            
            if (existingNationalId)
                return BadRequest(new { success = false, message = "الرقم الوطني مستخدم بالفعل" });

            employee.NationalId = request.NationalId;
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
            employee.Phone = request.Phone;

        if (!string.IsNullOrWhiteSpace(request.Address))
            employee.Address = request.Address;

        if (request.BirthDate.HasValue)
        {
            // التحقق من العمر
            var age = CalculateAge(request.BirthDate.Value);
            if (age < 18)
                return BadRequest(new { success = false, message = "عمر الموظف يجب أن يكون 18 سنة على الأقل" });
                
            employee.BirthDate = request.BirthDate;
        }

        if (!string.IsNullOrWhiteSpace(request.Qualification))
            employee.Qualification = request.Qualification;

        if (!string.IsNullOrWhiteSpace(request.Password))
            employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // 3. تحديث الدور إذا تم إرساله
        if (request.Role.HasValue && request.Role.Value != employeeSchool.Role)
        {
            // التحقق من الأدوار الفريدة
            if (IsUniqueRole(request.Role.Value))
            {
                var existingRole = await db.EmployeeSchools
                    .AnyAsync(es => es.Role == request.Role.Value &&
                                   es.SchoolId == SchoolId &&
                                   es.EmployeeId != employee.Id &&
                                   es.IsActive);

                if (existingRole)
                    return BadRequest(new { success = false, message = $"الدور '{GetRoleName(request.Role.Value)}' مشغول بالفعل في هذه المدرسة" });
            }

            // إذا كان الدور الجديد معلم، أضف TeacherAssignment
            if (request.Role.Value == EmployeeRole.Teacher)
            {
                var existingAssignment = await db.TeacherAssignments
                    .FirstOrDefaultAsync(t => t.EmployeeId == employee.Id && t.SchoolId == SchoolId);

                if (existingAssignment is null)
                {
                    db.TeacherAssignments.Add(new TeacherAssignment
                    {
                        EmployeeId = employee.Id,
                        SchoolId = SchoolId
                    });
                }
            }
            else
            {
                // إذا لم يعد معلم، احذف TeacherAssignment
                var assignments = await db.TeacherAssignments
                    .Where(t => t.EmployeeId == employee.Id && t.SchoolId == SchoolId)
                    .ToListAsync();

                if (assignments.Any())
                    db.TeacherAssignments.RemoveRange(assignments);
            }

            employeeSchool.Role = request.Role.Value;
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "تم تحديث بيانات الموظف بنجاح",
            data = new
            {
                employee.Id,
                employee.Name,
                employee.Email,
                LocalEmployeeNumber = localEmployeeNumber,
                employee.NationalId,
                employee.Phone,
                employee.Address,
                employee.BirthDate,
                employee.Qualification,
                Role = employeeSchool.Role.ToString(),
                RoleName = GetRoleName(employeeSchool.Role),
                employee.CreatedAt
            }
        });
    }

    [HttpDelete("employees/{localEmployeeNumber:int}")]
    public async Task<IActionResult> DeleteEmployee(int localEmployeeNumber)
    {
        // 1. البحث عن الموظف في المدرسة
        var employeeSchool = await db.EmployeeSchools
            .Include(es => es.Employee)
            .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                      es.LocalEmployeeNumber == localEmployeeNumber &&
                                      es.IsActive);

        if (employeeSchool is null)
            return NotFound(new { success = false, message = $"لا يوجد موظف برقم {localEmployeeNumber} في هذه المدرسة" });

        var employee = employeeSchool.Employee;
        if (employee is null)
            return NotFound(new { success = false, message = "الموظف غير موجود" });

        // 2. التحقق من أن الموظف ليس مدير المدرسة
        if (employeeSchool.Role == EmployeeRole.Principal)
            return BadRequest(new { success = false, message = "لا يمكن حذف مدير المدرسة" });

        // 3. التحقق من أن الموظف ليس له علاقات نشطة في مدارس أخرى
        var activeInOtherSchools = await db.EmployeeSchools
            .AnyAsync(es => es.EmployeeId == employee.Id &&
                           es.SchoolId != SchoolId &&
                           es.IsActive);

        if (activeInOtherSchools)
        {
            // إذا كان يعمل في مدارس أخرى، فقط قم بإلغاء الربط مع هذه المدرسة
            employeeSchool.IsActive = false;

            // حذف TeacherAssignment لهذه المدرسة فقط
            var assignments = await db.TeacherAssignments
                .Where(t => t.EmployeeId == employee.Id && t.SchoolId == SchoolId)
                .ToListAsync();

            if (assignments.Any())
                db.TeacherAssignments.RemoveRange(assignments);

            await db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "تم إلغاء ربط الموظف بالمدرسة بنجاح (لا يزال يعمل في مدارس أخرى)",
                data = new
                {
                    employee.Id,
                    employee.Name,
                    LocalEmployeeNumber = localEmployeeNumber,
                    SchoolId = SchoolId,
                    StillActiveInOtherSchools = true
                }
            });
        }

        // 4. حذف جميع البيانات المرتبطة
        // حضور الموظف
        var attendances = await db.EmployeeAttendances
            .Where(a => a.EmployeeId == employee.Id)
            .ToListAsync();
        if (attendances.Any())
            db.EmployeeAttendances.RemoveRange(attendances);

        // الإجازات
        var leaves = await db.Leaves
            .Where(l => l.EmployeeId == employee.Id)
            .ToListAsync();
        if (leaves.Any())
            db.Leaves.RemoveRange(leaves);

        // TeacherAssignments
        var teacherAssignments = await db.TeacherAssignments
            .Where(t => t.EmployeeId == employee.Id)
            .ToListAsync();
        if (teacherAssignments.Any())
            db.TeacherAssignments.RemoveRange(teacherAssignments);

        // TeacherSubjects
        var teacherSubjects = await db.TeacherSubjects
            .Where(t => t.TeacherId == employee.Id)
            .ToListAsync();
        if (teacherSubjects.Any())
            db.TeacherSubjects.RemoveRange(teacherSubjects);

        // TeacherGrades
        var teacherGrades = await db.TeacherGrades
            .Where(t => t.TeacherId == employee.Id)
            .ToListAsync();
        if (teacherGrades.Any())
            db.TeacherGrades.RemoveRange(teacherGrades);

        // 5. حذف جميع EmployeeSchools
        var allEmployeeSchools = await db.EmployeeSchools
            .Where(es => es.EmployeeId == employee.Id)
            .ToListAsync();
        if (allEmployeeSchools.Any())
            db.EmployeeSchools.RemoveRange(allEmployeeSchools);

        // 6. حذف الموظف
        db.Employees.Remove(employee);
        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "تم حذف الموظف بنجاح",
            data = new
            {
                employee.Id,
                employee.Name,
                employee.Email,
                LocalEmployeeNumber = localEmployeeNumber,
                SchoolId = SchoolId
            }
        });
    }

    [HttpPut("employees/{localEmployeeNumber:int}/role")]
    public async Task<IActionResult> UpdateEmployeeRole(int localEmployeeNumber, [FromBody] EmployeeRole newRole)
    {
        // 1. البحث عن الموظف
        var employeeSchool = await db.EmployeeSchools
            .Include(es => es.Employee)
            .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                      es.LocalEmployeeNumber == localEmployeeNumber &&
                                      es.IsActive);

        if (employeeSchool is null)
            return NotFound(new { success = false, message = $"لا يوجد موظف برقم {localEmployeeNumber} في هذه المدرسة" });

        // 2. التحقق من الأدوار الفريدة
        if (IsUniqueRole(newRole))
        {
            var existingRole = await db.EmployeeSchools
                .AnyAsync(es => es.Role == newRole &&
                               es.SchoolId == SchoolId &&
                               es.EmployeeId != employeeSchool.EmployeeId &&
                               es.IsActive);

            if (existingRole)
                return BadRequest(new { success = false, message = $"الدور '{GetRoleName(newRole)}' مشغول بالفعل في هذه المدرسة" });
        }

        // 3. تحديث TeacherAssignment إذا لزم الأمر
        if (newRole == EmployeeRole.Teacher)
        {
            var existingAssignment = await db.TeacherAssignments
                .FirstOrDefaultAsync(t => t.EmployeeId == employeeSchool.EmployeeId && t.SchoolId == SchoolId);

            if (existingAssignment is null)
            {
                db.TeacherAssignments.Add(new TeacherAssignment
                {
                    EmployeeId = employeeSchool.EmployeeId,
                    SchoolId = SchoolId
                });
            }
        }
        else
        {
            var assignments = await db.TeacherAssignments
                .Where(t => t.EmployeeId == employeeSchool.EmployeeId && t.SchoolId == SchoolId)
                .ToListAsync();

            if (assignments.Any())
                db.TeacherAssignments.RemoveRange(assignments);
        }

        // 4. تحديث الدور
        employeeSchool.Role = newRole;
        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "تم تحديث دور الموظف بنجاح",
            data = new
            {
                LocalEmployeeNumber = localEmployeeNumber,
                EmployeeId = employeeSchool.EmployeeId,
                EmployeeName = employeeSchool.Employee?.Name,
                NewRole = newRole.ToString(),
                NewRoleName = GetRoleName(newRole),
                SchoolId = SchoolId
            }
        });
    }

    [HttpPost("employees/{localEmployeeNumber:int}/dismiss")]
    public async Task<IActionResult> DismissEmployee(int localEmployeeNumber)
    {
        // 1. البحث عن الموظف
        var employeeSchool = await db.EmployeeSchools
            .Include(es => es.Employee)
            .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                      es.LocalEmployeeNumber == localEmployeeNumber &&
                                      es.IsActive);

        if (employeeSchool is null)
            return NotFound(new { success = false, message = $"لا يوجد موظف برقم {localEmployeeNumber} في هذه المدرسة" });

        var employee = employeeSchool.Employee;
        if (employee is null)
            return NotFound(new { success = false, message = "الموظف غير موجود" });

        // 2. التحقق من أنه ليس مدير المدرسة
        if (employeeSchool.Role == EmployeeRole.Principal)
            return BadRequest(new { success = false, message = "لا يمكن فصل مدير المدرسة" });

        // 3. تنفيذ الفصل
        employeeSchool.IsActive = false;
        employee.IsDismissed = true;

        // 4. حذف TeacherAssignment
        var assignments = await db.TeacherAssignments
            .Where(t => t.EmployeeId == employee.Id && t.SchoolId == SchoolId)
            .ToListAsync();

        if (assignments.Any())
            db.TeacherAssignments.RemoveRange(assignments);

        await db.SaveChangesAsync();

        // 5. إشعار بالفصل
        await notifier.SendAsync(employee.Id, UserType.Employee, 
            "قرار فصل", 
            "تم فصلك من العمل", 
            "dismissal");

        return Ok(new
        {
            success = true,
            message = "تم فصل الموظف بنجاح",
            data = new
            {
                LocalEmployeeNumber = localEmployeeNumber,
                EmployeeId = employee.Id,
                EmployeeName = employee.Name,
                SchoolId = SchoolId
            }
        });
    }

    [HttpGet("teachers")]
    public async Task<IActionResult> GetTeachers()
    {
        var teachers = await db.EmployeeSchools
            .Where(es => es.SchoolId == SchoolId && es.IsActive && es.Role == EmployeeRole.Teacher)
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
                es.Employee.Address,
                es.Employee.BirthDate,
                es.Role,
                RoleName = GetRoleName(es.Role),
                es.IsActive,
                es.CreatedAt,
                
                // ✅ Get subjects assigned to this teacher
                Subjects = db.TeacherSubjects
                    .Where(ts => ts.TeacherId == es.EmployeeId)
                    .Select(ts => new
                    {
                        ts.SubjectId,
                        SubjectName = ts.Subject != null ? ts.Subject.Name : null,
                        ts.Subject!.LocalSubjectId
                    })
                    .ToList(),
                
                // ✅ Get sections assigned to this teacher
                Sections = db.TeacherGrades
                    .Where(tg => tg.TeacherId == es.EmployeeId)
                    .Select(tg => new
                    {
                        tg.SectionId,
                        SectionName = tg.Section != null ? tg.Section.Name : null,
                        tg.Section!.LocalSectionNumber,
                        GradeName = tg.Section != null && tg.Section.Grade != null ? tg.Section.Grade.Name : null,
                        LocalGradeNumber = tg.Section != null && tg.Section.Grade != null ? tg.Section.Grade.LocalGradeNumber : 0
                    })
                    .ToList()
            })
            .ToListAsync();

        return Ok(teachers);
    }

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
    // جلب جميع الموجهين (Counselors)
    // ============================================

    [HttpGet("counselors")]
    public async Task<IActionResult> GetCounselors()
    {
        var counselors = await db.EmployeeSchools
            .Include(es => es.Employee)
            .Where(es => es.SchoolId == SchoolId &&
                         es.Role == EmployeeRole.Counselor &&
                         es.IsActive)
            .OrderBy(es => es.LocalEmployeeNumber)
            .Select(es => new
            {
                es.LocalEmployeeNumber,
                es.EmployeeId,
                es.Employee!.Name,
                es.Employee.Email,
                es.Employee.NationalId,
                es.Employee.Phone,
                es.Employee.Address,
                es.Employee.BirthDate,
                es.Employee.Qualification,
                es.Employee.CreatedAt,
                // عدد الطلاب الذين يشرف عليهم الموجه
                StudentsCount = db.Students
                    .Count(s => s.SchoolId == SchoolId &&
                               s.Section != null &&
                               s.Section.CounselorId == es.EmployeeId &&
                               s.IsActive),
                // عدد الشعب التي يشرف عليها الموجه
                SectionsCount = db.Sections
                    .Count(s => s.SchoolId == SchoolId &&
                               s.CounselorId == es.EmployeeId),
                // قائمة الشعب التي يشرف عليها
                Sections = db.Sections
                    .Where(s => s.SchoolId == SchoolId &&
                               s.CounselorId == es.EmployeeId)
                    .Select(s => new
                    {
                        s.Id,
                        s.Name,
                        s.LocalSectionNumber,
                        GradeName = s.Grade != null ? s.Grade.Name : null,
                        LocalGradeNumber = s.Grade != null ? s.Grade.LocalGradeNumber : 0
                    })
                    .ToList()
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب قائمة الموجهين بنجاح",
            data = new
            {
                totalCounselors = counselors.Count,
                counselors = counselors
            }
        });
    }

    [HttpGet("counselors/{localEmployeeNumber:int}")]
    public async Task<IActionResult> GetCounselor(int localEmployeeNumber)
    {
        var counselorSchool = await db.EmployeeSchools
            .Include(es => es.Employee)
            .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                       es.LocalEmployeeNumber == localEmployeeNumber &&
                                       es.Role == EmployeeRole.Counselor &&
                                       es.IsActive);

        if (counselorSchool is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد موجه برقم {localEmployeeNumber} في هذه المدرسة" 
            });

        var counselor = counselorSchool.Employee;
        if (counselor is null)
            return NotFound(new { 
                success = false, 
                message = "الموجه غير موجود" 
            });

        // جلب الشعب التي يشرف عليها الموجه
        var sections = await db.Sections
            .Include(s => s.Grade)
            .Where(s => s.SchoolId == SchoolId &&
                       s.CounselorId == counselor.Id)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.LocalSectionNumber,
                s.CreatedAt,
                GradeName = s.Grade != null ? s.Grade.Name : null,
                LocalGradeNumber = s.Grade != null ? s.Grade.LocalGradeNumber : 0,
                StudentsCount = db.Students.Count(st => st.SectionId == s.Id && st.IsActive)
            })
            .ToListAsync();

        // جلب الطلاب الذين يشرف عليهم الموجه
        var students = await db.Students
            .Include(s => s.Section)
                .ThenInclude(sec => sec!.Grade)
            .Where(s => s.SchoolId == SchoolId &&
                       s.Section != null &&
                       s.Section.CounselorId == counselor.Id &&
                       s.IsActive)
            .OrderBy(s => s.LocalStudentNumber)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Email,
                s.LocalStudentNumber,
                s.GuardianName,
                s.GuardianPhone,
                s.BirthDate,
                SectionName = s.Section != null ? s.Section.Name : null,
                LocalSectionNumber = s.Section != null ? s.Section.LocalSectionNumber : 0,
                GradeName = s.Section != null && s.Section.Grade != null ? s.Section.Grade.Name : null,
                LocalGradeNumber = s.Section != null && s.Section.Grade != null ? s.Section.Grade.LocalGradeNumber : 0,
                s.CreatedAt
            })
            .ToListAsync();

        // جلب التحذيرات الصادرة عن الموجه
        var warnings = await db.Warnings
            .Include(w => w.Student)
            .Where(w => db.Students.Any(s => s.Id == w.StudentId && 
                                             s.Section != null && 
                                             s.Section.CounselorId == counselor.Id))
            .OrderByDescending(w => w.CreatedAt)
            .Take(50)
            .Select(w => new
            {
                w.Id,
                w.StudentId,
                StudentName = w.Student != null ? w.Student.Name : null,
                StudentLocalNumber = w.Student != null ? w.Student.LocalStudentNumber : 0,
                w.Type,
                w.Reason,
                w.CreatedAt
            })
            .ToListAsync();

        // جلب استدعاءات ولي الأمر الصادرة عن الموجه
        var summons = await db.GuardianSummons
            .Include(s => s.Student)
            .Where(s => db.Students.Any(st => st.Id == s.StudentId && 
                                              st.Section != null && 
                                              st.Section.CounselorId == counselor.Id))
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .Select(s => new
            {
                s.Id,
                s.StudentId,
                StudentName = s.Student != null ? s.Student.Name : null,
                StudentLocalNumber = s.Student != null ? s.Student.LocalStudentNumber : 0,
                s.Reason,
                s.Date,
                s.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب بيانات الموجه بنجاح",
            data = new
            {
                Counselor = new
                {
                    counselor.Id,
                    counselor.Name,
                    counselor.Email,
                    counselor.NationalId,
                    counselor.Phone,
                    counselor.Address,
                    counselor.BirthDate,
                    counselor.Qualification,
                    counselor.CreatedAt,
                    LocalEmployeeNumber = counselorSchool.LocalEmployeeNumber
                },
                Statistics = new
                {
                    TotalSections = sections.Count,
                    TotalStudents = students.Count,
                    TotalWarnings = warnings.Count,
                    TotalSummons = summons.Count
                },
                Sections = sections,
                Students = students,
                RecentWarnings = warnings,
                RecentSummons = summons
            }
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
            "registration");

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

        // 1. حذف العلامات (Marks)
        var marks = await db.Marks
            .Where(m => m.StudentId == student.Id)
            .ToListAsync();
        if (marks.Any())
            db.Marks.RemoveRange(marks);

        // 2. حذف بطاقات التقارير (ReportCards)
        var reportCards = await db.ReportCards
            .Where(r => r.StudentId == student.Id)
            .ToListAsync();
        if (reportCards.Any())
            db.ReportCards.RemoveRange(reportCards);

        // 3. حذف سجلات الحضور (StudentAttendances)
        var attendances = await db.StudentAttendances
            .Where(a => a.StudentId == student.Id)
            .ToListAsync();
        if (attendances.Any())
            db.StudentAttendances.RemoveRange(attendances);

        // 4. حذف التحذيرات (Warnings)
        var warnings = await db.Warnings
            .Where(w => w.StudentId == student.Id)
            .ToListAsync();
        if (warnings.Any())
            db.Warnings.RemoveRange(warnings);

        // 5. حذف العقوبات (Punishments)
        var punishments = await db.Punishments
            .Where(p => p.StudentId == student.Id)
            .ToListAsync();
        if (punishments.Any())
            db.Punishments.RemoveRange(punishments);

        // 6. حذف التسجيلات في الأنشطة (ActivityRegistrations)
        var activityRegistrations = await db.ActivityRegistrations
            .Where(r => r.StudentId == student.Id)
            .ToListAsync();
        if (activityRegistrations.Any())
            db.ActivityRegistrations.RemoveRange(activityRegistrations);

        // 7. ✅ حذف إعارات الكتب (BookLoans) - باستخدام StudentId مباشرة
        var bookLoans = await db.BookLoans
            .Where(l => l.StudentId == student.Id)
            .ToListAsync();
        if (bookLoans.Any())
            db.BookLoans.RemoveRange(bookLoans);

        // 8. ✅ حذف حجوزات الكتب (BookReservations) - باستخدام StudentId مباشرة
        var bookReservations = await db.BookReservations
            .Where(r => r.StudentId == student.Id)
            .ToListAsync();
        if (bookReservations.Any())
            db.BookReservations.RemoveRange(bookReservations);

        // 9. ✅ حذف طلبات الاستعارة (BookLoanRequests) - باستخدام StudentId مباشرة
        var loanRequests = await db.BookLoanRequests
            .Where(r => r.StudentId == student.Id)
            .ToListAsync();
        if (loanRequests.Any())
            db.BookLoanRequests.RemoveRange(loanRequests);

        // 10. حذف سجل تطور الصفوف (StudentGradeHistory)
        var gradeHistory = await db.StudentGradeHistory
            .Where(h => h.StudentId == student.Id)
            .ToListAsync();
        if (gradeHistory.Any())
            db.StudentGradeHistory.RemoveRange(gradeHistory);

        // 11. حذف الشكاوى (Complaints) - إذا كان الطالب هو صاحب الشكوى
        var complaints = await db.Complaints
            .Where(c => c.FromUserId == student.Id && c.FromUserType == UserType.Student)
            .ToListAsync();
        if (complaints.Any())
            db.Complaints.RemoveRange(complaints);

        // 12. حذف الإشعارات (Notifications) - الخاصة بالطالب
        var notifications = await db.Notifications
            .Where(n => n.UserId == student.Id && n.UserType == UserType.Student)
            .ToListAsync();
        if (notifications.Any())
            db.Notifications.RemoveRange(notifications);

        // 13. حذف استدعاءات ولي الأمر (GuardianSummons)
        var summons = await db.GuardianSummons
            .Where(s => s.StudentId == student.Id)
            .ToListAsync();
        if (summons.Any())
            db.GuardianSummons.RemoveRange(summons);

        // ❌ إزالة LibraryMember - لم نعد نستخدمه
        // var libraryMember = await db.LibraryMembers
        //     .FirstOrDefaultAsync(m => m.StudentId == student.Id);
        // if (libraryMember is not null) { ... }

        // 14. أخيراً حذف الطالب نفسه
        db.Students.Remove(student);
        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "تم حذف الطالب وجميع البيانات المرتبطة بنجاح",
            data = new
            {
                localStudentNumber = localStudentNumber,
                studentName = student.Name,
                studentId = student.Id
            }
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
                EmployeeName = db.Employees
                    .Where(e => e.Id == a.EmployeeId)
                    .Select(e => e.Name)
                    .FirstOrDefault(),
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
    // صور الجداول - جميع العمليات (للمدير فقط)
    // ============================================

    // ============================================
    // 1. صورة جدول المعلم - رفع
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

    // ============================================
    // 2. صورة جدول المعلم - جلب
    // ============================================

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

    [HttpGet("sections")]
    public async Task<IActionResult> GetAllSections()
    {
        var sections = await db.Sections
            .Include(s => s.Grade)
            .Include(s => s.Counselor)
            .Where(s => s.SchoolId == SchoolId)
            .OrderBy(s => s.Grade != null ? s.Grade.LocalGradeNumber : 0)
            .ThenBy(s => s.LocalSectionNumber)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.LocalSectionNumber,
                s.SchoolId,
                s.CreatedAt,
                GradeId = s.GradeId,
                GradeName = s.Grade != null ? s.Grade.Name : null,
                LocalGradeNumber = s.Grade != null ? s.Grade.LocalGradeNumber : (int?)null,
                CounselorId = s.CounselorId,
                LocalCounselorNumber = db.EmployeeSchools
                    .Where(es => es.EmployeeId == s.CounselorId && 
                                es.SchoolId == SchoolId && 
                                es.IsActive)
                    .Select(es => (int?)es.LocalEmployeeNumber)
                    .FirstOrDefault(),
                CounselorName = s.Counselor != null ? s.Counselor.Name : null,
                StudentsCount = db.Students.Count(st => st.SectionId == s.Id && st.IsActive),
                Teachers = db.TeacherGrades
                    .Where(tg => tg.SectionId == s.Id)
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
                            .Where(sub => sub.Id == tg.SubjectId)
                            .Select(sub => sub.LocalSubjectId)
                            .FirstOrDefault(),
                        SubjectName = tg.Subject != null ? tg.Subject.Name : null,
                        CreatedAt = tg.CreatedAt
                    }).ToList()
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب جميع الشعب بنجاح",
            data = new
            {
                totalSections = sections.Count,
                sections = sections
            }
        });
    }

    // ============================================
    // 3. صورة جدول المعلم - حذف (باستخدام LocalEmployeeNumber)
    // ============================================

    [HttpDelete("schedule-images/teacher/{localEmployeeNumber:int}")]
    public async Task<IActionResult> DeleteTeacherScheduleImage(int localEmployeeNumber)
    {
        // ✅ 1. البحث عن الموظف في المدرسة
        var employeeSchool = await db.EmployeeSchools
            .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                       es.LocalEmployeeNumber == localEmployeeNumber &&
                                       es.IsActive);

        if (employeeSchool is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد موظف برقم {localEmployeeNumber} في هذه المدرسة" 
            });

        // ✅ 2. جلب الموظف
        var employee = await db.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeSchool.EmployeeId);

        if (employee is null)
            return NotFound(new { 
                success = false, 
                message = "الموظف غير موجود" 
            });

        // ✅ 3. جلب صورة الجدول للمعلم (بدون التحقق من Role Teacher هنا)
        var image = await db.ScheduleImages
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.TeacherId == employee.Id && 
                                      s.Type == ScheduleImageType.Teacher);

        if (image is null)
            return NotFound(new { 
                success = false, 
                message = $"لا توجد صورة جدول للمعلم رقم {localEmployeeNumber}" 
            });

        // ✅ 4. حذف الملف الفعلي
        var fileDeleted = DeleteScheduleImageFile(image.ImageUrl);

        // ✅ 5. حذف السجل من قاعدة البيانات
        db.ScheduleImages.Remove(image);
        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "تم حذف صورة جدول المعلم بنجاح",
            data = new
            {
                LocalEmployeeNumber = localEmployeeNumber,
                EmployeeName = employee.Name,
                ImageUrl = image.ImageUrl,
                FileDeleted = fileDeleted
            }
        });
    }

    // ============================================
    // 4. صورة جدول الشعبة - رفع
    // ============================================

    [HttpPost("schedule-images/section")]
    public async Task<IActionResult> UploadSectionScheduleImage([FromForm] ScheduleImageRequest request)
    {
        var school = await db.Schools.FindAsync(SchoolId);
        if (school is null)
            return BadRequest(new { message = "المدرسة غير موجودة" });

        var grade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.LocalGradeNumber == request.LocalGradeNumber);
        if (grade is null)
            return BadRequest(new { message = $"لا يوجد صف برقم {request.LocalGradeNumber} في هذه المدرسة" });

        var section = await db.Sections
            .FirstOrDefaultAsync(s => s.GradeId == grade.Id && 
                                      s.LocalSectionNumber == request.LocalSectionNumber &&
                                      s.SchoolId == SchoolId);
        
        if (section is null)
            return BadRequest(new { message = $"لا توجد شعبة برقم {request.LocalSectionNumber} في الصف {request.LocalGradeNumber}" });

        var imageUrl = await SaveScheduleImageAsync(request.Image);

        var existingImage = await db.ScheduleImages
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.SectionId == section.Id && 
                                      s.Type == ScheduleImageType.Section);

        if (existingImage is not null)
        {
            DeleteScheduleImageFile(existingImage.ImageUrl);
            db.ScheduleImages.Remove(existingImage);
            await db.SaveChangesAsync();
        }

        var scheduleImage = new ScheduleImage
        {
            SchoolId = SchoolId,
            GradeId = grade.Id,
            SectionId = section.Id,
            TeacherId = null,
            ImageUrl = imageUrl,
            Description = request.Description ?? $"جدول الشعبة {section.Name} - {grade.Name}",
            Type = ScheduleImageType.Section,
            CreatedAt = DateTime.UtcNow
        };

        db.ScheduleImages.Add(scheduleImage);
        await db.SaveChangesAsync();

        return Created($"api/manager/schedule-images/section/{scheduleImage.Id}", new
        {
            message = "تم رفع صورة جدول الشعبة بنجاح",
            scheduleImage = new
            {
                scheduleImage.Id,
                scheduleImage.ImageUrl,
                scheduleImage.Description,
                LocalGradeNumber = grade.LocalGradeNumber,
                GradeName = grade.Name,
                LocalSectionNumber = section.LocalSectionNumber,
                SectionName = section.Name,
                scheduleImage.CreatedAt
            }
        });
    }

    // ============================================
    // 5. صورة جدول الشعبة - جلب
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
            .Select(s => new
            {
                s.Id,
                s.ImageUrl,
                s.Description,
                s.CreatedAt,
                LocalGradeNumber = grade.LocalGradeNumber,
                GradeName = grade.Name,
                LocalSectionNumber = section.LocalSectionNumber,
                SectionName = section.Name
            })
            .FirstOrDefaultAsync();

        if (image is null)
            return NotFound(new { message = "لا توجد صورة جدول لهذه الشعبة" });

        return Ok(image);
    }

    // ============================================
    // 6. صورة جدول الشعبة - حذف (باستخدام Local IDs)
    // ============================================

    [HttpDelete("schedule-images/section/{localGradeNumber:int}/{localSectionNumber:int}")]
    public async Task<IActionResult> DeleteSectionScheduleImage(int localGradeNumber, int localSectionNumber)
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
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.SectionId == section.Id && 
                                      s.Type == ScheduleImageType.Section);

        if (image is null)
            return NotFound(new { message = "لا توجد صورة جدول لهذه الشعبة" });

        DeleteScheduleImageFile(image.ImageUrl);
        db.ScheduleImages.Remove(image);
        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "تم حذف صورة جدول الشعبة بنجاح",
            data = new
            {
                LocalGradeNumber = grade.LocalGradeNumber,
                GradeName = grade.Name,
                LocalSectionNumber = section.LocalSectionNumber,
                SectionName = section.Name
            }
        });
    }

    // ============================================
    // 7. جلب جميع صور الجداول
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
    // 8. حذف صورة عامة (باستخدام ID)
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
                StudentLocalNumber = db.Students
                    .Where(s => s.Id == g.Key)
                    .Select(s => s.LocalStudentNumber)
                    .FirstOrDefault(),
                StudentName = db.Students
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
                StudentLocalNumber = s.LocalStudentNumber,
                s.Name,
                s.BloodType,
                s.ChronicDiseases,
                s.Allergies,
                s.HealthNotes,
                GuardianPhone = s.GuardianPhone,
                SectionName = s.Section != null ? s.Section.Name : null,
                LocalSectionNumber = s.Section != null ? s.Section.LocalSectionNumber : 0,
                GradeName = s.Section != null && s.Section.Grade != null ? s.Section.Grade.Name : null,
                LocalGradeNumber = s.Section != null && s.Section.Grade != null ? s.Section.Grade.LocalGradeNumber : 0
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
                LocalId = a.LocalAnnouncementId,
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
                LocalId = a.LocalActivityId,
                Title = a.Title,
                Description = a.Description,
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
            LocalEmployeeNumber = counselorSchool.LocalEmployeeNumber
        };

        var sections = await db.Sections
            .Include(s => s.Grade)
            .Where(s => s.CounselorId == counselor.Id)
            .Select(s => new
            {
                s.Id,
                s.Name,
                LocalSectionNumber = s.LocalSectionNumber,
                GradeName = s.Grade != null ? s.Grade.Name : null,
                LocalGradeNumber = s.Grade != null ? s.Grade.LocalGradeNumber : 0,
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
                StudentLocalNumber = w.Student != null ? w.Student.LocalStudentNumber : 0,
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
                StudentLocalNumber = s.Student != null ? s.Student.LocalStudentNumber : 0,
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
                StudentLocalNumber = a.Student != null ? a.Student.LocalStudentNumber : 0,
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

   [HttpPost("promote-students")]
public async Task<IActionResult> PromoteStudents([FromBody] PromoteRequest request)
{
    try
    {
        var currentYear = DateTime.Now.Year;
        const int semester = 2;
        var nextYear = currentYear + 1;

        // ✅ البحث عن الصف (بدون AcademicYear)
        var currentGrade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.LocalGradeNumber == request.LocalGradeNumber);

        if (currentGrade is null)
        {
            return BadRequest(new { 
                success = false, 
                message = $"لا يوجد صف برقم {request.LocalGradeNumber}" 
            });
        }

        // ✅ التحقق من معالجة الصف في هذه السنة
        var alreadyProcessed = await db.StudentGradeHistory
            .AnyAsync(h => h.GradeId == currentGrade.Id && 
                          h.AcademicYear == currentYear && 
                          h.Semester == semester);

        if (alreadyProcessed)
        {
            var processedInfo = await db.StudentGradeHistory
                .Where(h => h.GradeId == currentGrade.Id && 
                           h.AcademicYear == currentYear && 
                           h.Semester == semester)
                .GroupBy(h => h.IsPassed)
                .Select(g => new
                {
                    IsPassed = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var passedCount = processedInfo.FirstOrDefault(x => x.IsPassed)?.Count ?? 0;
            var failedCount = processedInfo.FirstOrDefault(x => !x.IsPassed)?.Count ?? 0;

            return BadRequest(new
            {
                success = false,
                message = $"تمت معالجة الصف {currentGrade.Name} للعام {currentYear} مسبقاً",
                data = new
                {
                    GradeName = currentGrade.Name,
                    AcademicYear = currentYear,
                    NextAcademicYear = nextYear,
                    CanProcess = false,
                    Hint = $"يمكنك معالجة الصف {currentGrade.Name} مرة أخرى في العام {nextYear} مع الطلاب الجدد",
                    Statistics = new
                    {
                        TotalStudents = passedCount + failedCount,
                        PassedCount = passedCount,
                        FailedCount = failedCount
                    }
                }
            });
        }

        // ✅ جلب الطلاب في الصف
        var students = await db.Students
            .Include(s => s.Section)
            .Where(s => s.SchoolId == SchoolId && 
                        s.Section != null &&
                        s.Section.GradeId == currentGrade.Id &&
                        s.IsActive)
            .ToListAsync();

        if (!students.Any())
        {
            return Ok(new
            {
                success = true,
                message = "لا يوجد طلاب في هذا الصف للترقية",
                data = new
                {
                    TotalStudents = 0,
                    PromotedCount = 0,
                    FailedCount = 0,
                    GraduatedCount = 0
                }
            });
        }

        // ✅ التحقق من وجود FinalExam لجميع الطلاب (ما عدا الصف 12)
        if (currentGrade.Level < 12)
        {
            var missingFinalExamStudents = new List<object>();

            foreach (var student in students)
            {
                var studentSubjects = await db.TeacherGrades
                    .Where(tg => tg.SectionId == student.SectionId)
                    .Select(tg => tg.SubjectId)
                    .Distinct()
                    .ToListAsync();

                var missingSubjects = new List<string>();

                foreach (var subjectId in studentSubjects)
                {
                    var hasFinalExam = await db.Marks
                        .AnyAsync(m => m.StudentId == student.Id && 
                                      m.SubjectId == subjectId && 
                                      m.Semester == semester &&
                                      m.FinalExam > 0);

                    if (!hasFinalExam)
                    {
                        var subjectName = await db.Subjects
                            .Where(s => s.Id == subjectId)
                            .Select(s => s.Name)
                            .FirstOrDefaultAsync() ?? "غير معروف";

                        missingSubjects.Add(subjectName);
                    }
                }

                if (missingSubjects.Any())
                {
                    missingFinalExamStudents.Add(new
                    {
                        student.Id,
                        student.Name,
                        student.LocalStudentNumber,
                        student.Email,
                        MissingSubjects = missingSubjects
                    });
                }
            }

            if (missingFinalExamStudents.Any())
            {
                return BadRequest(new
                {
                    success = false,
                    message = "لا يمكن الترقية لأن بعض الطلاب لم يتم إدخال علامات الامتحان النهائي (FinalExam) لهم",
                    data = new
                    {
                        TotalStudents = students.Count,
                        StudentsWithoutFinalExam = missingFinalExamStudents.Count,
                        MissingStudents = missingFinalExamStudents
                    }
                });
            }
        }

        // ✅ جلب الصف التالي (بدون AcademicYear)
        var nextLevel = currentGrade.Level + 1;
        var nextGrade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.Level == nextLevel);

        var passPercent = request.PassPercent;

        var promotedStudents = new List<Student>();
        var failedStudents = new List<Student>();
        var graduatedStudents = new List<Student>();
        var historyEntries = new List<StudentGradeHistory>();

        using var transaction = await db.Database.BeginTransactionAsync();

        try
        {
            foreach (var student in students)
            {
                var average = await GetStudentFinalAverageAsync(student.Id);
                var passed = average >= passPercent;

                historyEntries.Add(new StudentGradeHistory
                {
                    StudentId = student.Id,
                    GradeId = currentGrade.Id,
                    SectionId = student.SectionId ?? 0,
                    AcademicYear = currentYear,
                    Semester = semester,
                    IsPassed = passed,
                    Average = average,
                    CreatedAt = DateTime.UtcNow
                });

                if (passed)
                {
                    // ✅ معالجة خاصة للصف الثاني عشر (تخرج)
                    if (currentGrade.Level >= 12)
                    {
                        student.IsActive = false;
                        graduatedStudents.Add(student);
                    }
                    else if (nextGrade is not null)
                    {
                        var nextSection = await GetOrCreateSectionAsync(SchoolId, nextGrade.Id);
                        student.SectionId = nextSection.Id;
                        promotedStudents.Add(student);
                    }
                    else
                    {
                        // ✅ إذا لم يوجد صف تالي (حالة نادرة)
                        failedStudents.Add(student);
                    }
                }
                else
                {
                    failedStudents.Add(student);
                }
            }

            if (historyEntries.Any())
                db.StudentGradeHistory.AddRange(historyEntries);

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }

        // ✅ إرسال الإشعارات
        await SendPromotionNotificationsAsync(promotedStudents, failedStudents, graduatedStudents);

        return Ok(new
        {
            success = true,
            message = $"تمت معالجة ترقية الطلاب من {currentGrade.Name} للعام {currentYear} بنجاح",
            data = new
            {
                AcademicYear = currentYear,
                NextAcademicYear = nextYear,
                Semester = semester,
                PassPercent = passPercent,
                CurrentGrade = new
                {
                    currentGrade.Id,
                    currentGrade.Name,
                    currentGrade.Level,
                    currentGrade.LocalGradeNumber
                },
                NextGrade = nextGrade != null ? new
                {
                    nextGrade.Id,
                    nextGrade.Name,
                    nextGrade.Level,
                    nextGrade.LocalGradeNumber
                } : null,
                Statistics = new
                {
                    TotalStudents = students.Count,
                    PromotedCount = promotedStudents.Count,
                    FailedCount = failedStudents.Count,
                    GraduatedCount = graduatedStudents.Count
                },
                PromotedStudents = promotedStudents.Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.LocalStudentNumber,
                    s.Email
                }).ToList(),
                FailedStudents = failedStudents.Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.LocalStudentNumber,
                    s.Email
                }).ToList(),
                GraduatedStudents = graduatedStudents.Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.LocalStudentNumber,
                    s.Email
                }).ToList(),
                Note = $"يمكن معالجة {currentGrade.Name} مرة أخرى في العام {nextYear} مع الطلاب الجدد"
            }
        });
    }
    catch (Exception ex)
    {
        return BadRequest(new { success = false, message = ex.Message });
    }
}

// ============================================
// دوال مساعدة
// ============================================
private async Task<GradePromotionResult> ProcessGradePromotionAsync(
    Grade grade,
    List<Student> students,
    int currentYear,
    int nextYear,
    int semester,
    decimal passPercent)
{
    // ✅ جلب الصف التالي
    var nextLevel = grade.Level + 1;
    var nextGrade = await db.Grades
        .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                  g.Level == nextLevel );

    var promotedStudents = new List<Student>();
    var failedStudents = new List<Student>();
    var graduatedStudents = new List<Student>();
    var historyEntries = new List<StudentGradeHistory>();

    foreach (var student in students)
    {
        // ✅ حساب المعدل
        var average = await GetStudentFinalAverageAsync(student.Id);
        var passed = average >= passPercent;

        // ✅ تسجيل سجل الترقية
        historyEntries.Add(new StudentGradeHistory
        {
            StudentId = student.Id,
            GradeId = grade.Id,
            SectionId = student.SectionId ?? 0,
            AcademicYear = currentYear,
            Semester = semester,
            IsPassed = passed,
            Average = average,
            CreatedAt = DateTime.UtcNow
        });

        if (passed)
        {
            // ✅ معالجة خاصة للصف الثاني عشر
            if (grade.Level >= 12)
            {
                // ✅ طلاب الصف 12 الناجحين → يتخرجون
                student.IsActive = false;
                graduatedStudents.Add(student);
            }
            else if (nextGrade is not null)
            {
                // ✅ باقي الصفوف → ينتقلون للصف التالي
                var nextSection = await GetOrCreateSectionAsync(SchoolId, nextGrade.Id);
                student.SectionId = nextSection.Id;
                promotedStudents.Add(student);
            }
            else
            {
                // ✅ حالة استثنائية (لا يوجد صف تالي)
                failedStudents.Add(student);
            }
        }
        else
        {
            // ✅ الطالب راسب → يبقى في نفس الصف
            failedStudents.Add(student);
        }
    }

    // ✅ حفظ سجل الترقية
    if (historyEntries.Any())
        db.StudentGradeHistory.AddRange(historyEntries);

    return new GradePromotionResult
    {
        GradeName = grade.Name,
        Level = grade.Level,
        LocalGradeNumber = grade.LocalGradeNumber,
        TotalStudents = students.Count,
        PromotedCount = promotedStudents.Count,
        FailedCount = failedStudents.Count,
        GraduatedCount = graduatedStudents.Count,
        PromotedStudents = promotedStudents.Select(s => new StudentBasicInfo { Id = s.Id, Name = s.Name, LocalStudentNumber = s.LocalStudentNumber }).ToList(),
        FailedStudents = failedStudents.Select(s => new StudentBasicInfo { Id = s.Id, Name = s.Name, LocalStudentNumber = s.LocalStudentNumber }).ToList(),
        GraduatedStudents = graduatedStudents.Select(s => new StudentBasicInfo { Id = s.Id, Name = s.Name, LocalStudentNumber = s.LocalStudentNumber }).ToList(),
        Status = "Processed",
        Message = "تمت المعالجة بنجاح"
    };
}

// ============================================
// دوال مساعدة أخرى
// ============================================

private async Task<decimal> GetStudentFinalAverageAsync(int studentId)
{
    var marks = await db.Marks
        .Where(m => m.StudentId == studentId)
        .ToListAsync();

    return marks.Any() ? marks.Average(m => m.Total) : 0;
}

private async Task<Section> GetOrCreateSectionAsync(int schoolId, int gradeId)
{
    var section = await db.Sections
        .FirstOrDefaultAsync(s => s.GradeId == gradeId);

    if (section is null)
    {
        var usedNumbers = await db.Sections
            .Where(s => s.GradeId == gradeId)
            .Select(s => s.LocalSectionNumber)
            .ToListAsync();

        int newLocalNumber = 1;
        while (usedNumbers.Contains(newLocalNumber)) newLocalNumber++;

        section = new Section
        {
            Name = $"الشعبة {GetSectionLetter(newLocalNumber)}",
            GradeId = gradeId,
            SchoolId = schoolId,
            LocalSectionNumber = newLocalNumber,
            CreatedAt = DateTime.UtcNow
        };

        db.Sections.Add(section);
        await db.SaveChangesAsync();
    }

    return section;
}

private string GetSectionLetter(int number) => number switch
{
    1 => "أ",
    2 => "ب",
    3 => "ج",
    4 => "د",
    5 => "ه",
    6 => "و",
    7 => "ز",
    8 => "ح",
    9 => "ط",
    10 => "ي",
    _ => number.ToString()
};

private int GetCurrentAcademicYear()
{
    var now = DateTime.Now;
    var currentYear = now.Year;
    var currentMonth = now.Month;
    
    // ✅ إذا كنا في النصف الثاني من العام (سبتمبر - ديسمبر)
    // نعتبر أننا في السنة الدراسية الحالية
    if (currentMonth >= 9 && currentMonth <= 12)
    {
        return currentYear;  // مثلاً: سبتمبر 2026 → السنة الدراسية 2026
    }
    // ✅ إذا كنا في النصف الأول من العام (يناير - أغسطس)
    // نعتبر أننا في السنة الدراسية الماضية
    else
    {
        return currentYear - 1;  // مثلاً: يونيو 2026 → السنة الدراسية 2025
    }
}

// ✅ دالة لحساب السنة الدراسية التالية
private int GetNextAcademicYear(int currentAcademicYear)
{
    return currentAcademicYear + 1;
}

private async Task SendBulkPromotionNotificationsAsync(List<GradePromotionResult> results)
{
    var tasks = new List<Task>();

    foreach (var gradeResult in results)
    {
        // ✅ إشعارات للطلاب المترقين
        foreach (var student in gradeResult.PromotedStudents)
        {
            tasks.Add(notifier.SendAsync(student.Id, UserType.Student,
                "تهانينا! لقد تم ترقيتك",
                $"لقد نجحت وتم ترقيتك من {gradeResult.GradeName} إلى الصف التالي",
                "promotion"));
        }

        // ✅ إشعارات للطلاب الراسبين
        foreach (var student in gradeResult.FailedStudents)
        {
            tasks.Add(notifier.SendAsync(student.Id, UserType.Student,
                "للأسف، لم تنجح هذا العام",
                $"لم تنجح في {gradeResult.GradeName}. نتمنى لك التوفيق في العام القادم",
                "failure"));
        }

        // ✅ إشعارات للطلاب المتخرجين (الصف 12)
        foreach (var student in gradeResult.GraduatedStudents)
        {
            tasks.Add(notifier.SendAsync(student.Id, UserType.Student,
                "🎓 ألف مبروك! لقد تخرجت!",
                $"تهانينا على تخرجك من المدرسة للعام الدراسي {GetCurrentAcademicYear()}-{GetCurrentAcademicYear() + 1}",
                "graduation"));
        }
    }

    // ✅ تنفيذ جميع الإشعارات بشكل متسلسل (لتجنب مشاكل DbContext)
    foreach (var task in tasks)
    {
        await task;
    }
}


private async Task SendPromotionNotificationsAsync(
    List<Student> promoted,
    List<Student> failed,
    List<Student> graduated)
{
    foreach (var student in promoted)
    {
        await notifier.SendAsync(student.Id, UserType.Student,
            "تهانينا! لقد تم ترقيتك",
            "لقد نجحت وتم ترقيتك إلى الصف التالي",
            "promotion");
    }

    foreach (var student in failed)
    {
        await notifier.SendAsync(student.Id, UserType.Student,
            "للأسف، لم تنجح هذا العام",
            "نتمنى لك التوفيق في العام القادم",
            "failure");
    }

    foreach (var student in graduated)
    {
        await notifier.SendAsync(student.Id, UserType.Student,
            "🎓 ألف مبروك! لقد تخرجت!",
            "تهانينا على تخرجك من المدرسة",
            "graduation");
    }
}
// ============================================
// جلب الطلاب الذين لم تدخل علاماتهم النهائية
// ============================================

[HttpGet("students-missing-final-exam")]
public async Task<IActionResult> GetStudentsMissingFinalExam(
    [FromQuery] int localGradeNumber,
    [FromQuery] int semester = 2)
{
    try
    {
        var currentYear = DateTime.Now.Year;

        // ✅ البحث عن الصف
        var grade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.LocalGradeNumber == localGradeNumber 
                                     );

        if (grade is null)
        {
            return NotFound(new
            {
                success = false,
                message = $"لا يوجد صف برقم {localGradeNumber} للعام {currentYear}"
            });
        }

        // ✅ جلب جميع الطلاب في الصف
        var students = await db.Students
            .Include(s => s.Section)
            .Where(s => s.SchoolId == SchoolId && 
                        s.Section != null &&
                        s.Section.GradeId == grade.Id &&
                        s.IsActive)
            .ToListAsync();

        if (!students.Any())
        {
            return Ok(new
            {
                success = true,
                message = "لا يوجد طلاب في هذا الصف",
                data = new
                {
                    TotalStudents = 0,
                    StudentsWithMissingMarks = 0,
                    Students = new List<object>()
                }
            });
        }

        var result = new List<object>();

        foreach (var student in students)
        {
            // ✅ جلب المواد التي يدرسها الطالب
            var studentSubjects = await db.TeacherGrades
                .Where(tg => tg.SectionId == student.SectionId)
                .Select(tg => tg.SubjectId)
                .Distinct()
                .ToListAsync();

            var missingSubjects = new List<string>();

            foreach (var subjectId in studentSubjects)
            {
                // ✅ التحقق من وجود FinalExam
                var hasFinalExam = await db.Marks
                    .AnyAsync(m => m.StudentId == student.Id && 
                                  m.SubjectId == subjectId && 
                                  m.Semester == semester &&
                                  m.FinalExam > 0);

                if (!hasFinalExam)
                {
                    var subjectName = await db.Subjects
                        .Where(s => s.Id == subjectId)
                        .Select(s => s.Name)
                        .FirstOrDefaultAsync() ?? "غير معروف";

                    missingSubjects.Add(subjectName);
                }
            }

            if (missingSubjects.Any())
            {
                result.Add(new
                {
                    student.Id,
                    student.Name,
                    student.LocalStudentNumber,
                    student.Email,
                    SectionName = student.Section?.Name,
                    MissingSubjects = missingSubjects,
                    MissingCount = missingSubjects.Count
                });
            }
        }

        return Ok(new
        {
            success = true,
            message = result.Any() 
                ? $"يوجد {result.Count} طالب ناقص علاماتهم النهائية"
                : "جميع الطلاب لديهم علامات نهائية مكتملة",
            data = new
            {
                GradeName = grade.Name,
                GradeLevel = grade.Level,
                LocalGradeNumber = grade.LocalGradeNumber,
                TotalStudents = students.Count,
                StudentsWithMissingMarks = result.Count,
                Students = result
            }
        });
    }
    catch (Exception ex)
    {
        return BadRequest(new { success = false, message = ex.Message });
    }
}
// ============================================
// جلب الطلاب الراسبين في جميع الصفوف
// ============================================

[HttpGet("all-failed-students")]
public async Task<IActionResult> GetAllFailedStudents(
    [FromQuery] decimal passPercent,
    [FromQuery] int semester = 2)
{
    try
    {
        var currentYear = DateTime.Now.Year;

        // ✅ جلب جميع الصفوف في المدرسة
        var grades = await db.Grades
            .Where(g => g.SchoolId == SchoolId 
                        )
            .OrderBy(g => g.Level)
            .ToListAsync();

        var allResults = new List<object>();
        var totalStudents = 0;
        var totalFailed = 0;
        var totalPassed = 0;

        foreach (var grade in grades)
        {
            var students = await db.Students
                .Include(s => s.Section)
                .Where(s => s.SchoolId == SchoolId && 
                            s.Section != null &&
                            s.Section.GradeId == grade.Id &&
                            s.IsActive)
                .ToListAsync();

            if (!students.Any()) continue;

            var gradeFailed = new List<object>();
            var gradePassed = 0;

            foreach (var student in students)
            {
                var average = await GetStudentFinalAverageAsync(student.Id);
                var isPassed = average >= passPercent;

                if (!isPassed)
                {
                    gradeFailed.Add(new
                    {
                        student.Id,
                        student.Name,
                        student.LocalStudentNumber,
                        student.Email,
                        Average = Math.Round(average, 2)
                    });
                    totalFailed++;
                }
                else
                {
                    gradePassed++;
                }
                totalStudents++;
            }

            totalPassed += gradePassed;

            if (gradeFailed.Any())
            {
                allResults.Add(new
                {
                    Grade = new
                    {
                        grade.Id,
                        grade.Name,
                        grade.Level,
                        grade.LocalGradeNumber
                    },
                    TotalStudents = students.Count,
                    PassedCount = gradePassed,
                    FailedCount = gradeFailed.Count,
                    FailedStudents = gradeFailed
                });
            }
        }

        return Ok(new
        {
            success = true,
            message = "تم جلب جميع الطلاب الراسبين في المدرسة",
            data = new
            {
                PassPercent = passPercent,
                Semester = semester,
                AcademicYear = currentYear,
                Statistics = new
                {
                    TotalStudents = totalStudents,
                    TotalPassed = totalPassed,
                    TotalFailed = totalFailed,
                    OverallSuccessRate = totalStudents > 0 ? Math.Round((double)totalPassed / totalStudents * 100, 2) : 0,
                    OverallFailureRate = totalStudents > 0 ? Math.Round((double)totalFailed / totalStudents * 100, 2) : 0
                },
                FailedByGrade = allResults
            }
        });
    }
    catch (Exception ex)
    {
        return BadRequest(new { success = false, message = ex.Message });
    }
}

    // ============================================
    // دوال مساعدة لحفظ وحذف الصور
    // ============================================

    private async Task<string> SaveScheduleImageAsync(IFormFile image)
{
    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "schedules");
    
    if (!Directory.Exists(uploadsFolder))
        Directory.CreateDirectory(uploadsFolder);

    // ✅ تنظيف اسم الملف من المسافات والأحرف الخاصة
    var fileName = Path.GetFileNameWithoutExtension(image.FileName);
    var extension = Path.GetExtension(image.FileName);
    
    // استبدال المسافات بشرطة سفلية وإزالة الأحرف غير المسموحة
    var cleanFileName = fileName
        .Replace(" ", "_")
        .Replace("(", "")
        .Replace(")", "")
        .Replace("[", "")
        .Replace("]", "")
        .Replace("&", "")
        .Replace("%", "")
        .Replace("#", "");
    
    var uniqueFileName = $"{Guid.NewGuid()}_{cleanFileName}{extension}";
    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

    using (var fileStream = new FileStream(filePath, FileMode.Create))
    {
        await image.CopyToAsync(fileStream);
    }

    return $"/uploads/schedules/{uniqueFileName}";
}

    private bool DeleteScheduleImageFile(string imageUrl)
{
    if (string.IsNullOrEmpty(imageUrl))
        return false;

    try
    {
        var fileName = Path.GetFileName(imageUrl);
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "schedules", fileName);

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
            return true;
        }
        
        return false; // الملف غير موجود
    }
    catch (Exception ex)
    {
        // تسجيل الخطأ
        Console.WriteLine($"خطأ في حذف الملف: {ex.Message}");
        return false;
    }
}
private int CalculateAge(DateTime birthDate)
{
    var today = DateTime.Today;
    var age = today.Year - birthDate.Year;
    
    // إذا لم يحن عيد الميلاد بعد هذا العام، اطرح 1
    if (birthDate.Date > today.AddYears(-age))
        age--;
    
    return age;
}
}