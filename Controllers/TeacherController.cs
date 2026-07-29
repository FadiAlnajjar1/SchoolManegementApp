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

    // ✅ Helper Method للتعامل مع SchoolId - تعيد (bool isValid, int schoolId, string? errorMessage)
    private async Task<(bool IsValid, int SchoolId, string? ErrorMessage)> GetEffectiveSchoolIdAsync(int? requestSchoolId = null)
    {
        // إذا تم إرسال SchoolId في الطلب، تحقق من صلاحية المعلم فيه
        if (requestSchoolId.HasValue && requestSchoolId.Value > 0)
        {
            var hasAccess = await db.EmployeeSchools
                .AnyAsync(es => es.EmployeeId == TeacherId && 
                               es.SchoolId == requestSchoolId.Value && 
                               es.IsActive);
            
            if (!hasAccess)
                return (false, 0, "ليس لديك صلاحية في هذه المدرسة");
            
            return (true, requestSchoolId.Value, null);
        }
        
        // إذا لم يتم إرساله، تحقق من عدد المدارس التي يعمل بها المعلم
        var schoolCount = await db.EmployeeSchools
            .CountAsync(es => es.EmployeeId == TeacherId && es.IsActive);
        
        // إذا كان يعمل في مدرسة واحدة فقط، استخدمها
        if (schoolCount == 1)
        {
            var school = await db.EmployeeSchools
                .FirstOrDefaultAsync(es => es.EmployeeId == TeacherId && es.IsActive);
            return (true, school?.SchoolId ?? 0, null);
        }
        
        // إذا كان يعمل في أكثر من مدرسة ولم يرسل SchoolId، أبلغ المستخدم
        return (false, 0, "أنت تعمل في أكثر من مدرسة. يرجى تحديد schoolId في الطلب");
    }

    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects([FromQuery] int? schoolId = null)
    {
        // ✅ التحقق من SchoolId
        var (isValid, effectiveSchoolId, errorMessage) = await GetEffectiveSchoolIdAsync(schoolId);
        if (!isValid)
            return BadRequest(new { success = false, message = errorMessage });

        var subjects = await db.TeacherGrades
            .Where(t => t.TeacherId == TeacherId && 
                        t.Section != null &&
                        t.Section.SchoolId == effectiveSchoolId)
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
        [FromQuery] int? localSubjectId = null,
        [FromQuery] int? schoolId = null)
    {
        // ✅ التحقق من SchoolId
        var (isValid, effectiveSchoolId, errorMessage) = await GetEffectiveSchoolIdAsync(schoolId);
        if (!isValid)
            return BadRequest(new { success = false, message = errorMessage });

        var grade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == effectiveSchoolId && 
                                      g.LocalGradeNumber == localGradeNumber);

        if (grade is null)
            return NotFound(new { success = false, message = $"لا يوجد صف برقم {localGradeNumber} في هذه المدرسة" });

        var section = await db.Sections
            .FirstOrDefaultAsync(s => s.GradeId == grade.Id && 
                                      s.LocalSectionNumber == localSectionNumber &&
                                      s.SchoolId == effectiveSchoolId);

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
                .FirstOrDefaultAsync(s => s.SchoolId == effectiveSchoolId && 
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
                                   db.TeacherGrades.Any(t => t.TeacherId == TeacherId && t.SubjectId == m.SubjectId))
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
public async Task<IActionResult> AddQuizMark(
    [FromBody] QuizMarkRequest request,
    [FromQuery] int? schoolId = null)
{
    try
    {
        // ✅ التحقق من SchoolId
        var (isValid, effectiveSchoolId, errorMessage) = await GetEffectiveSchoolIdAsync(schoolId);
        if (!isValid)
            return BadRequest(new { success = false, message = errorMessage });

        var blocked = await rules.ValidateSecondPeriodAttendanceTakenAsync(TeacherId);
        if (blocked is not null) 
            return StatusCode(403, new { message = blocked });

        // ✅ البحث عن المادة
        var subject = await db.Subjects
            .FirstOrDefaultAsync(s => s.SchoolId == effectiveSchoolId &&
                                      s.LocalSubjectId == request.LocalSubjectId);

        if (subject is null)
            return BadRequest(new { success = false, message = $"لا توجد مادة برقم {request.LocalSubjectId}" });

        // ✅ التحقق من أن المعلم يدرس هذه المادة
        var teacherSubject = await db.TeacherGrades
            .FirstOrDefaultAsync(t => t.TeacherId == TeacherId && t.SubjectId == subject.Id);
        
        if (teacherSubject is null) 
            return BadRequest(new { success = false, message = "هذه المادة ليست من موادك" });

        // ✅ البحث عن الطالب
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == effectiveSchoolId &&
                                      s.LocalStudentNumber == request.LocalStudentNumber);
        
        if (student is null) 
            return BadRequest(new { success = false, message = $"لا يوجد طالب برقم {request.LocalStudentNumber}" });

        // ✅ التحقق من صحة نوع الاختبار
        if (!Enum.IsDefined(typeof(QuizType), request.QuizTypeId))
            return BadRequest(new { success = false, message = "نوع المذاكرة غير صحيح" });

        // ✅ التحقق من أن العلامة لا تتجاوز العلامة الكاملة
        if (request.Score > request.MaxScore)
        {
            return BadRequest(new { 
                success = false, 
                message = $"العلامة المدخلة ({request.Score}) تتجاوز العلامة الكاملة ({request.MaxScore})" 
            });
        }

        if (request.Score < 0)
        {
            return BadRequest(new { 
                success = false, 
                message = "العلامة لا يمكن أن تكون سالبة" 
            });
        }

        if (request.MaxScore <= 0)
        {
            return BadRequest(new { 
                success = false, 
                message = "العلامة الكاملة يجب أن تكون أكبر من صفر" 
            });
        }

        // ✅ البحث عن العلامة الموجودة
        var existingMark = await db.Marks
            .FirstOrDefaultAsync(m => m.StudentId == student.Id && 
                                      m.SubjectId == subject.Id && 
                                      m.Semester == request.Semester);

        if (existingMark is null)
        {
            // ✅ إنشاء علامة جديدة
            existingMark = new Mark
            {
                StudentId = student.Id,
                SubjectId = subject.Id,
                Semester = request.Semester,
                EnteredById = TeacherId,
                SchoolId = effectiveSchoolId,
                CreatedAt = DateTime.UtcNow
            };
            db.Marks.Add(existingMark);
        }

        // ✅ تحديث العلامة المكتسبة والعلامة الكاملة
        switch (request.QuizTypeId)
        {
            case 1: // Quiz1
                existingMark.Quiz1 = request.Score;
                existingMark.MaxQuiz1 = request.MaxScore;
                break;
            case 2: // Quiz2
                existingMark.Quiz2 = request.Score;
                existingMark.MaxQuiz2 = request.MaxScore;
                break;
            case 3: // Homework
                existingMark.Homework = request.Score;
                existingMark.MaxHomework = request.MaxScore;
                break;
            case 4: // Oral
                existingMark.Oral = request.Score;
                existingMark.MaxOral = request.MaxScore;
                break;
            case 5: // FinalExam
                existingMark.FinalExam = request.Score;
                existingMark.MaxFinalExam = request.MaxScore;
                break;
            default:
                return BadRequest(new { success = false, message = "نوع المذاكرة غير صحيح" });
        }

        // ✅ حساب المجموع الكلي (مجموع جميع العلامات المكتسبة)
        existingMark.Total = existingMark.Oral + existingMark.Quiz1 + existingMark.Quiz2 + 
                             existingMark.Homework + existingMark.FinalExam;

        // ✅ حفظ الملاحظات
        if (!string.IsNullOrEmpty(request.Notes))
            existingMark.Notes = request.Notes;

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
                MaxScore = request.MaxScore,
                existingMark.Oral,
                existingMark.Quiz1,
                existingMark.Quiz2,
                existingMark.Homework,
                existingMark.FinalExam,
                existingMark.MaxOral,
                existingMark.MaxQuiz1,
                existingMark.MaxQuiz2,
                existingMark.MaxHomework,
                existingMark.MaxFinalExam,
                Total = existingMark.Total,
                existingMark.Notes,
                existingMark.CreatedAt,
                existingMark.UpdatedAt
            }
        });
    }
    catch (Exception ex)
    {
        // ✅ التعامل مع الأخطاء
        return StatusCode(500, new
        {
            success = false,
            message = "حدث خطأ أثناء تسجيل العلامة",
            error = ex.Message
        });
    }
}

    [HttpPut("marks/quiz")]
