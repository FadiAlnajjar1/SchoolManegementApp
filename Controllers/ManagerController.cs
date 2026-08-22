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

        if (request.Level < 1 || request.Level > 12)
            return BadRequest(new { message = "المستوى يجب أن يكون بين 1 و 12" });

        var existingGrade = await db.Grades
            .AnyAsync(g => g.Level == request.Level && g.SchoolId == SchoolId);

        if (existingGrade)
            return BadRequest(new { 
                message = $"الصف {GetGradeNameByLevel(request.Level)} موجود بالفعل" 
            });

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
                    .Where(s => s.SchoolId == SchoolId)
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
                            .Where(tg => tg.SectionId == s.Id &&
                                         tg.Teacher != null &&
                                         db.EmployeeSchools.Any(es => es.EmployeeId == tg.TeacherId && 
                                                                     es.SchoolId == SchoolId && 
                                                                     es.IsActive))
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
                                    .Where(sub => sub.Id == tg.SubjectId && sub.SchoolId == SchoolId)
                                    .Select(sub => sub.LocalSubjectId)
                                    .FirstOrDefault(),
                                SubjectName = tg.Subject != null ? tg.Subject.Name : null
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
                .Where(s => s.SchoolId == SchoolId)
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
                    Teachers = s.TeacherGrades
                        .Where(tg => tg.Teacher != null &&
                                     db.EmployeeSchools.Any(es => es.EmployeeId == tg.TeacherId && 
                                                                 es.SchoolId == SchoolId && 
                                                                 es.IsActive))
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
                                .Where(sub => sub.Id == tg.SubjectId && sub.SchoolId == SchoolId)
                                .Select(sub => sub.LocalSubjectId)
                                .FirstOrDefault(),
                            SubjectName = tg.Subject != null ? tg.Subject.Name : null,
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

        var studentIds = sections
            .SelectMany(s => s.Students ?? new List<Student>())
            .Select(s => s.Id)
            .ToList();

        if (studentIds.Any())
        {
            var marks = await db.Marks
                .Where(m => studentIds.Contains(m.StudentId))
                .ToListAsync();
            if (marks.Any())
                db.Marks.RemoveRange(marks);

            var activityRegistrations = await db.ActivityRegistrations
                .Where(r => studentIds.Contains(r.StudentId))
                .ToListAsync();
            if (activityRegistrations.Any())
                db.ActivityRegistrations.RemoveRange(activityRegistrations);
        }

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

        if (studentIds.Any())
        {
            var bookLoans = await db.BookLoans
                .Where(l => studentIds.Contains(l.StudentId))
                .ToListAsync();
            if (bookLoans.Any())
                db.BookLoans.RemoveRange(bookLoans);

            var bookReservations = await db.BookReservations
                .Where(r => studentIds.Contains(r.StudentId))
                .ToListAsync();
            if (bookReservations.Any())
                db.BookReservations.RemoveRange(bookReservations);

            var gradeHistory = await db.StudentGradeHistory
                .Where(h => studentIds.Contains(h.StudentId))
                .ToListAsync();
            if (gradeHistory.Any())
                db.StudentGradeHistory.RemoveRange(gradeHistory);

            var attendances = await db.StudentAttendances
                .Where(a => studentIds.Contains(a.StudentId))
                .ToListAsync();
            if (attendances.Any())
                db.StudentAttendances.RemoveRange(attendances);

            var warnings = await db.Warnings
                .Where(w => studentIds.Contains(w.StudentId))
                .ToListAsync();
            if (warnings.Any())
                db.Warnings.RemoveRange(warnings);

            var summons = await db.GuardianSummons
                .Where(s => studentIds.Contains(s.StudentId))
                .ToListAsync();
            if (summons.Any())
                db.GuardianSummons.RemoveRange(summons);

            var notifications = await db.Notifications
                .Where(n => studentIds.Contains(n.UserId) && n.UserType == UserType.Student)
                .ToListAsync();
            if (notifications.Any())
                db.Notifications.RemoveRange(notifications);

            var complaints = await db.Complaints
                .Where(c => studentIds.Contains(c.FromUserId) && c.FromUserType == UserType.Student)
                .ToListAsync();
            if (complaints.Any())
                db.Complaints.RemoveRange(complaints);
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
            deletedStudents = studentIds.Count,
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
        var currentYear = DateTime.Now.Year;
        
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
                .Where(m => m.StudentId == student.Id && 
                            subjectIds.Contains(m.SubjectId) &&
                            m.AcademicYear == currentYear);

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
                    Semester = semester,
                    AcademicYear = currentYear
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
                    Semester = semester,
                    AcademicYear = currentYear
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
        var currentYear = DateTime.Now.Year;
        
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
            .Where(m => m.StudentId == student.Id && 
                        subjectIds.Contains(m.SubjectId) &&
                        m.AcademicYear == currentYear)
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
                    TotalReports = performanceReports.Count,
                    AcademicYear = currentYear
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
            Teachers = section.TeacherGrades
                .Where(tg => tg.Teacher != null &&
                             db.EmployeeSchools.Any(es => es.EmployeeId == tg.TeacherId && 
                                                         es.SchoolId == SchoolId && 
                                                         es.IsActive))
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
                        .Where(sub => sub.Id == tg.SubjectId && sub.SchoolId == SchoolId)
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
                    .Where(t => t.SubjectId == s.Id && t.SchoolId == SchoolId)
                    .Select(t => new
                    {
                        t.TeacherId,
                        TeacherName = t.Teacher != null ? t.Teacher.Name : null,
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
                    .Where(tg => tg.SubjectId == s.Id &&
                                 tg.Section != null &&
                                 tg.Section.SchoolId == SchoolId)
                    .Select(tg => new
                    {
                        tg.SectionId,
                        SectionName = tg.Section != null ? tg.Section.Name : null,
                        LocalSectionNumber = tg.Section != null ? tg.Section.LocalSectionNumber : 0,
                        GradeName = tg.Section != null && tg.Section.Grade != null ? tg.Section.Grade.Name : null,
                        LocalGradeNumber = tg.Section != null && tg.Section.Grade != null ? tg.Section.Grade.LocalGradeNumber : 0,
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
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalSubjectId == localSubjectId);

        if (subject is null)
            return NotFound(new { message = $"لا توجد مادة برقم {localSubjectId} في هذه المدرسة" });

        var teachers = await db.TeacherSubjects
            .Where(ts => ts.SubjectId == subject.Id && ts.SchoolId == SchoolId)
            .Include(ts => ts.Teacher)
            .Select(ts => new
            {
                ts.TeacherId,
                TeacherName = ts.Teacher != null ? ts.Teacher.Name : null,
                LocalTeacherNumber = db.EmployeeSchools
                    .Where(es => es.EmployeeId == ts.TeacherId && 
                                 es.SchoolId == SchoolId && 
                                 es.IsActive)
                    .Select(es => (int?)es.LocalEmployeeNumber)
                    .FirstOrDefault(),
                ts.CreatedAt
            })
            .ToListAsync();

        var sections = await db.TeacherGrades
            .Where(tg => tg.SubjectId == subject.Id &&
                         tg.Section != null &&
                         tg.Section.SchoolId == SchoolId)
            .Include(tg => tg.Teacher)
            .Include(tg => tg.Section)
                .ThenInclude(s => s!.Grade)
            .Select(tg => new
            {
                tg.SectionId,
                SectionName = tg.Section != null ? tg.Section.Name : null,
                LocalSectionNumber = tg.Section != null ? tg.Section.LocalSectionNumber : 0,
                GradeName = tg.Section != null && tg.Section.Grade != null ? tg.Section.Grade.Name : null,
                LocalGradeNumber = tg.Section != null && tg.Section.Grade != null ? tg.Section.Grade.LocalGradeNumber : 0,
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
            .ToListAsync();

        return Ok(new
        {
            subject.Id,
            subject.Name,
            subject.LocalSubjectId,
            subject.SchoolId,
            Teachers = teachers,
            Sections = sections
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

        var grade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.LocalGradeNumber == request.LocalGradeNumber);

        if (grade is null)
            return BadRequest(new { message = $"لا يوجد صف برقم {request.LocalGradeNumber} في هذه المدرسة" });

        var section = await db.Sections
            .Include(s => s.Grade)
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalSectionNumber == request.LocalSectionNumber &&
                                      s.GradeId == grade.Id);

        if (section is null)
            return BadRequest(new { 
                message = $"لا توجد شعبة برقم {request.LocalSectionNumber} في الصف {request.LocalGradeNumber}" 
            });

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
            data = new
            {
                teacherLocalNumber = request.TeacherLocalNumber,
                teacherName = teacher?.Name,
                localSubjectId = request.LocalSubjectId,
                subjectName = subject.Name,
                localGradeNumber = request.LocalGradeNumber,
                gradeName = grade.Name,
                localSectionNumber = section.LocalSectionNumber,
                sectionName = section.Name,
                sectionId = section.Id,
                gradeId = grade.Id,
                createdAt = DateTime.UtcNow
            }
        });
    }

    // ============================================
    // فك ربط المعلم بالشعبة (باستخدام Local IDs)
    // ============================================

    [HttpDelete("unassign-teacher-from-section")]
    public async Task<IActionResult> UnassignTeacherFromSection(
        [FromQuery] int teacherLocalNumber,
        [FromQuery] int localGradeNumber,
        [FromQuery] int localSectionNumber,
        [FromQuery] int localSubjectId)
    {
        var teacherSchool = await db.EmployeeSchools
            .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                       es.LocalEmployeeNumber == teacherLocalNumber &&
                                       es.Role == EmployeeRole.Teacher &&
                                       es.IsActive);

        if (teacherSchool is null)
            return BadRequest(new { 
                success = false, 
                message = $"لا يوجد معلم برقم {teacherLocalNumber} في هذه المدرسة" 
            });

        var teacherId = teacherSchool.EmployeeId;

        var grade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.LocalGradeNumber == localGradeNumber);

        if (grade is null)
            return BadRequest(new { 
                success = false, 
                message = $"لا يوجد صف برقم {localGradeNumber} في هذه المدرسة" 
            });

        var section = await db.Sections
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalSectionNumber == localSectionNumber &&
                                      s.GradeId == grade.Id);

        if (section is null)
            return BadRequest(new { 
                success = false, 
                message = $"لا توجد شعبة برقم {localSectionNumber} في الصف {localGradeNumber}" 
            });

        var subject = await db.Subjects
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalSubjectId == localSubjectId);

        if (subject is null)
            return BadRequest(new { 
                success = false, 
                message = $"لا توجد مادة برقم {localSubjectId} في هذه المدرسة" 
            });

        var teacherGrade = await db.TeacherGrades
            .FirstOrDefaultAsync(tg => tg.TeacherId == teacherId &&
                                       tg.SubjectId == subject.Id &&
                                       tg.SectionId == section.Id);

        if (teacherGrade is null)
            return NotFound(new { 
                success = false, 
                message = "هذا المعلم غير مرتبط بهذه المادة في هذه الشعبة" 
            });

        db.TeacherGrades.Remove(teacherGrade);
        await db.SaveChangesAsync();

        var teacher = await db.Employees.FindAsync(teacherId);

        return Ok(new
        {
            success = true,
            message = "تم فك ربط المعلم بالشعبة بنجاح",
            data = new
            {
                teacherLocalNumber = teacherLocalNumber,
                teacherName = teacher?.Name,
                localGradeNumber = localGradeNumber,
                gradeName = grade.Name,
                localSectionNumber = localSectionNumber,
                sectionName = section.Name,
                localSubjectId = localSubjectId,
                subjectName = subject.Name,
                deletedAt = DateTime.UtcNow
            }
        });
    }

    // ============================================
    // جلب جميع روابط المعلم في صف معين (باستخدام Local IDs)
    // ============================================

    [HttpGet("teacher-sections/{localTeacherNumber:int}/grade/{localGradeNumber:int}")]
    public async Task<IActionResult> GetTeacherSectionsByGrade(
        int localTeacherNumber,
        int localGradeNumber)
    {
        var teacherSchool = await db.EmployeeSchools
            .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                       es.LocalEmployeeNumber == localTeacherNumber &&
                                       es.Role == EmployeeRole.Teacher &&
                                       es.IsActive);

        if (teacherSchool is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد معلم برقم {localTeacherNumber} في هذه المدرسة" 
            });

        var teacherId = teacherSchool.EmployeeId;
        var teacher = await db.Employees.FindAsync(teacherId);

        var grade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.LocalGradeNumber == localGradeNumber);

        if (grade is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد صف برقم {localGradeNumber} في هذه المدرسة" 
            });

        var sectionIds = await db.Sections
            .Where(s => s.GradeId == grade.Id && s.SchoolId == SchoolId)
            .Select(s => s.Id)
            .ToListAsync();

        if (!sectionIds.Any())
            return Ok(new
            {
                success = true,
                message = $"لا توجد شعب في الصف {grade.Name}",
                data = new
                {
                    teacherLocalNumber = localTeacherNumber,
                    teacherName = teacher?.Name,
                    localGradeNumber = localGradeNumber,
                    gradeName = grade.Name,
                    totalSections = 0,
                    sections = new List<object>()
                }
            });

        var teacherSections = await db.TeacherGrades
            .Where(tg => tg.TeacherId == teacherId &&
                         sectionIds.Contains(tg.SectionId))
            .Include(tg => tg.Section)
                .ThenInclude(s => s!.Grade)
            .Include(tg => tg.Subject)
            .Select(tg => new
            {
                tg.Id,
                sectionId = tg.SectionId,
                localSectionNumber = tg.Section != null ? tg.Section.LocalSectionNumber : 0,
                sectionName = tg.Section != null ? tg.Section.Name : null,
                subjectId = tg.SubjectId,
                localSubjectId = tg.Subject != null ? tg.Subject.LocalSubjectId : 0,
                subjectName = tg.Subject != null ? tg.Subject.Name : null,
                tg.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = $"تم جلب شعب المعلم في الصف {grade.Name} بنجاح",
            data = new
            {
                teacherLocalNumber = localTeacherNumber,
                teacherName = teacher?.Name,
                localGradeNumber = localGradeNumber,
                gradeName = grade.Name,
                totalSections = teacherSections.Count,
                sections = teacherSections
            }
        });
    }

    // ============================================
    // جلب جميع شعب الصف مع معلميها (باستخدام Local IDs)
    // ============================================

    [HttpGet("grade-sections-with-teachers/{localGradeNumber:int}")]
    public async Task<IActionResult> GetGradeSectionsWithTeachers(int localGradeNumber)
    {
        var grade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.LocalGradeNumber == localGradeNumber);

        if (grade is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد صف برقم {localGradeNumber} في هذه المدرسة" 
            });

        var sections = await db.Sections
            .Where(s => s.GradeId == grade.Id && s.SchoolId == SchoolId)
            .OrderBy(s => s.LocalSectionNumber)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.LocalSectionNumber,
                Teachers = db.TeacherGrades
                    .Where(tg => tg.SectionId == s.Id &&
                                 tg.Teacher != null &&
                                 db.EmployeeSchools.Any(es => es.EmployeeId == tg.TeacherId && 
                                                             es.SchoolId == SchoolId && 
                                                             es.IsActive))
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
                        LocalSubjectId = tg.Subject != null ? tg.Subject.LocalSubjectId : 0,
                        SubjectName = tg.Subject != null ? tg.Subject.Name : null,
                        tg.CreatedAt
                    })
                    .ToList()
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = $"تم جلب شعب الصف {grade.Name} مع معلميها بنجاح",
            data = new
            {
                localGradeNumber = grade.LocalGradeNumber,
                gradeName = grade.Name,
                totalSections = sections.Count,
                sections = sections
            }
        });
    }

    // ============================================
    // إدارة الموظفين - إنشاء، تحديث، حذف (باستخدام Local IDs)
    // ============================================

    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployee(EmployeeCreateLocalRequest request)
    {
        var school = await db.Schools.FindAsync(SchoolId);
        if (school is null)
            return BadRequest(new { success = false, message = "المدرسة غير موجودة" });

        Employee? existingEmployee = null;
        if (!string.IsNullOrEmpty(request.NationalId))
        {
            existingEmployee = await db.Employees
                .FirstOrDefaultAsync(e => e.NationalId == request.NationalId);
        }

        Employee employee;
        bool isNewEmployee = false;

        if (existingEmployee is not null)
        {
            employee = existingEmployee;

            if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != employee.Email)
            {
                var existingEmail = await db.Employees
                    .FirstOrDefaultAsync(e => e.Email == request.Email && e.Id != employee.Id);

                if (existingEmail is not null)
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "البريد الإلكتروني مستخدم بالفعل من قبل موظف آخر برقم وطني مختلف" 
                    });
                }

                employee.Email = request.Email;
            }

            employee.Name = request.Name;
            employee.Phone = request.Phone ?? employee.Phone;
            employee.Address = request.Address ?? employee.Address;
            employee.BirthDate = request.BirthDate;
            employee.Qualification = request.Qualification ?? employee.Qualification;
            
            if (!string.IsNullOrWhiteSpace(request.Password))
                employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var existingEmail = await db.Employees
                    .FirstOrDefaultAsync(e => e.Email == request.Email);

                if (existingEmail is not null)
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "البريد الإلكتروني مستخدم بالفعل من قبل موظف آخر" 
                    });
                }
            }
            else
            {
                return BadRequest(new { success = false, message = "البريد الإلكتروني مطلوب" });
            }

            employee = new Employee
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
            isNewEmployee = true;
            await db.SaveChangesAsync();
        }

        if (IsUniqueRole(request.Role))
        {
            var existingRole = await db.EmployeeSchools
                .AnyAsync(es => es.SchoolId == SchoolId &&
                               es.Role == request.Role &&
                               es.IsActive);

            if (existingRole)
                return BadRequest(new { success = false, message = $"الوظيفة '{GetRoleName(request.Role)}' مشغولة بالفعل في هذه المدرسة" });
        }

        var existingInSameSchool = await db.EmployeeSchools
            .AnyAsync(es => es.EmployeeId == employee.Id &&
                           es.SchoolId == SchoolId &&
                           es.IsActive);

        if (existingInSameSchool)
            return BadRequest(new { success = false, message = "هذا الموظف موجود بالفعل في هذه المدرسة" });

        var maxLocalNumber = await db.EmployeeSchools
            .Where(es => es.SchoolId == SchoolId)
            .Select(es => (int?)es.LocalEmployeeNumber)
            .MaxAsync() ?? 0;

        int newLocalNumber = maxLocalNumber + 1;

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

        if (request.Role == EmployeeRole.Teacher)
        {
            db.TeacherAssignments.Add(new TeacherAssignment
            {
                EmployeeId = employee.Id,
                SchoolId = SchoolId
            });
        }

        await db.SaveChangesAsync();

        await notifier.SendAsync(employee.Id, UserType.Employee,
            "مرحباً في المدرسة",
            $"تم تسجيلك في مدرسة '{school.Name}' برقم موظف {newLocalNumber}",
            "registration");

        return Created($"api/manager/employees/{newLocalNumber}", new
        {
            success = true,
            message = isNewEmployee ? "تم إنشاء الموظف وربطه بالمدرسة بنجاح" : "تم تحديث بيانات الموظف وربطه بالمدرسة بنجاح",
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

    if (!string.IsNullOrWhiteSpace(request.Name))
        employee.Name = request.Name;

    // ✅ تعديل التحقق من البريد الإلكتروني
    if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != employee.Email)
    {
        var existingEmployeeWithEmail = await db.Employees
            .FirstOrDefaultAsync(e => e.Email == request.Email && e.Id != employee.Id);

        if (existingEmployeeWithEmail is not null)
        {
            // ✅ يسمح إذا كان الرقم الوطني هو نفسه
            if (existingEmployeeWithEmail.NationalId != employee.NationalId)
            {
                return BadRequest(new { 
                    success = false, 
                    message = "البريد الإلكتروني مستخدم بالفعل من قبل موظف آخر برقم وطني مختلف" 
                });
            }
            // إذا كان الرقم الوطني هو نفسه، نسمح بتحديث البريد
        }

        employee.Email = request.Email;
    }

    if (!string.IsNullOrWhiteSpace(request.NationalId) && request.NationalId != employee.NationalId)
    {
        var existingNationalId = await db.Employees
            .FirstOrDefaultAsync(e => e.NationalId == request.NationalId && e.Id != employee.Id);

        if (existingNationalId is not null)
        {
            return BadRequest(new { 
                success = false, 
                message = "الرقم الوطني مستخدم بالفعل من قبل موظف آخر" 
            });
        }

        employee.NationalId = request.NationalId;
    }

    if (!string.IsNullOrWhiteSpace(request.Phone))
        employee.Phone = request.Phone;

    if (!string.IsNullOrWhiteSpace(request.Address))
        employee.Address = request.Address;

    if (request.BirthDate.HasValue)
    {
        var age = CalculateAge(request.BirthDate.Value);
        if (age < 18)
            return BadRequest(new { success = false, message = "عمر الموظف يجب أن يكون 18 سنة على الأقل" });
            
        employee.BirthDate = request.BirthDate;
    }

    if (!string.IsNullOrWhiteSpace(request.Qualification))
        employee.Qualification = request.Qualification;

    // ✅ كلمة المرور اختيارية (إذا تم إرسالها فقط)
    if (!string.IsNullOrWhiteSpace(request.Password))
        employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

    if (request.Role.HasValue && request.Role.Value != employeeSchool.Role)
    {
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

        if (employeeSchool.Role == EmployeeRole.Principal)
            return BadRequest(new { success = false, message = "لا يمكن حذف مدير المدرسة" });

        var activeInOtherSchools = await db.EmployeeSchools
            .AnyAsync(es => es.EmployeeId == employee.Id &&
                           es.SchoolId != SchoolId &&
                           es.IsActive);

        if (activeInOtherSchools)
        {
            employeeSchool.IsActive = false;

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

        var attendances = await db.EmployeeAttendances
            .Where(a => a.EmployeeId == employee.Id)
            .ToListAsync();
        if (attendances.Any())
            db.EmployeeAttendances.RemoveRange(attendances);

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

        var allEmployeeSchools = await db.EmployeeSchools
            .Where(es => es.EmployeeId == employee.Id)
            .ToListAsync();
        if (allEmployeeSchools.Any())
            db.EmployeeSchools.RemoveRange(allEmployeeSchools);

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
        var employeeSchool = await db.EmployeeSchools
            .Include(es => es.Employee)
            .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                      es.LocalEmployeeNumber == localEmployeeNumber &&
                                      es.IsActive);

        if (employeeSchool is null)
            return NotFound(new { success = false, message = $"لا يوجد موظف برقم {localEmployeeNumber} في هذه المدرسة" });

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

        if (employeeSchool.Role == EmployeeRole.Principal)
            return BadRequest(new { success = false, message = "لا يمكن فصل مدير المدرسة" });

        employeeSchool.IsActive = false;
        employee.IsDismissed = true;

        var assignments = await db.TeacherAssignments
            .Where(t => t.EmployeeId == employee.Id && t.SchoolId == SchoolId)
            .ToListAsync();

        if (assignments.Any())
            db.TeacherAssignments.RemoveRange(assignments);

        await db.SaveChangesAsync();

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

    // ============================================
    // جلب المعلمين
    // ============================================

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
                Subjects = db.TeacherGrades
                    .Where(tg => tg.TeacherId == es.EmployeeId &&
                                 tg.Section != null &&
                                 tg.Section.SchoolId == SchoolId)
                    .Select(tg => new
                    {
                        tg.SubjectId,
                        SubjectName = tg.Subject != null ? tg.Subject.Name : null,
                        LocalSubjectId = tg.Subject != null ? tg.Subject.LocalSubjectId : 0,
                        SectionId = tg.SectionId,
                        SectionName = tg.Section != null ? tg.Section.Name : null,
                        LocalSectionNumber = tg.Section != null ? tg.Section.LocalSectionNumber : 0,
                        GradeName = tg.Section != null && tg.Section.Grade != null ? tg.Section.Grade.Name : null,
                        LocalGradeNumber = tg.Section != null && tg.Section.Grade != null ? tg.Section.Grade.LocalGradeNumber : 0
                    })
                    .ToList(),
                Sections = db.TeacherGrades
                    .Where(tg => tg.TeacherId == es.EmployeeId &&
                                 tg.Section != null &&
                                 tg.Section.SchoolId == SchoolId)
                    .Select(tg => new
                    {
                        tg.SectionId,
                        SectionName = tg.Section != null ? tg.Section.Name : null,
                        tg.Section!.LocalSectionNumber,
                        GradeName = tg.Section != null && tg.Section.Grade != null ? tg.Section.Grade.Name : null,
                        LocalGradeNumber = tg.Section != null && tg.Section.Grade != null ? tg.Section.Grade.LocalGradeNumber : 0,
                        SubjectId = tg.SubjectId,
                        SubjectName = tg.Subject != null ? tg.Subject.Name : null,
                        LocalSubjectId = tg.Subject != null ? tg.Subject.LocalSubjectId : 0
                    })
                    .ToList()
            })
            .ToListAsync();

        return Ok(teachers);
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
                StudentsCount = db.Students
                    .Count(s => s.SchoolId == SchoolId &&
                               s.Section != null &&
                               s.Section.CounselorId == es.EmployeeId &&
                               s.IsActive),
                SectionsCount = db.Sections
                    .Count(s => s.SchoolId == SchoolId &&
                               s.CounselorId == es.EmployeeId),
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
                StudentsCount = db.Students.Count(st => st.SectionId == s.Id && st.IsActive && st.SchoolId == SchoolId)
            })
            .ToListAsync();

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

        var warnings = await db.Warnings
            .Include(w => w.Student)
            .Where(w => db.Students.Any(s => s.Id == w.StudentId && 
                                             s.SchoolId == SchoolId &&
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

        var summons = await db.GuardianSummons
            .Include(s => s.Student)
            .Where(s => db.Students.Any(st => st.Id == s.StudentId && 
                                              st.SchoolId == SchoolId &&
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

        var marks = await db.Marks
            .Where(m => m.StudentId == student.Id)
            .ToListAsync();
        if (marks.Any())
            db.Marks.RemoveRange(marks);

        var reportCards = await db.ReportCards
            .Where(r => r.StudentId == student.Id)
            .ToListAsync();
        if (reportCards.Any())
            db.ReportCards.RemoveRange(reportCards);

        var attendances = await db.StudentAttendances
            .Where(a => a.StudentId == student.Id)
            .ToListAsync();
        if (attendances.Any())
            db.StudentAttendances.RemoveRange(attendances);

        var warnings = await db.Warnings
            .Where(w => w.StudentId == student.Id)
            .ToListAsync();
        if (warnings.Any())
            db.Warnings.RemoveRange(warnings);

        var punishments = await db.Punishments
            .Where(p => p.StudentId == student.Id)
            .ToListAsync();
        if (punishments.Any())
            db.Punishments.RemoveRange(punishments);

        var activityRegistrations = await db.ActivityRegistrations
            .Where(r => r.StudentId == student.Id)
            .ToListAsync();
        if (activityRegistrations.Any())
            db.ActivityRegistrations.RemoveRange(activityRegistrations);

        var bookLoans = await db.BookLoans
            .Where(l => l.StudentId == student.Id)
            .ToListAsync();
        if (bookLoans.Any())
            db.BookLoans.RemoveRange(bookLoans);

        var bookReservations = await db.BookReservations
            .Where(r => r.StudentId == student.Id)
            .ToListAsync();
        if (bookReservations.Any())
            db.BookReservations.RemoveRange(bookReservations);

        var loanRequests = await db.BookLoanRequests
            .Where(r => r.StudentId == student.Id)
            .ToListAsync();
        if (loanRequests.Any())
            db.BookLoanRequests.RemoveRange(loanRequests);

        var gradeHistory = await db.StudentGradeHistory
            .Where(h => h.StudentId == student.Id)
            .ToListAsync();
        if (gradeHistory.Any())
            db.StudentGradeHistory.RemoveRange(gradeHistory);

        var complaints = await db.Complaints
            .Where(c => c.FromUserId == student.Id && c.FromUserType == UserType.Student)
            .ToListAsync();
        if (complaints.Any())
            db.Complaints.RemoveRange(complaints);

        var notifications = await db.Notifications
            .Where(n => n.UserId == student.Id && n.UserType == UserType.Student)
            .ToListAsync();
        if (notifications.Any())
            db.Notifications.RemoveRange(notifications);

        var summons = await db.GuardianSummons
            .Where(s => s.StudentId == student.Id)
            .ToListAsync();
        if (summons.Any())
            db.GuardianSummons.RemoveRange(summons);

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
                StudentsCount = db.Students.Count(st => st.SectionId == s.Id && st.IsActive && st.SchoolId == SchoolId),
                Teachers = db.TeacherGrades
                    .Where(tg => tg.SectionId == s.Id &&
                                 tg.Teacher != null &&
                                 db.EmployeeSchools.Any(es => es.EmployeeId == tg.TeacherId && 
                                                             es.SchoolId == SchoolId && 
                                                             es.IsActive))
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
                            .Where(sub => sub.Id == tg.SubjectId && sub.SchoolId == SchoolId)
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

    [HttpDelete("schedule-images/teacher/{localEmployeeNumber:int}")]
    public async Task<IActionResult> DeleteTeacherScheduleImage(int localEmployeeNumber)
    {
        var employeeSchool = await db.EmployeeSchools
            .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                       es.LocalEmployeeNumber == localEmployeeNumber &&
                                       es.IsActive);

        if (employeeSchool is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد موظف برقم {localEmployeeNumber} في هذه المدرسة" 
            });

        var employee = await db.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeSchool.EmployeeId);

        if (employee is null)
            return NotFound(new { 
                success = false, 
                message = "الموظف غير موجود" 
            });

        var image = await db.ScheduleImages
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.TeacherId == employee.Id && 
                                      s.Type == ScheduleImageType.Teacher);

        if (image is null)
            return NotFound(new { 
                success = false, 
                message = $"لا توجد صورة جدول للمعلم رقم {localEmployeeNumber}" 
            });

        var fileDeleted = DeleteScheduleImageFile(image.ImageUrl);

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
            .Where(s => s.SchoolId == SchoolId && s.CounselorId == counselor.Id)
            .Select(s => new
            {
                s.Id,
                s.Name,
                LocalSectionNumber = s.LocalSectionNumber,
                GradeName = s.Grade != null ? s.Grade.Name : null,
                LocalGradeNumber = s.Grade != null ? s.Grade.LocalGradeNumber : 0,
                StudentsCount = db.Students.Count(x => x.SectionId == s.Id && x.SchoolId == SchoolId)
            })
            .ToListAsync();

        var warnings = await db.Warnings
            .Include(w => w.Student)
            .Where(w => db.Students.Any(s => s.Id == w.StudentId && 
                                             s.SchoolId == SchoolId &&
                                             s.SectionId != null &&
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
            .Where(s => db.Students.Any(st => st.Id == s.StudentId && 
                                              st.SchoolId == SchoolId &&
                                              st.SectionId != null &&
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
            .Where(a => db.Students.Any(s => s.Id == a.StudentId && 
                                             s.SchoolId == SchoolId &&
                                             s.SectionId != null &&
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
            .CountAsync(s => s.SchoolId == SchoolId &&
                            s.SectionId != null &&
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
    // ترقية الطلاب
    // ============================================

    [HttpPost("promote-students")]
    public async Task<IActionResult> PromoteStudents([FromBody] PromoteRequest request)
    {
        try
        {
            const int semester = 2;
            var currentYear = DateTime.Now.Year;
            
            var grade = await db.Grades
                .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                          g.LocalGradeNumber == request.LocalGradeNumber);

            if (grade is null)
                return BadRequest(new { success = false, message = "الصف غير موجود" });

            var alreadyProcessed = await db.StudentGradeHistory
                .AnyAsync(h => h.GradeId == grade.Id && 
                              h.AcademicYear == currentYear && 
                              h.Semester == semester);

            if (alreadyProcessed)
            {
                var processedInfo = await db.StudentGradeHistory
                    .Where(h => h.GradeId == grade.Id && 
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
                    message = $"تمت معالجة الصف {grade.Name} للعام {currentYear} مسبقاً",
                    data = new
                    {
                        GradeName = grade.Name,
                        AcademicYear = currentYear,
                        CanProcess = false,
                        Hint = $"يمكنك معالجة الصف {grade.Name} مرة أخرى في العام {currentYear + 1} مع الطلاب الجدد",
                        Statistics = new
                        {
                            TotalStudents = passedCount + failedCount,
                            PassedCount = passedCount,
                            FailedCount = failedCount
                        }
                    }
                });
            }

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
                    message = "لا يوجد طلاب في هذا الصف للترقية"
                });
            }

            var eligibleStudents = new List<Student>();
            var alreadyPromotedStudents = new List<object>();

            foreach (var student in students)
            {
                var alreadyPromoted = await db.StudentGradeHistory
                    .AnyAsync(h => h.StudentId == student.Id &&
                                  h.AcademicYear == currentYear &&
                                  h.IsPassed);

                if (alreadyPromoted)
                {
                    alreadyPromotedStudents.Add(new
                    {
                        student.Id,
                        student.Name,
                        student.LocalStudentNumber,
                        Reason = "تم ترقيته مسبقاً في هذا العام"
                    });
                }
                else
                {
                    eligibleStudents.Add(student);
                }
            }

            if (!eligibleStudents.Any())
            {
                return Ok(new
                {
                    success = true,
                    message = $"جميع طلاب {grade.Name} تمت ترقيتهم مسبقاً للعام {currentYear}",
                    data = new
                    {
                        TotalStudents = students.Count,
                        AlreadyPromoted = alreadyPromotedStudents.Count,
                        EligibleStudents = 0,
                        AlreadyPromotedStudents = alreadyPromotedStudents
                    }
                });
            }

            if (grade.Level < 12)
            {
                var missingFinalExamStudents = new List<object>();

                foreach (var student in eligibleStudents)
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
                            MissingSubjects = missingSubjects
                        });
                    }
                }

                if (missingFinalExamStudents.Any())
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "بعض الطلاب لديهم علامات ناقصة (FinalExam)",
                        missingStudents = missingFinalExamStudents
                    });
                }
            }

            var allResults = new List<StudentFinalResult>();
            var incompleteStudents = new List<object>();

            foreach (var student in eligibleStudents)
            {
                var result = await CalculateStudentFinalResultAsync(
                    student.Id, 
                    semester, 
                    SchoolId,
                    currentYear);

                if (!result.IsComplete)
                {
                    incompleteStudents.Add(new
                    {
                        student.Id,
                        student.Name,
                        student.LocalStudentNumber,
                        MissingSubjects = result.MissingSubjects
                    });
                }

                allResults.Add(result);
            }

            if (incompleteStudents.Any())
            {
                return BadRequest(new
                {
                    success = false,
                    message = "بعض الطلاب لديهم علامات ناقصة",
                    incompleteStudents = incompleteStudents
                });
            }

            var nextLevel = grade.Level + 1;
            var nextGrade = await db.Grades
                .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                          g.Level == nextLevel);

            using var transaction = await db.Database.BeginTransactionAsync();

            try
            {
                var promotedStudents = new List<StudentFinalResult>();
                var failedStudents = new List<StudentFinalResult>();
                var graduatedStudents = new List<StudentFinalResult>();

                foreach (var result in allResults)
                {
                    var alreadyPromoted = await db.StudentGradeHistory
                        .AnyAsync(h => h.StudentId == result.StudentId &&
                                      h.AcademicYear == currentYear &&
                                      h.IsPassed);

                    if (alreadyPromoted)
                    {
                        continue;
                    }

                    var isPassed = result.OverallPercentage >= request.PassPercent;

                    var history = new StudentGradeHistory
                    {
                        StudentId = result.StudentId,
                        GradeId = grade.Id,
                        SectionId = eligibleStudents.First(s => s.Id == result.StudentId).SectionId ?? 0,
                        AcademicYear = currentYear,
                        Semester = semester,
                        IsPassed = isPassed,
                        Average = result.OverallPercentage,
                        CreatedAt = DateTime.UtcNow
                    };

                    db.StudentGradeHistory.Add(history);

                    if (isPassed)
                    {
                        if (grade.Level >= 12)
                        {
                            var student = eligibleStudents.First(s => s.Id == result.StudentId);
                            student.IsActive = false;
                            graduatedStudents.Add(result);
                        }
                        else if (nextGrade != null)
                        {
                            var student = eligibleStudents.First(s => s.Id == result.StudentId);
                            var nextSection = await GetOrCreateSectionAsync(SchoolId, nextGrade.Id);
                            student.SectionId = nextSection.Id;
                            promotedStudents.Add(result);
                        }
                        else
                        {
                            failedStudents.Add(result);
                        }
                    }
                    else
                    {
                        failedStudents.Add(result);
                    }
                }

                await db.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = $"تمت معالجة ترقية {grade.Name} للعام {currentYear} بنجاح",
                    data = new
                    {
                        AcademicYear = currentYear,
                        Semester = semester,
                        PassPercent = request.PassPercent,
                        CurrentGrade = new
                        {
                            grade.Id,
                            grade.Name,
                            grade.Level,
                            grade.LocalGradeNumber
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
                            TotalStudentsInGrade = students.Count,
                            AlreadyPromoted = alreadyPromotedStudents.Count,
                            EligibleStudents = eligibleStudents.Count,
                            PromotedCount = promotedStudents.Count,
                            FailedCount = failedStudents.Count,
                            GraduatedCount = graduatedStudents.Count,
                            SuccessRate = eligibleStudents.Any() ? 
                                Math.Round((decimal)promotedStudents.Count / eligibleStudents.Count * 100, 2) : 0
                        },
                        PromotedStudents = promotedStudents.Select(s => new
                        {
                            s.StudentId,
                            s.StudentName,
                            s.LocalStudentNumber,
                            s.OverallPercentage
                        }).ToList(),
                        FailedStudents = failedStudents.Select(s => new
                        {
                            s.StudentId,
                            s.StudentName,
                            s.LocalStudentNumber,
                            s.OverallPercentage
                        }).ToList(),
                        GraduatedStudents = graduatedStudents.Select(s => new
                        {
                            s.StudentId,
                            s.StudentName,
                            s.LocalStudentNumber,
                            s.OverallPercentage
                        }).ToList(),
                        AlreadyPromotedStudents = alreadyPromotedStudents,
                        Note = $"يمكن معالجة {grade.Name} مرة أخرى في العام {currentYear + 1} مع الطلاب الجدد"
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"حدث خطأ أثناء معالجة الترقية: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new { 
                success = false, 
                message = $"حدث خطأ: {ex.Message}" 
            });
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

            var grade = await db.Grades
                .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                          g.LocalGradeNumber == localGradeNumber);

            if (grade is null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"لا يوجد صف برقم {localGradeNumber} للعام {currentYear}"
                });
            }

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
// جلب جميع الموظفين في المدرسة
// ============================================

