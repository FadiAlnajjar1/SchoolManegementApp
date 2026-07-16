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
[Route("api/teacher")]
[Authorize(Roles = Roles.Teacher)]
public class TeacherController(
    AppDbContext db,
    SchoolRulesService rules,
    NotificationService notifier) : ControllerBase
{
    private int TeacherId => User.GetUserId();
    private int SchoolId => User.GetSchoolId();

    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects()
    {
        var subjects = await db.TeacherGrades
            .Where(t => t.TeacherId == TeacherId)
            .Include(t => t.Subject)
            .Include(t => t.Section)
                .ThenInclude(s => s!.Grade)
            .Select(t => new
            {
                t.SubjectId,
                LocalSubjectId = t.Subject != null ? t.Subject.LocalSubjectId : 0,
                SubjectName = t.Subject != null ? t.Subject.Name : null,
                LocalGradeNumber = t.Section != null && t.Section.Grade != null ? 
                    t.Section.Grade.LocalGradeNumber : 0,
                GradeName = t.Section != null && t.Section.Grade != null ? 
                    t.Section.Grade.Name : null,
                t.SectionId,
                SectionName = t.Section != null ? t.Section.Name : null,
                LocalSectionNumber = t.Section != null ? t.Section.LocalSectionNumber : 0,
                t.CreatedAt
            })
            .ToListAsync();

        var result = subjects
            .GroupBy(s => new { s.SubjectId, s.LocalSubjectId, s.SubjectName })
            .Select(g => new
            {
                LocalSubjectId = g.Key.LocalSubjectId,
                SubjectName = g.Key.SubjectName,
                Grades = g
                    .GroupBy(gr => new { gr.LocalGradeNumber, gr.GradeName })
                    .Select(grade => new
                    {
                        LocalGradeNumber = grade.Key.LocalGradeNumber,
                        GradeName = grade.Key.GradeName,
                        Sections = grade.Select(s => new
                        {
                            s.SectionId,
                            s.SectionName,
                            s.LocalSectionNumber,
                            s.CreatedAt
                        })
                        .OrderBy(s => s.LocalSectionNumber)
                        .ToList()
                    })
                    .OrderBy(g => g.LocalGradeNumber)
                    .ToList()
            })
            .OrderBy(s => s.LocalSubjectId)
            .ToList();

        return Ok(new
        {
            success = true,
            message = "تم جلب المواد بنجاح",
            data = result
        });
    }

    [HttpGet("sections/{localGradeNumber:int}/{localSectionNumber:int}/students")]
    public async Task<IActionResult> GetSectionStudents(
        int localGradeNumber,
        int localSectionNumber,
        [FromQuery] int? localSubjectId = null)
    {
        var grade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                      g.LocalGradeNumber == localGradeNumber);

        if (grade is null)
            return NotFound(new { success = false, message = $"لا يوجد صف برقم {localGradeNumber} في هذه المدرسة" });

        var section = await db.Sections
            .FirstOrDefaultAsync(s => s.GradeId == grade.Id && 
                                      s.LocalSectionNumber == localSectionNumber &&
                                      s.SchoolId == SchoolId);

        if (section is null)
            return NotFound(new { success = false, message = $"لا توجد شعبة برقم {localSectionNumber} في الصف {localGradeNumber}" });

        var teachesSection = await db.TeacherGrades
            .AnyAsync(tg => tg.TeacherId == TeacherId && tg.SectionId == section.Id);

        if (!teachesSection)
            return BadRequest(new { success = false, message = "أنت لا تدرس في هذه الشعبة" });

        Subject? subject = null;
        if (localSubjectId.HasValue)
        {
            subject = await db.Subjects
                .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                          s.LocalSubjectId == localSubjectId.Value);

            if (subject is null)
                return NotFound(new { success = false, message = $"لا توجد مادة برقم {localSubjectId}" });

            var teachesSubject = await db.TeacherGrades
                .AnyAsync(tg => tg.TeacherId == TeacherId && 
                               tg.SectionId == section.Id && 
                               tg.SubjectId == subject.Id);

            if (!teachesSubject)
                return BadRequest(new { success = false, message = "أنت لا تدرس هذه المادة في هذه الشعبة" });
        }

        var teacherSubjects = await db.TeacherGrades
            .Where(tg => tg.TeacherId == TeacherId && tg.SectionId == section.Id)
            .Select(tg => new
            {
                tg.SubjectId,
                LocalSubjectId = tg.Subject != null ? tg.Subject.LocalSubjectId : 0,
                SubjectName = tg.Subject != null ? tg.Subject.Name : null
            })
            .Distinct()
            .ToListAsync();

        var students = await db.Students
            .Where(s => s.SectionId == section.Id && s.IsActive)
            .OrderBy(s => s.LocalStudentNumber)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Email,
                LocalStudentNumber = s.LocalStudentNumber,
                s.BloodType,
                s.GuardianName,
                s.GuardianPhone,
                s.BirthDate,
                s.Address,
                s.DismissalWarning,
                s.CreatedAt,
                Marks = localSubjectId.HasValue ?
                    db.Marks
                        .Where(m => m.StudentId == s.Id && 
                                   m.SubjectId == subject!.Id)
                        .Select(m => new
                        {
                            m.SubjectId,
                            LocalSubjectId = m.Subject != null ? m.Subject.LocalSubjectId : 0,
                            SubjectName = m.Subject != null ? m.Subject.Name : null,
                            m.Semester,
                            m.Oral,
                            m.Quiz1,
                            m.Quiz2,
                            m.Homework,
                            m.FinalExam,
                            m.Total,
                            m.UpdatedAt
                        })
                        .ToList() :
                    db.Marks
                        .Where(m => m.StudentId == s.Id && 
                                   db.TeacherSubjects.Any(t => t.TeacherId == TeacherId && t.SubjectId == m.SubjectId))
                        .Select(m => new
                        {
                            m.SubjectId,
                            LocalSubjectId = m.Subject != null ? m.Subject.LocalSubjectId : 0,
                            SubjectName = m.Subject != null ? m.Subject.Name : null,
                            m.Semester,
                            m.Oral,
                            m.Quiz1,
                            m.Quiz2,
                            m.Homework,
                            m.FinalExam,
                            m.Total,
                            m.UpdatedAt
                        })
                        .ToList()
            })
            .ToListAsync();

        var studentsWithMarks = students.Select(s => new
        {
            s.Id,
            s.Name,
            s.Email,
            s.LocalStudentNumber,
            s.BloodType,
            s.GuardianName,
            s.GuardianPhone,
            s.BirthDate,
            s.Address,
            s.DismissalWarning,
            s.CreatedAt,
            Semester1Marks = s.Marks
                .Where(m => m.Semester == 1)
                .GroupBy(m => new { m.SubjectId, m.LocalSubjectId, m.SubjectName })
                .Select(g => new
                {
                    LocalSubjectId = g.Key.LocalSubjectId,
                    SubjectName = g.Key.SubjectName,
                    Oral = g.FirstOrDefault()?.Oral ?? 0,
                    Quiz1 = g.FirstOrDefault()?.Quiz1 ?? 0,
                    Quiz2 = g.FirstOrDefault()?.Quiz2 ?? 0,
                    Homework = g.FirstOrDefault()?.Homework ?? 0,
                    FinalExam = g.FirstOrDefault()?.FinalExam ?? 0,
                    Total = g.FirstOrDefault()?.Total ?? 0,
                    UpdatedAt = g.FirstOrDefault()?.UpdatedAt
                })
                .ToList(),
            Semester2Marks = s.Marks
                .Where(m => m.Semester == 2)
                .GroupBy(m => new { m.SubjectId, m.LocalSubjectId, m.SubjectName })
                .Select(g => new
                {
                    LocalSubjectId = g.Key.LocalSubjectId,
                    SubjectName = g.Key.SubjectName,
                    Oral = g.FirstOrDefault()?.Oral ?? 0,
                    Quiz1 = g.FirstOrDefault()?.Quiz1 ?? 0,
                    Quiz2 = g.FirstOrDefault()?.Quiz2 ?? 0,
                    Homework = g.FirstOrDefault()?.Homework ?? 0,
                    FinalExam = g.FirstOrDefault()?.FinalExam ?? 0,
                    Total = g.FirstOrDefault()?.Total ?? 0,
                    UpdatedAt = g.FirstOrDefault()?.UpdatedAt
                })
                .ToList(),
            Semester1Average = s.Marks.Where(m => m.Semester == 1).Any() 
                ? Math.Round(s.Marks.Where(m => m.Semester == 1).Average(m => m.Total), 2) 
                : 0,
            Semester2Average = s.Marks.Where(m => m.Semester == 2).Any() 
                ? Math.Round(s.Marks.Where(m => m.Semester == 2).Average(m => m.Total), 2) 
                : 0,
            FinalAverage = s.Marks.Any() 
                ? Math.Round(s.Marks.Average(m => m.Total), 2) 
                : 0
        }).ToList();

        var totalStudents = studentsWithMarks.Count;
        var studentsWithWarnings = studentsWithMarks.Count(s => s.DismissalWarning);
        var overallAverage = studentsWithMarks.Any() 
            ? Math.Round(studentsWithMarks.Average(s => s.FinalAverage), 2) 
            : 0;

        return Ok(new
        {
            success = true,
            message = "تم جلب طلاب الشعبة مع العلامات بنجاح",
            data = new
            {
                Section = new
                {
                    section.Id,
                    section.Name,
                    LocalSectionNumber = section.LocalSectionNumber,
                    GradeName = grade.Name,
                    LocalGradeNumber = grade.LocalGradeNumber
                },
                Subject = localSubjectId.HasValue ? new
                {
                    subject!.Id,
                    subject.LocalSubjectId,
                    subject.Name
                } : null,
                TeacherSubjects = teacherSubjects,
                Statistics = new
                {
                    TotalStudents = totalStudents,
                    StudentsWithWarnings = studentsWithWarnings,
                    ActiveStudents = totalStudents - studentsWithWarnings,
                    OverallAverage = overallAverage
                },
                Students = studentsWithMarks
            }
        });
    }

    [HttpPost("marks/quiz")]
    public async Task<IActionResult> AddQuizMark(QuizMarkRequest request)
    {
        var blocked = await rules.ValidateSecondPeriodAttendanceTakenAsync(TeacherId);
        if (blocked is not null) 
            return StatusCode(403, new { message = blocked });

        var subject = await db.Subjects
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalSubjectId == request.LocalSubjectId);

        if (subject is null)
            return BadRequest(new { success = false, message = $"لا توجد مادة برقم {request.LocalSubjectId}" });

        var teacherSubject = await db.TeacherSubjects
            .FirstOrDefaultAsync(t => t.TeacherId == TeacherId && t.SubjectId == subject.Id);
        
        if (teacherSubject is null) 
            return BadRequest(new { success = false, message = "هذه المادة ليست من موادك" });

        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalStudentNumber == request.LocalStudentNumber);
        
        if (student is null) 
            return BadRequest(new { success = false, message = $"لا يوجد طالب برقم {request.LocalStudentNumber}" });

        if (!Enum.IsDefined(typeof(QuizType), request.QuizTypeId))
            return BadRequest(new { success = false, message = "نوع المذاكرة غير صحيح" });

        var existingMark = await db.Marks
            .FirstOrDefaultAsync(m => m.StudentId == student.Id && 
                                      m.SubjectId == subject.Id && 
                                      m.Semester == request.Semester);

        if (existingMark is null)
        {
            existingMark = new Mark
            {
                StudentId = student.Id,
                SubjectId = subject.Id,
                Semester = request.Semester,
                EnteredById = TeacherId,
                
            };
            db.Marks.Add(existingMark);
        }

        switch (request.QuizTypeId)
        {
            case 1:
                existingMark.Quiz1 = request.Score;
                break;
            case 2:
                existingMark.Quiz2 = request.Score;
                break;
            case 3:
                existingMark.Homework = request.Score;
                break;
            case 4:
                existingMark.Oral = request.Score;
                break;
            case 5:
                existingMark.FinalExam = request.Score;
                break;
            default:
                return BadRequest(new { success = false, message = "نوع المذاكرة غير صحيح" });
        }

        existingMark.Total = existingMark.Oral + existingMark.Quiz1 + existingMark.Quiz2 + 
                             existingMark.Homework + existingMark.FinalExam;
        existingMark.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var quizTypeName = ((QuizType)request.QuizTypeId).ToString();

        return Ok(new
        {
            success = true,
            message = "تم تسجيل علامة المذاكرة بنجاح",
            data = new
            {
                existingMark.Id,
                StudentLocalNumber = student.LocalStudentNumber,
                StudentName = student.Name,
                LocalSubjectId = subject.LocalSubjectId,
                SubjectName = subject.Name,
                Semester = existingMark.Semester,
                QuizTypeId = request.QuizTypeId,
                QuizTypeName = quizTypeName,
                Score = request.Score,
                existingMark.Oral,
                existingMark.Quiz1,
                existingMark.Quiz2,
                existingMark.Homework,
                existingMark.FinalExam,
                Total = existingMark.Total,
                UpdatedAt = existingMark.UpdatedAt
            }
        });
    }

    [HttpPut("marks/quiz")]
    public async Task<IActionResult> UpdateQuizMark(QuizMarkUpdateLocalRequest request)
    {
        var blocked = await rules.ValidateSecondPeriodAttendanceTakenAsync(TeacherId);
        if (blocked is not null) 
            return StatusCode(403, new { message = blocked });

        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalStudentNumber == request.LocalStudentNumber);
        
        if (student is null)
            return NotFound(new { success = false, message = $"لا يوجد طالب برقم {request.LocalStudentNumber}" });

        var subject = await db.Subjects
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalSubjectId == request.LocalSubjectId);

        if (subject is null)
            return NotFound(new { success = false, message = $"لا توجد مادة برقم {request.LocalSubjectId}" });

        var teacherSubject = await db.TeacherSubjects
            .AnyAsync(t => t.TeacherId == TeacherId && t.SubjectId == subject.Id);

        if (!teacherSubject)
            return BadRequest(new { success = false, message = "هذه المادة ليست من موادك" });

        if (!Enum.IsDefined(typeof(QuizType), request.QuizTypeId))
            return BadRequest(new { success = false, message = "نوع المذاكرة غير صحيح" });

        var mark = await db.Marks
            .FirstOrDefaultAsync(m => m.StudentId == student.Id && 
                                      m.SubjectId == subject.Id && 
                                      m.Semester == request.Semester);

        if (mark is null)
            return NotFound(new { 
                success = false, 
                message = $"لا توجد علامة للطالب {request.LocalStudentNumber} في مادة {request.LocalSubjectId} للفصل {request.Semester}" 
            });

        switch (request.QuizTypeId)
        {
            case 1:
                if (request.Score.HasValue) mark.Quiz1 = request.Score.Value;
                break;
            case 2:
                if (request.Score.HasValue) mark.Quiz2 = request.Score.Value;
                break;
            case 3:
                if (request.Score.HasValue) mark.Homework = request.Score.Value;
                break;
            case 4:
                if (request.Score.HasValue) mark.Oral = request.Score.Value;
                break;
            case 5:
                if (request.Score.HasValue) mark.FinalExam = request.Score.Value;
                break;
            default:
                return BadRequest(new { success = false, message = "نوع المذاكرة غير صحيح" });
        }

        mark.Total = mark.Oral + mark.Quiz1 + mark.Quiz2 + mark.Homework + mark.FinalExam;
        mark.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var quizTypeName = ((QuizType)request.QuizTypeId).ToString();

        await notifier.SendAsync(student.Id, UserType.Student,
            "تحديث علامة المذاكرة",
            $"تم تحديث {quizTypeName} في {subject.Name}: {mark.Total}",
            "quiz_mark_update");

        return Ok(new
        {
            success = true,
            message = "تم تحديث علامة المذاكرة بنجاح",
            data = new
            {
                mark.Id,
                LocalStudentNumber = student.LocalStudentNumber,
                StudentName = student.Name,
                LocalSubjectId = subject.LocalSubjectId,
                SubjectName = subject.Name,
                Semester = mark.Semester,
                QuizTypeId = request.QuizTypeId,
                QuizTypeName = quizTypeName,
                mark.Oral,
                mark.Quiz1,
                mark.Quiz2,
                mark.Homework,
                mark.FinalExam,
                Total = mark.Total,
                UpdatedAt = mark.UpdatedAt
            }
        });
    }

    [HttpDelete("marks/quiz")]
    public async Task<IActionResult> DeleteQuizMark(
        [FromQuery] int localStudentNumber,
        [FromQuery] int localSubjectId,
        [FromQuery] int semester,
        [FromQuery] int quizTypeId)
    {
        var blocked = await rules.ValidateSecondPeriodAttendanceTakenAsync(TeacherId);
        if (blocked is not null) 
            return StatusCode(403, new { message = blocked });

        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalStudentNumber == localStudentNumber);
        
        if (student is null)
            return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber}" });

        var subject = await db.Subjects
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalSubjectId == localSubjectId);

        if (subject is null)
            return NotFound(new { success = false, message = $"لا توجد مادة برقم {localSubjectId}" });

        var teacherSubject = await db.TeacherSubjects
            .AnyAsync(t => t.TeacherId == TeacherId && t.SubjectId == subject.Id);

        if (!teacherSubject)
            return BadRequest(new { success = false, message = "هذه المادة ليست من موادك" });

        if (!Enum.IsDefined(typeof(QuizType), quizTypeId))
            return BadRequest(new { success = false, message = "نوع المذاكرة غير صحيح" });

        var mark = await db.Marks
            .FirstOrDefaultAsync(m => m.StudentId == student.Id && 
                                      m.SubjectId == subject.Id && 
                                      m.Semester == semester);

        if (mark is null)
            return NotFound(new { 
                success = false, 
                message = $"لا توجد علامة للطالب {localStudentNumber} في مادة {localSubjectId} للفصل {semester}" 
            });

        var subjectName = subject.Name;
        var studentId = student.Id;
        var quizTypeName = ((QuizType)quizTypeId).ToString();

        db.Marks.Remove(mark);
        await db.SaveChangesAsync();

        await notifier.SendAsync(studentId, UserType.Student,
            "حذف علامة المذاكرة",
            $"تم حذف {quizTypeName} في مادة {subjectName} (الفصل {semester})",
            "quiz_mark_delete");

        return Ok(new
        {
            success = true,
            message = "تم حذف علامة المذاكرة بنجاح",
            data = new
            {
                LocalStudentNumber = localStudentNumber,
                StudentName = student.Name,
                LocalSubjectId = localSubjectId,
                SubjectName = subjectName,
                QuizTypeId = quizTypeId,
                QuizTypeName = quizTypeName,
                Semester = semester,
                DeletedAt = DateTime.UtcNow
            }
        });
    }

    [HttpGet("schedule-image")]
    public async Task<IActionResult> GetScheduleImage()
    {
        var image = await db.ScheduleImages
            .Where(s => s.SchoolId == SchoolId && 
                        s.TeacherId == TeacherId && 
                        s.Type == ScheduleImageType.Teacher)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.ImageUrl,
                s.Description,
                s.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (image is null)
            return NotFound(new { success = false, message = "لا توجد صورة جدول لك" });

        return Ok(new
        {
            success = true,
            message = "تم جلب صورة الجدول بنجاح",
            data = image
        });
    }

    [HttpGet("full-profile")]
    public async Task<IActionResult> GetFullProfile()
    {
        var me = await db.Employees.FindAsync(TeacherId);
        if (me is null) 
            return NotFound();

        var primarySchool = await db.EmployeeSchools
            .Include(es => es.School)
            .FirstOrDefaultAsync(es => es.EmployeeId == TeacherId && es.IsActive);

        var primarySchoolId = primarySchool?.SchoolId ?? 0;
        var primarySchoolName = primarySchool?.School?.Name ?? "غير معروف";
        var localEmployeeNumber = primarySchool?.LocalEmployeeNumber ?? 0;

        var teacher = new
        {
            me.Id,
            me.Name,
            me.Email,
            LocalEmployeeNumber = localEmployeeNumber,
            PrimarySchoolId = primarySchoolId,
            PrimarySchoolName = primarySchoolName,
            me.Phone,
            me.Address,
            me.BirthDate,
            me.Qualification,
            me.IsDismissed,
            me.CreatedAt
        };

        var assignments = await db.TeacherAssignments
            .Where(t => t.EmployeeId == TeacherId)
            .Join(db.Schools, t => t.SchoolId, s => s.Id, (t, s) => new { SchoolId = s.Id, SchoolName = s.Name })
            .ToListAsync();

        var schools = new List<object>();
        foreach (var a in assignments)
        {
            var teacherData = await db.TeacherGrades
                .Where(t => t.TeacherId == TeacherId)
                .Include(t => t.Subject)
                    .ThenInclude(s => s!.Grade)
                .Include(t => t.Section)
                    .ThenInclude(s => s!.Grade)
                .Where(t => t.Subject!.SchoolId == a.SchoolId)
                .Select(t => new
                {
                    t.SubjectId,
                    LocalSubjectId = t.Subject != null ? t.Subject.LocalSubjectId : 0,
                    SubjectName = t.Subject != null ? t.Subject.Name : null,
                    t.SectionId,
                    SectionName = t.Section != null ? t.Section.Name : null,
                    LocalSectionNumber = t.Section != null ? t.Section.LocalSectionNumber : 0,
                    SectionGradeId = t.Section != null ? t.Section.GradeId : 0,
                    SectionGradeName = t.Section != null && t.Section.Grade != null ? t.Section.Grade.Name : null,
                    SectionLocalGradeNumber = t.Section != null && t.Section.Grade != null ? t.Section.Grade.LocalGradeNumber : 0,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            var subjectsOrganized = teacherData
                .GroupBy(s => new { s.SubjectId, s.LocalSubjectId, s.SubjectName })
                .Select(subject => new
                {
                    SubjectId = subject.Key.SubjectId,
                    LocalSubjectId = subject.Key.LocalSubjectId,
                    SubjectName = subject.Key.SubjectName,
                    Grades = subject
                        .GroupBy(g => new { g.SectionGradeId, g.SectionGradeName, g.SectionLocalGradeNumber })
                        .Select(grade => new
                        {
                            GradeId = grade.Key.SectionGradeId,
                            GradeName = grade.Key.SectionGradeName,
                            LocalGradeNumber = grade.Key.SectionLocalGradeNumber,
                            Sections = grade.Select(s => new
                            {
                                s.SectionId,
                                s.SectionName,
                                s.LocalSectionNumber,
                                s.CreatedAt
                            })
                            .OrderBy(s => s.LocalSectionNumber)
                            .ToList()
                        })
                        .OrderBy(g => g.LocalGradeNumber)
                        .ToList()
                })
                .OrderBy(s => s.LocalSubjectId)
                .ToList();

            var localEmpNumber = await db.EmployeeSchools
                .Where(es => es.EmployeeId == TeacherId && es.SchoolId == a.SchoolId && es.IsActive)
                .Select(es => es.LocalEmployeeNumber)
                .FirstOrDefaultAsync();

            var school = new
            {
                a.SchoolId,
                SchoolName = a.SchoolName,
                LocalEmployeeNumber = localEmpNumber,
                Subjects = subjectsOrganized
            };

            schools.Add(school);
        }

        var marks = await db.Marks
            .Where(m => db.TeacherSubjects.Any(t => t.TeacherId == TeacherId && t.SubjectId == m.SubjectId))
            .OrderByDescending(m => m.UpdatedAt).Take(500)
            .Select(m => new
            {
                m.Id,
                StudentLocalNumber = m.Student != null ? m.Student.LocalStudentNumber : 0,
                StudentName = m.Student != null ? m.Student.Name : null,
                LocalSubjectId = m.Subject != null ? m.Subject.LocalSubjectId : 0,
                SubjectName = m.Subject != null ? m.Subject.Name : null,
                m.Semester,
                m.Oral,
                m.Quiz1,
                m.Quiz2,
                m.Homework,
                m.FinalExam,
                m.Total,
                m.UpdatedAt
            })
            .ToListAsync();

        var attendance = await db.EmployeeAttendances
            .Where(a => a.EmployeeId == TeacherId)
            .OrderByDescending(a => a.Date).Take(200)
            .Select(a => new
            {
                a.Date,
                Status = a.Status.ToString(),
                a.OnLeave
            })
            .ToListAsync();

        var leaves = await db.Leaves
            .Where(l => l.EmployeeId == TeacherId)
            .OrderByDescending(l => l.StartDate)
            .Select(l => new
            {
                l.Id,
                l.StartDate,
                l.EndDate,
                l.Reason
            })
            .ToListAsync();

        var perfReports = await db.PerformanceReports
            .Where(r => r.TeacherId == TeacherId)
            .Join(db.Subjects, r => r.SubjectId, s => s.Id, (r, s) => new { r, s })
            .OrderByDescending(x => x.r.CreatedAt)
            .Select(x => new
            {
                x.r.Id,
                StudentLocalNumber = x.r.Student != null ? x.r.Student.LocalStudentNumber : 0,
                StudentName = x.r.Student != null ? x.r.Student.Name : null,
                SubjectName = x.s.Name,
                LocalSubjectId = x.s.LocalSubjectId,
                x.r.Semester,
                x.r.Behavior,
                x.r.Notes,
                x.r.CreatedAt
            })
            .ToListAsync();

        var complaints = await db.Complaints
            .Where(c => c.FromUserId == TeacherId && c.FromUserType == UserType.Employee)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Against,
                c.Content,
                Status = c.Status.ToString(),
                c.Resolution,
                c.CreatedAt
            })
            .ToListAsync();

        var punishments = await db.Punishments
            .Where(p => p.EmployeeId == TeacherId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.Reason,
                Type = p.Type.ToString(),
                p.CreatedAt
            })
            .ToListAsync();

        var notifications = await db.Notifications
            .Where(n => n.UserId == TeacherId && n.UserType == UserType.Employee)
            .OrderByDescending(n => n.CreatedAt).Take(100)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Body,
                n.Type,
                n.IsRead,
                n.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب الملف الكامل للمعلم بنجاح",
            data = new
            {
                Teacher = teacher,
                Schools = schools,
                Marks = marks,
                Attendance = attendance,
                Leaves = leaves,
                PerformanceReports = perfReports,
                Complaints = complaints,
                Punishments = punishments,
                Notifications = notifications
            }
        });
    }
}