public async Task<IActionResult> UpdateQuizMark(
    [FromBody] QuizMarkUpdateLocalRequest request,
    [FromQuery] int? schoolId = null)
{
    // ✅ التحقق من SchoolId
    var (isValid, effectiveSchoolId, errorMessage) = await GetEffectiveSchoolIdAsync(schoolId);
    if (!isValid)
        return BadRequest(new { success = false, message = errorMessage });

    var blocked = await rules.ValidateSecondPeriodAttendanceTakenAsync(TeacherId);
    if (blocked is not null) 
        return StatusCode(403, new { message = blocked });

    var student = await db.Students
        .FirstOrDefaultAsync(s => s.SchoolId == effectiveSchoolId &&
                                  s.LocalStudentNumber == request.LocalStudentNumber);
    
    if (student is null)
        return NotFound(new { success = false, message = $"لا يوجد طالب برقم {request.LocalStudentNumber}" });

    var subject = await db.Subjects
        .FirstOrDefaultAsync(s => s.SchoolId == effectiveSchoolId &&
                                  s.LocalSubjectId == request.LocalSubjectId);

    if (subject is null)
        return NotFound(new { success = false, message = $"لا توجد مادة برقم {request.LocalSubjectId}" });

    var teacherSubject = await db.TeacherGrades
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

    // ✅ التحقق من صحة العلامات
    if (request.Score.HasValue && request.MaxScore.HasValue)
    {
        if (request.Score.Value > request.MaxScore.Value)
        {
            return BadRequest(new { 
                success = false, 
                message = $"العلامة المدخلة ({request.Score.Value}) تتجاوز العلامة الكاملة ({request.MaxScore.Value})" 
            });
        }
        
        if (request.Score.Value < 0)
        {
            return BadRequest(new { 
                success = false, 
                message = "العلامة لا يمكن أن تكون سالبة" 
            });
        }
    }

    // ✅ تحديث العلامة المكتسبة والعلامة الكاملة
    switch (request.QuizTypeId)
    {
        case 1: // Quiz1
            if (request.Score.HasValue) 
                mark.Quiz1 = request.Score.Value;
            if (request.MaxScore.HasValue) 
                mark.MaxQuiz1 = request.MaxScore.Value;
            break;
        case 2: // Quiz2
            if (request.Score.HasValue) 
                mark.Quiz2 = request.Score.Value;
            if (request.MaxScore.HasValue) 
                mark.MaxQuiz2 = request.MaxScore.Value;
            break;
        case 3: // Homework
            if (request.Score.HasValue) 
                mark.Homework = request.Score.Value;
            if (request.MaxScore.HasValue) 
                mark.MaxHomework = request.MaxScore.Value;
            break;
        case 4: // Oral
            if (request.Score.HasValue) 
                mark.Oral = request.Score.Value;
            if (request.MaxScore.HasValue) 
                mark.MaxOral = request.MaxScore.Value;
            break;
        case 5: // FinalExam
            if (request.Score.HasValue) 
                mark.FinalExam = request.Score.Value;
            if (request.MaxScore.HasValue) 
                mark.MaxFinalExam = request.MaxScore.Value;
            break;
        default:
            return BadRequest(new { success = false, message = "نوع المذاكرة غير صحيح" });
    }

    // ✅ تحديث الملاحظات إذا وجدت
    if (!string.IsNullOrEmpty(request.Notes))
        mark.Notes = request.Notes;

    // ✅ إعادة حساب المجموع الكلي
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
            
            // ✅ العلامات المكتسبة
            mark.Oral,
            mark.Quiz1,
            mark.Quiz2,
            mark.Homework,
            mark.FinalExam,
            
            // ✅ العلامات الكاملة
            mark.MaxOral,
            mark.MaxQuiz1,
            mark.MaxQuiz2,
            mark.MaxHomework,
            mark.MaxFinalExam,
            
            // ✅ المجموع والملاحظات
            Total = mark.Total,
            mark.Notes,
            mark.UpdatedAt
        }
    });
}

    [HttpDelete("marks/quiz")]