[HttpGet("employees")]
public async Task<IActionResult> GetEmployees()
{
    var employees = await db.EmployeeSchools
        .Include(es => es.Employee)
        .Where(es => es.SchoolId == SchoolId && es.IsActive)
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
            es.Role,
            RoleName = GetRoleName(es.Role),
            es.IsActive,
            // ✅ عدد الشعب التي يشرف عليها (إذا كان موجه)
            SectionsCount = es.Role == EmployeeRole.Counselor ? 
                db.Sections.Count(s => s.CounselorId == es.EmployeeId && s.SchoolId == SchoolId) : 0,
            // ✅ عدد المواد التي يدرسها (إذا كان معلم)
            SubjectsCount = es.Role == EmployeeRole.Teacher ? 
                db.TeacherGrades.Count(tg => tg.TeacherId == es.EmployeeId && 
                                            tg.Section != null && 
                                            tg.Section.SchoolId == SchoolId) : 0,
            // ✅ عدد الشعب التي يدرس فيها (إذا كان معلم)
            TeachingSectionsCount = es.Role == EmployeeRole.Teacher ? 
                db.TeacherGrades
                    .Where(tg => tg.TeacherId == es.EmployeeId && 
                                 tg.Section != null && 
                                 tg.Section.SchoolId == SchoolId)
                    .Select(tg => tg.SectionId)
                    .Distinct()
                    .Count() : 0
        })
        .ToListAsync();

    return Ok(new
    {
        success = true,
        message = "تم جلب الموظفين بنجاح",
        data = new
        {
            totalEmployees = employees.Count,
            employees = employees
        }
    });
}

// ============================================
// جلب موظف محدد باستخدام LocalEmployeeNumber
// ============================================

[HttpGet("employees/{localEmployeeNumber:int}")]
public async Task<IActionResult> GetEmployee(int localEmployeeNumber)
{
    var employeeSchool = await db.EmployeeSchools
        .Include(es => es.Employee)
        .FirstOrDefaultAsync(es => es.SchoolId == SchoolId &&
                                  es.LocalEmployeeNumber == localEmployeeNumber &&
                                  es.IsActive);

    if (employeeSchool is null)
        return NotFound(new { 
            success = false, 
            message = $"لا يوجد موظف برقم {localEmployeeNumber} في هذه المدرسة" 
        });

    var employee = employeeSchool.Employee;
    if (employee is null)
        return NotFound(new { success = false, message = "الموظف غير موجود" });

    // ✅ جلب معلومات إضافية حسب دور الموظف
    object? additionalInfo = null;

    if (employeeSchool.Role == EmployeeRole.Teacher)
    {
        var sections = await db.TeacherGrades
            .Where(tg => tg.TeacherId == employee.Id && 
                         tg.Section != null && 
                         tg.Section.SchoolId == SchoolId)
            .Select(tg => new
            {
                tg.SectionId,
                SectionName = tg.Section != null ? tg.Section.Name : null,
                LocalSectionNumber = tg.Section != null ? tg.Section.LocalSectionNumber : 0,
                GradeName = tg.Section != null && tg.Section.Grade != null ? tg.Section.Grade.Name : null,
                LocalGradeNumber = tg.Section != null && tg.Section.Grade != null ? tg.Section.Grade.LocalGradeNumber : 0,
                tg.SubjectId,
                SubjectName = tg.Subject != null ? tg.Subject.Name : null,
                LocalSubjectId = tg.Subject != null ? tg.Subject.LocalSubjectId : 0
            })
            .ToListAsync();

        additionalInfo = new
        {
            TeachingSections = sections,
            TotalSections = sections.Count,
            Subjects = sections.Select(s => new { s.SubjectId, s.SubjectName, s.LocalSubjectId }).Distinct().ToList(),
            TotalSubjects = sections.Select(s => s.SubjectId).Distinct().Count()
        };
    }
    else if (employeeSchool.Role == EmployeeRole.Counselor)
    {
        var sections = await db.Sections
            .Where(s => s.CounselorId == employee.Id && s.SchoolId == SchoolId)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.LocalSectionNumber,
                GradeName = s.Grade != null ? s.Grade.Name : null,
                LocalGradeNumber = s.Grade != null ? s.Grade.LocalGradeNumber : 0,
                StudentsCount = db.Students.Count(st => st.SectionId == s.Id && st.IsActive && st.SchoolId == SchoolId)
            })
            .ToListAsync();

        additionalInfo = new
        {
            SupervisedSections = sections,
            TotalSections = sections.Count,
            TotalStudents = sections.Sum(s => s.StudentsCount)
        };
    }

    return Ok(new
    {
        success = true,
        message = "تم جلب بيانات الموظف بنجاح",
        data = new
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
            LocalEmployeeNumber = employeeSchool.LocalEmployeeNumber,
            Role = employeeSchool.Role.ToString(),
            RoleName = GetRoleName(employeeSchool.Role),
            IsActive = employeeSchool.IsActive,
            AdditionalInfo = additionalInfo
        }
    });
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

            var grades = await db.Grades
                .Where(g => g.SchoolId == SchoolId)
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
                    var result = await CalculateStudentFinalResultAsync(
                        student.Id, 
                        semester, 
                        SchoolId,
                        currentYear);
                    
                    var average = result.OverallPercentage;
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

        var fileName = Path.GetFileNameWithoutExtension(image.FileName);
        var extension = Path.GetExtension(image.FileName);
        
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
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"خطأ في حذف الملف: {ex.Message}");
            return false;
        }
    }

    private async Task<StudentFinalResult> CalculateStudentFinalResultAsync(
        int studentId,
        int semester,
        int schoolId,
        int academicYear)
    {
        var student = await db.Students
            .Include(s => s.Section)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        if (student == null)
        {
            return new StudentFinalResult
            {
                StudentId = studentId,
                StudentName = "طالب غير موجود",
                LocalStudentNumber = 0,
                IsComplete = false,
                MissingSubjects = new List<string> { "الطالب غير موجود" }
            };
        }

        if (student.SectionId == null)
        {
            return new StudentFinalResult
            {
                StudentId = studentId,
                StudentName = student.Name,
                LocalStudentNumber = student.LocalStudentNumber,
                IsComplete = false,
                MissingSubjects = new List<string> { "الطالب ليس في أي شعبة" }
            };
        }

        var subjectIds = await db.TeacherGrades
            .Where(tg => tg.SectionId == student.SectionId)
            .Select(tg => tg.SubjectId)
            .Distinct()
            .ToListAsync();

        if (!subjectIds.Any())
        {
            return new StudentFinalResult
            {
                StudentId = studentId,
                StudentName = student.Name,
                LocalStudentNumber = student.LocalStudentNumber,
                IsComplete = false,
                MissingSubjects = new List<string> { "لا توجد مواد في هذه الشعبة" }
            };
        }

        var marks = await db.Marks
            .Where(m => m.StudentId == studentId &&
                        m.Semester == semester &&
                        m.AcademicYear == academicYear &&
                        subjectIds.Contains(m.SubjectId))
            .ToDictionaryAsync(m => m.SubjectId);

        decimal totalStudentScore = 0;
        decimal totalMaxScore = 0;
        var subjectResults = new List<SubjectResult>();
        var missingSubjects = new List<string>();

        foreach (var subjectId in subjectIds)
        {
            var subject = await db.Subjects.FindAsync(subjectId);
            var subjectName = subject?.Name ?? "غير معروف";

            if (marks.TryGetValue(subjectId, out var mark))
            {
                var studentTotal = mark.Oral + mark.Quiz1 + mark.Quiz2 + 
                                   mark.Homework + mark.FinalExam;

                var maxTotal = mark.MaxOral + mark.MaxQuiz1 + mark.MaxQuiz2 + 
                               mark.MaxHomework + mark.MaxFinalExam;

                var hasFinalExam = mark.FinalExam > 0;

                if (!hasFinalExam)
                {
                    missingSubjects.Add($"{subjectName} (العلامة النهائية غير مدخلة)");
                    continue;
                }

                if (maxTotal == 0)
                {
                    missingSubjects.Add($"{subjectName} (العلامات الكاملة غير محددة)");
                    continue;
                }

                totalStudentScore += studentTotal;
                totalMaxScore += maxTotal;

                subjectResults.Add(new SubjectResult
                {
                    SubjectId = subjectId,
                    SubjectName = subjectName,
                    StudentScore = studentTotal,
                    MaxScore = maxTotal,
                    Percentage = Math.Round((decimal)studentTotal / maxTotal * 100, 2)
                });
            }
            else
            {
                missingSubjects.Add($"{subjectName} (لا توجد علامات)");
            }
        }

        var isComplete = missingSubjects.Count == 0 && subjectResults.Count == subjectIds.Count;

        decimal overallPercentage = 0;
        if (totalMaxScore > 0)
        {
            overallPercentage = Math.Round((totalStudentScore / totalMaxScore) * 100, 2);
        }

        return new StudentFinalResult
        {
            StudentId = studentId,
            StudentName = student.Name,
            LocalStudentNumber = student.LocalStudentNumber,
            IsComplete = isComplete,
            TotalStudentScore = totalStudentScore,
            TotalMaxScore = totalMaxScore,
            OverallPercentage = overallPercentage,
            SubjectResults = subjectResults,
            MissingSubjects = missingSubjects,
            SubjectCount = subjectIds.Count,
            CompletedSubjects = subjectResults.Count,
            AcademicYear = academicYear
        };
    }

    // ============================================
    // كلاسات مساعدة
    // ============================================

    public class StudentFinalResult
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public int LocalStudentNumber { get; set; }
        public bool IsComplete { get; set; }
        public decimal TotalStudentScore { get; set; }
        public decimal TotalMaxScore { get; set; }
        public decimal OverallPercentage { get; set; }
        public List<SubjectResult> SubjectResults { get; set; } = new();
        public List<string> MissingSubjects { get; set; } = new();
        public int SubjectCount { get; set; }
        public int CompletedSubjects { get; set; }
        public int AcademicYear { get; set; }
    }

    public class SubjectResult
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public decimal StudentScore { get; set; }
        public decimal MaxScore { get; set; }
        public decimal Percentage { get; set; }
    }

    private async Task<GradePromotionResult> ProcessGradePromotionAsync(
        Grade grade,
        List<Student> students,
        int currentYear,
        int nextYear,
        int semester,
        decimal passPercent)
    {
        var nextLevel = grade.Level + 1;
        var nextGrade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.Level == nextLevel);

        var promotedStudents = new List<Student>();
        var failedStudents = new List<Student>();
        var graduatedStudents = new List<Student>();
        var historyEntries = new List<StudentGradeHistory>();

        foreach (var student in students)
        {
            var average = await GetStudentFinalAverageAsync(student.Id);
            var passed = average >= passPercent;

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
                if (grade.Level >= 12)
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
        
        if (currentMonth >= 9 && currentMonth <= 12)
        {
            return currentYear;
        }
        else
        {
            return currentYear - 1;
        }
    }

    private int GetNextAcademicYear(int currentAcademicYear)
    {
        return currentAcademicYear + 1;
    }

    private async Task SendBulkPromotionNotificationsAsync(List<GradePromotionResult> results)
    {
        var tasks = new List<Task>();

        foreach (var gradeResult in results)
        {
            foreach (var student in gradeResult.PromotedStudents)
            {
                tasks.Add(notifier.SendAsync(student.Id, UserType.Student,
                    "تهانينا! لقد تم ترقيتك",
                    $"لقد نجحت وتم ترقيتك من {gradeResult.GradeName} إلى الصف التالي",
                    "promotion"));
            }

            foreach (var student in gradeResult.FailedStudents)
            {
                tasks.Add(notifier.SendAsync(student.Id, UserType.Student,
                    "للأسف، لم تنجح هذا العام",
                    $"لم تنجح في {gradeResult.GradeName}. نتمنى لك التوفيق في العام القادم",
                    "failure"));
            }

            foreach (var student in gradeResult.GraduatedStudents)
            {
                tasks.Add(notifier.SendAsync(student.Id, UserType.Student,
                    "🎓 ألف مبروك! لقد تخرجت!",
                    $"تهانينا على تخرجك من المدرسة للعام الدراسي {GetCurrentAcademicYear()}-{GetCurrentAcademicYear() + 1}",
                    "graduation"));
            }
        }

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

    private int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;
        
        if (birthDate.Date > today.AddYears(-age))
            age--;
        
        return age;
    }
}