public async Task<IActionResult> DeleteQuizMark(
    [FromQuery] int localStudentNumber,
    [FromQuery] int localSubjectId,
    [FromQuery] int semester,
    [FromQuery] int quizTypeId,
    [FromQuery] int? schoolId = null)
{
    // ✅ التحقق من SchoolId
    var (isValid, effectiveSchoolId, errorMessage) = await GetEffectiveSchoolIdAsync(schoolId);
    if (!isValid)
        return BadRequest(new { success = false, message = errorMessage });

    var blocked = await rules.ValidateSecondPeriodAttendanceTakenAsync(TeacherId);
    if (blocked is not null) 
        return StatusCode(403, new { message = blocked });

    var student = await db.Students
        .FirstOrDefaultAsync(s => s.SchoolId == effectiveSchoolId &&
                                  s.LocalStudentNumber == localStudentNumber);
    
    if (student is null)
        return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber}" });

    var subject = await db.Subjects
        .FirstOrDefaultAsync(s => s.SchoolId == effectiveSchoolId &&
                                  s.LocalSubjectId == localSubjectId);

    if (subject is null)
        return NotFound(new { success = false, message = $"لا توجد مادة برقم {localSubjectId}" });

    var teacherSubject = await db.TeacherGrades
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

    // ✅ حذف العلامة المحددة فقط (تعيينها إلى 0)
    switch (quizTypeId)
    {
        case 1: // Quiz1
            mark.Quiz1 = 0;
            mark.MaxQuiz1 = 0;
            break;
        case 2: // Quiz2
            mark.Quiz2 = 0;
            mark.MaxQuiz2 = 0;
            break;
        case 3: // Homework
            mark.Homework = 0;
            mark.MaxHomework = 0;
            break;
        case 4: // Oral
            mark.Oral = 0;
            mark.MaxOral = 0;
            break;
        case 5: // FinalExam
            mark.FinalExam = 0;
            mark.MaxFinalExam = 0;
            break;
        default:
            return BadRequest(new { success = false, message = "نوع المذاكرة غير صحيح" });
    }

    // ✅ إعادة حساب المجموع الكلي
    mark.Total = mark.Oral + mark.Quiz1 + mark.Quiz2 + 
                 mark.Homework + mark.FinalExam;
    
    mark.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();

    // ✅ إرسال إشعار
    await notifier.SendAsync(studentId, UserType.Student,
        "حذف علامة المذاكرة",
        $"تم حذف {quizTypeName} في مادة {subjectName} (الفصل {semester})",
        "quiz_mark_delete");

    return Ok(new
    {
        success = true,
        message = $"تم حذف {quizTypeName} بنجاح",
        data = new
        {
            LocalStudentNumber = localStudentNumber,
            StudentName = student.Name,
            LocalSubjectId = localSubjectId,
            SubjectName = subjectName,
            QuizTypeId = quizTypeId,
            QuizTypeName = quizTypeName,
            Semester = semester,
            // ✅ العلامات المتبقية
            RemainingMarks = new
            {
                mark.Oral,
                mark.Quiz1,
                mark.Quiz2,
                mark.Homework,
                mark.FinalExam,
                mark.Total
            },
            DeletedAt = DateTime.UtcNow
        }
    });
}

    [HttpGet("schedule-image")]
    public async Task<IActionResult> GetScheduleImage([FromQuery] int? schoolId = null)
    {
        // ✅ التحقق من SchoolId
        var (isValid, effectiveSchoolId, errorMessage) = await GetEffectiveSchoolIdAsync(schoolId);
        if (!isValid)
            return BadRequest(new { success = false, message = errorMessage });

        var image = await db.ScheduleImages
            .Where(s => s.SchoolId == effectiveSchoolId && 
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
        return NotFound(new { success = false, message = "المعلم غير موجود" });

    // ✅ جلب جميع المدارس التي يعمل بها المعلم
    var employeeSchools = await db.EmployeeSchools
        .Where(es => es.EmployeeId == TeacherId && es.IsActive)
        .Include(es => es.School)
        .ToListAsync();

    if (!employeeSchools.Any())
        return BadRequest(new { success = false, message = "لا توجد مدارس مرتبطة بك" });

    // ✅ المدرسة الأولى كـ Primary (أو يمكن جلبها كلها)
    var primarySchool = employeeSchools.FirstOrDefault();
    var primarySchoolName = primarySchool?.School?.Name ?? "غير معروف";
    var localEmployeeNumber = primarySchool?.LocalEmployeeNumber ?? 0;
    var primarySchoolId = primarySchool?.SchoolId ?? 0;

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

    // ✅ جلب جميع المدارس مع المواد
    var schools = new List<object>();
    foreach (var es in employeeSchools)
    {
        var schoolId = es.SchoolId;
        var schoolName = es.School?.Name ?? "غير معروف";

        var teacherData = await db.TeacherGrades
            .Where(t => t.TeacherId == TeacherId && 
                       t.Section != null && 
                       t.Section.SchoolId == schoolId)
            .Include(t => t.Subject)
                .ThenInclude(s => s!.Grade)
            .Include(t => t.Section)
                .ThenInclude(s => s!.Grade)
            .Where(t => t.Subject!.SchoolId == schoolId)
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

        var localEmpNumber = es.LocalEmployeeNumber;

        var school = new
        {
            SchoolId = schoolId,
            SchoolName = schoolName,
            LocalEmployeeNumber = localEmpNumber,
            Subjects = subjectsOrganized
        };

        schools.Add(school);
    }

    // ✅ باقي البيانات (نفسها مع تعديل بسيط)
    var marks = await db.Marks
        .Where(m => db.TeacherGrades.Any(t => t.TeacherId == TeacherId && t.SubjectId == m.SubjectId))
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

    // ✅ Performance Reports - بدون schoolId (جلب كل التقارير)
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
    // ============================================
// جلب معلومات طالب معين - للمعلمين
// ============================================

[HttpGet("students/{localStudentNumber:int}/full-profile")]
public async Task<IActionResult> GetStudentFullProfile(
    [FromRoute] int localStudentNumber,
    [FromQuery] int? schoolId = null)
{
    // ✅ بناء الاستعلام الأساسي
    var query = db.Students
        .Include(s => s.Section)
            .ThenInclude(sec => sec!.Grade)
        .AsQueryable();

    // ✅ إذا تم إرسال schoolId، فلترة الطلاب حسب المدرسة
    if (schoolId.HasValue && schoolId.Value > 0)
    {
        var hasAccess = await db.EmployeeSchools
            .AnyAsync(es => es.EmployeeId == TeacherId && 
                           es.SchoolId == schoolId.Value && 
                           es.IsActive);

        if (!hasAccess)
            return BadRequest(new { 
                success = false, 
                message = "ليس لديك صلاحية في هذه المدرسة" 
            });

        query = query.Where(s => s.SchoolId == schoolId.Value);
    }

    // ✅ البحث عن الطالب
    var student = await query
        .FirstOrDefaultAsync(s => s.LocalStudentNumber == localStudentNumber);

    if (student is null)
        return NotFound(new { 
            success = false, 
            message = $"لا يوجد طالب برقم {localStudentNumber}" 
        });

    // ✅ التحقق من أن الطالب في شعبة يدرسها المعلم
    if (student.SectionId is null)
        return BadRequest(new { success = false, message = "الطالب ليس في أي شعبة" });

    var teachesStudent = await db.TeacherGrades
        .AnyAsync(tg => tg.TeacherId == TeacherId && 
                       tg.SectionId == student.SectionId);

    if (!teachesStudent)
        return BadRequest(new { 
            success = false, 
            message = "أنت لا تدرس هذا الطالب" 
        });

    // ✅ جلب العلامات مع جميع التفاصيل (بما في ذلك Max...)
    var marksQuery = db.Marks
        .Include(m => m.Subject)
        .Where(m => m.StudentId == student.Id)
        .AsQueryable();

    if (schoolId.HasValue && schoolId.Value > 0)
    {
        marksQuery = marksQuery.Where(m => m.SchoolId == schoolId.Value);
    }

    // ✅ جلب العلامات مع القيم المخزنة في قاعدة البيانات
    // ✅ جلب العلامات مع جميع التفاصيل
var marks = await marksQuery
    .Select(m => new
    {
        m.Id,
        m.Semester,
        m.Oral,
        m.Quiz1,
        m.Quiz2,
        m.Homework,
        m.FinalExam,
        m.Total,
        m.MaxOral,
        m.MaxQuiz1,
        m.MaxQuiz2,
        m.MaxHomework,
        m.MaxFinalExam,
        m.Notes,
        m.UpdatedAt,
        LocalSubjectId = m.Subject != null ? m.Subject.LocalSubjectId : 0,
        SubjectName = m.Subject != null ? m.Subject.Name : null,
    })
    .ToListAsync();

// ✅ فصل العلامات حسب الفصل الدراسي (كل علامة على حدة)
var semester1Marks = marks
    .Where(m => m.Semester == 1)
    .Select(m => new
    {
        LocalSubjectId = m.LocalSubjectId,
        SubjectName = m.SubjectName,
        Oral = m.Oral,
        Quiz1 = m.Quiz1,
        Quiz2 = m.Quiz2,
        Homework = m.Homework,
        FinalExam = m.FinalExam,
        Total = m.Total,  // ✅ كل علامة على حدة
        MaxOral = m.MaxOral,
        MaxQuiz1 = m.MaxQuiz1,
        MaxQuiz2 = m.MaxQuiz2,
        MaxHomework = m.MaxHomework,
        MaxFinalExam = m.MaxFinalExam,
        OralPercent = m.MaxOral > 0 ? Math.Round((decimal)m.Oral / (decimal)m.MaxOral * 100, 2) : 0,
        Quiz1Percent = m.MaxQuiz1 > 0 ? Math.Round((decimal)m.Quiz1 / (decimal)m.MaxQuiz1 * 100, 2) : 0,
        Quiz2Percent = m.MaxQuiz2 > 0 ? Math.Round((decimal)m.Quiz2 / (decimal)m.MaxQuiz2 * 100, 2) : 0,
        HomeworkPercent = m.MaxHomework > 0 ? Math.Round((decimal)m.Homework / (decimal)m.MaxHomework * 100, 2) : 0,
        FinalExamPercent = m.MaxFinalExam > 0 ? Math.Round((decimal)m.FinalExam / (decimal)m.MaxFinalExam * 100, 2) : 0,
        TotalPercent = (m.MaxOral + m.MaxQuiz1 + m.MaxQuiz2 + m.MaxHomework + m.MaxFinalExam) > 0 ?
            Math.Round((decimal)m.Total / (decimal)(m.MaxOral + m.MaxQuiz1 + m.MaxQuiz2 + m.MaxHomework + m.MaxFinalExam) * 100, 2) : 0,
        Grade = m.Total >= 90 ? "ممتاز" :
               m.Total >= 80 ? "جيد جداً" :
               m.Total >= 70 ? "جيد" :
               m.Total >= 60 ? "مقبول" : "ضعيف",
        m.Notes,
        m.UpdatedAt
    })
    .OrderBy(m => m.LocalSubjectId)
    .ToList();

// ✅ نفس الشيء للفصل الثاني
var semester2Marks = marks
    .Where(m => m.Semester == 2)
    .Select(m => new
    {
        LocalSubjectId = m.LocalSubjectId,
        SubjectName = m.SubjectName,
        Oral = m.Oral,
        Quiz1 = m.Quiz1,
        Quiz2 = m.Quiz2,
        Homework = m.Homework,
        FinalExam = m.FinalExam,
        Total = m.Total,  // ✅ كل علامة على حدة
        MaxOral = m.MaxOral,
        MaxQuiz1 = m.MaxQuiz1,
        MaxQuiz2 = m.MaxQuiz2,
        MaxHomework = m.MaxHomework,
        MaxFinalExam = m.MaxFinalExam,
        OralPercent = m.MaxOral > 0 ? Math.Round((decimal)m.Oral / (decimal)m.MaxOral * 100, 2) : 0,
        Quiz1Percent = m.MaxQuiz1 > 0 ? Math.Round((decimal)m.Quiz1 / (decimal)m.MaxQuiz1 * 100, 2) : 0,
        Quiz2Percent = m.MaxQuiz2 > 0 ? Math.Round((decimal)m.Quiz2 / (decimal)m.MaxQuiz2 * 100, 2) : 0,
        HomeworkPercent = m.MaxHomework > 0 ? Math.Round((decimal)m.Homework / (decimal)m.MaxHomework * 100, 2) : 0,
        FinalExamPercent = m.MaxFinalExam > 0 ? Math.Round((decimal)m.FinalExam / (decimal)m.MaxFinalExam * 100, 2) : 0,
        TotalPercent = (m.MaxOral + m.MaxQuiz1 + m.MaxQuiz2 + m.MaxHomework + m.MaxFinalExam) > 0 ?
            Math.Round((decimal)m.Total / (decimal)(m.MaxOral + m.MaxQuiz1 + m.MaxQuiz2 + m.MaxHomework + m.MaxFinalExam) * 100, 2) : 0,
        Grade = m.Total >= 90 ? "ممتاز" :
               m.Total >= 80 ? "جيد جداً" :
               m.Total >= 70 ? "جيد" :
               m.Total >= 60 ? "مقبول" : "ضعيف",
        m.Notes,
        m.UpdatedAt
    })
    .OrderBy(m => m.LocalSubjectId)
    .ToList();

// ✅ حساب المتوسطات (متوسط العلامات وليس مجموعها)
var semester1Average = semester1Marks.Any() 
    ? Math.Round(semester1Marks.Average(m => m.Total), 2)  // ✅ متوسط العلامات
    : 0;

var semester2Average = semester2Marks.Any() 
    ? Math.Round(semester2Marks.Average(m => m.Total), 2)  // ✅ متوسط العلامات
    : 0;

// ✅ المتوسط النهائي (متوسط جميع العلامات)
var finalAverage = marks.Any() 
    ? Math.Round(marks.Average(m => m.Total), 2)  // ✅ متوسط جميع العلامات
    : 0;

    // ✅ الرد النهائي
    return Ok(new
    {
        success = true,
        message = "تم جلب ملف الطالب بنجاح",
        data = new
        {
            // ✅ المعلومات الأساسية
            Student = new
            {
                student.Id,
                student.Name,
                student.Email,
                LocalStudentNumber = student.LocalStudentNumber,
                student.GuardianName,
                student.GuardianPhone,
                student.BirthDate,
                student.Address,
                student.DismissalWarning,
                student.CreatedAt,
                
                SectionName = student.Section?.Name,
                LocalSectionNumber = student.Section?.LocalSectionNumber ?? 0,
                GradeName = student.Section?.Grade?.Name,
                LocalGradeNumber = student.Section?.Grade?.LocalGradeNumber ?? 0,
                AcademicYear = student.Section?.Grade?.AcademicYear ?? 0
            },

           Semester1Marks = semester1Marks,
        Semester2Marks = semester2Marks,
        
        // ✅ المتوسطات الصحيحة
        Averages = new
        {
            Semester1 = semester1Average,    // ✅ متوسط الفصل الأول
            Semester2 = semester2Average,    // ✅ متوسط الفصل الثاني
            Final = finalAverage,            // ✅ المتوسط النهائي
            SubjectsCount = marks.Count,
            PassedSubjects = marks.Count(m => m.Total >= 60),
            FailedSubjects = marks.Count(m => m.Total < 60),
            SuccessRate = marks.Any() ? 
                Math.Round((double)marks.Count(m => m.Total >= 60) / marks.Count * 100, 2) : 0
        }
    }
});
}
}