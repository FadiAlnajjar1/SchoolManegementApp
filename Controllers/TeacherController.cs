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

    // ============================================
    // المواد التي يدرسها المعلم (باستخدام TeacherGrade)
    // ============================================

    [HttpGet("subjects")]
public async Task<IActionResult> GetSubjects()
{
    var subjects = await db.TeacherGrades
        .Where(t => t.TeacherId == TeacherId)
        .Include(t => t.Subject)
            .ThenInclude(s => s!.Grade)
        .Include(t => t.Section)
        .Select(t => new
        {
            t.SubjectId,
            LocalSubjectId = t.Subject != null ? t.Subject.LocalSubjectId : 0,
            SubjectName = t.Subject != null ? t.Subject.Name : null,
            LocalGradeNumber = t.Subject != null && t.Subject.Grade != null ? t.Subject.Grade.LocalGradeNumber : 0,
            GradeName = t.Subject != null && t.Subject.Grade != null ? t.Subject.Grade.Name : null,
            t.SectionId,
            SectionName = t.Section != null ? t.Section.Name : null,
            LocalSectionNumber = t.Section != null ? t.Section.LocalSectionNumber : 0,
            t.CreatedAt
        })
        .ToListAsync();

    var result = subjects
        .GroupBy(s => new { 
            s.SubjectId, 
            s.LocalSubjectId,
            s.SubjectName, 
            s.LocalGradeNumber, 
            s.GradeName 
        })
        .Select(g => new
        {
            LocalSubjectId = g.Key.LocalSubjectId,
            SubjectName = g.Key.SubjectName,
            LocalGradeNumber = g.Key.LocalGradeNumber,
            GradeName = g.Key.GradeName,
            Sections = g.Select(s => new
            {
                s.SectionId,
                SectionName = s.SectionName,
                LocalSectionNumber = s.LocalSectionNumber,
                s.CreatedAt
            }).ToList()
        })
        .OrderBy(g => g.LocalGradeNumber)
        .ToList();

    return Ok(result);
}

    // ============================================
    // الجدول الدراسي للمعلم (صورة)
    // ============================================

    [HttpGet("schedule-image")]
    public async Task<IActionResult> GetScheduleImage()
    {
        var image = await db.ScheduleImages
            .Where(s => s.SchoolId == SchoolId && 
                        s.TeacherId == TeacherId && 
                        s.Type == ScheduleImageType.Teacher)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (image is null)
            return NotFound(new { message = "لا توجد صورة جدول لك" });

        return Ok(new
        {
            image.Id,
            image.ImageUrl,
            image.Description,
            image.CreatedAt
        });
    }

    // ============================================
    // حضور الطلاب
    // ============================================

    [HttpPost("attendance")]
    public async Task<IActionResult> TakeAttendance(StudentAttendanceRequest request)
    {
        // التحقق من أن المعلم يدرس هذه الشعبة
        var teachesSection = await db.TeacherGrades
            .AnyAsync(tg => tg.TeacherId == TeacherId && tg.SectionId == request.SectionId);
        
        if (!teachesSection)
            return BadRequest(new { message = "أنت لا تدرس في هذه الشعبة" });

        // التحقق من وجود صورة جدول للمعلم
        var hasScheduleImage = await db.ScheduleImages
            .AnyAsync(s => s.SchoolId == SchoolId && 
                          s.TeacherId == TeacherId && 
                          s.Type == ScheduleImageType.Teacher);
        
        if (!hasScheduleImage)
            return BadRequest(new { message = "لم يتم رفع جدولك الدراسي بعد، يرجى التواصل مع الإدارة" });

        return await AttendanceHelper.RecordAsync(db, request, TeacherId, this);
    }

    // ============================================
    // إدارة العلامات (باستخدام TeacherSubject)
    // ============================================

    [HttpPost("marks")]
    public async Task<IActionResult> UpsertMark(MarkRequest request)
    {
        var blocked = await rules.ValidateSecondPeriodAttendanceTakenAsync(TeacherId);
        if (blocked is not null) 
            return StatusCode(403, new { message = blocked });

        // التحقق من أن المادة يدرسها المعلم (باستخدام TeacherSubject)
        var subject = await db.TeacherSubjects
            .Include(t => t.Subject)
            .FirstOrDefaultAsync(t => t.TeacherId == TeacherId && t.SubjectId == request.SubjectId);
        
        if (subject is null) 
            return BadRequest(new { message = "هذه المادة ليست من موادك" });

        // التحقق من وجود الطالب في مدارس المعلم
        var schoolIds = await GetSchoolIdsAsync();
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && schoolIds.Contains(s.SchoolId));
        
        if (student is null) 
            return BadRequest(new { message = "الطالب غير موجود في مدارسك" });

        // التحقق من حدود العلامات
        var config = await db.MarkConfigs
            .FirstOrDefaultAsync(c => c.SchoolId == subject.Subject!.SchoolId)
            ?? new MarkConfig { SchoolId = subject.Subject.SchoolId };

        if (request.Oral > config.MaxOral || request.Quiz1 > config.MaxQuiz1 ||
            request.Quiz2 > config.MaxQuiz2 || request.Homework > config.MaxHomework ||
            request.FinalExam > config.MaxFinalExam)
            return BadRequest(new { message = "علامة تتجاوز الحد الأعلى المضبوط للمدرسة" });

        // البحث عن علامة موجودة أو إنشاء جديدة
        var mark = await db.Marks
            .FirstOrDefaultAsync(m => m.StudentId == request.StudentId && 
                                      m.SubjectId == request.SubjectId && 
                                      m.Semester == request.Semester);
        
        if (mark is null)
        {
            mark = new Mark 
            { 
                StudentId = request.StudentId, 
                SubjectId = request.SubjectId, 
                Semester = request.Semester 
            };
            db.Marks.Add(mark);
        }

        mark.Oral = request.Oral;
        mark.Quiz1 = request.Quiz1;
        mark.Quiz2 = request.Quiz2;
        mark.Homework = request.Homework;
        mark.FinalExam = request.FinalExam;
        mark.Total = request.Oral + request.Quiz1 + request.Quiz2 + request.Homework + request.FinalExam;
        mark.EnteredById = TeacherId;
        mark.UpdatedAt = DateTime.UtcNow;
        
        await db.SaveChangesAsync();

        await notifier.SendAsync(student.Id, UserType.Student,
            "علامة جديدة", 
            $"رُصدت علامتك في {subject.Subject!.Name} (الفصل {request.Semester}): {mark.Total}", 
            "mark");

        return Ok(new
        {
            message = "تم تسجيل العلامة بنجاح",
            mark = new
            {
                mark.Id,
                mark.StudentId,
                StudentName = student.Name,
                mark.SubjectId,
                SubjectName = subject.Subject.Name,
                mark.Semester,
                mark.Oral,
                mark.Quiz1,
                mark.Quiz2,
                mark.Homework,
                mark.FinalExam,
                mark.Total,
                mark.UpdatedAt
            }
        });
    }

    // ============================================
    // علامات المذاكرات (Quizzes/Assignments)
    // ============================================

    [HttpPost("marks/quiz")]
    public async Task<IActionResult> AddQuizMark(QuizMarkRequest request)
    {
        var blocked = await rules.ValidateSecondPeriodAttendanceTakenAsync(TeacherId);
        if (blocked is not null) 
            return StatusCode(403, new { message = blocked });

        // التحقق من أن المادة يدرسها المعلم
        var subject = await db.TeacherSubjects
            .Include(t => t.Subject)
            .FirstOrDefaultAsync(t => t.TeacherId == TeacherId && t.SubjectId == request.SubjectId);
    
        if (subject is null) 
            return BadRequest(new { message = "هذه المادة ليست من موادك" });

        // التحقق من وجود الطالب
        var schoolIds = await GetSchoolIdsAsync();
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && schoolIds.Contains(s.SchoolId));
    
        if (student is null) 
            return BadRequest(new { message = "الطالب غير موجود في مدارسك" });

        // التحقق من عدم وجود تكرار
        var existingQuiz = await db.QuizMarks
            .AnyAsync(q => q.StudentId == request.StudentId && 
                          q.SubjectId == request.SubjectId && 
                          q.Semester == request.Semester &&
                          q.QuizNumber == request.QuizNumber);
    
        if (existingQuiz)
            return BadRequest(new { message = "هذه المذاكرة مسجلة بالفعل لهذا الطالب" });

        // إنشاء سجل مذاكرة
        var quizMark = new QuizMark
        {
            StudentId = request.StudentId,
            SubjectId = request.SubjectId,
            Semester = request.Semester,
            QuizNumber = request.QuizNumber,
            Score = request.Score,
            MaxScore = request.MaxScore,
            Date = DateOnly.FromDateTime(DateTime.Today),
            Notes = request.Notes ?? "",
            EnteredById = TeacherId,
            CreatedAt = DateTime.UtcNow
        };

        db.QuizMarks.Add(quizMark);
        await db.SaveChangesAsync();

        // حساب متوسط علامات المذاكرات
        var quizMarks = await db.QuizMarks
            .Where(q => q.StudentId == request.StudentId && 
                       q.SubjectId == request.SubjectId && 
                       q.Semester == request.Semester)
            .ToListAsync();

        var quizAverage = quizMarks.Any() 
            ? (double)quizMarks.Average(q => q.Score / q.MaxScore * 100)
            : 0;

        return Ok(new
        {
            message = "تم تسجيل علامة المذاكرة بنجاح",
            quizMark = new
            {
                quizMark.Id,
                quizMark.StudentId,
                StudentName = student.Name,
                quizMark.SubjectId,
                SubjectName = subject.Subject!.Name,
                quizMark.Semester,
                quizMark.QuizNumber,
                quizMark.Score,
                quizMark.MaxScore,
                Percentage = (double)(quizMark.Score * 100 / quizMark.MaxScore),
                quizMark.Date,
                quizMark.Notes,
                QuizAverage = Math.Round(quizAverage, 2)
            }
        });
    }

    [HttpGet("marks/quiz/{studentId:int}/{subjectId:int}/{semester:int}")]
    public async Task<IActionResult> GetQuizMarks(int studentId, int subjectId, int semester)
    {
        var schoolIds = await GetSchoolIdsAsync();
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.Id == studentId && schoolIds.Contains(s.SchoolId));
        
        if (student is null)
            return NotFound(new { message = "الطالب غير موجود" });

        var subject = await db.TeacherSubjects
            .AnyAsync(t => t.TeacherId == TeacherId && t.SubjectId == subjectId);
        
        if (!subject)
            return BadRequest(new { message = "هذه المادة ليست من موادك" });

        var quizMarks = await db.QuizMarks
            .Where(q => q.StudentId == studentId && 
                       q.SubjectId == subjectId && 
                       q.Semester == semester)
            .OrderBy(q => q.QuizNumber)
            .Select(q => new
            {
                q.Id,
                q.QuizNumber,
                q.Score,
                q.MaxScore,
                Percentage = Math.Round((double)q.Score / q.MaxScore * 100, 2),
                q.Date,
                q.Notes,
                q.CreatedAt
            })
            .ToListAsync();

        var average = quizMarks.Any() 
            ? Math.Round(quizMarks.Average(q => q.Percentage), 2) 
            : 0;

        return Ok(new
        {
            studentId = studentId,
            studentName = student.Name,
            subjectId = subjectId,
            semester = semester,
            quizMarks = quizMarks,
            average = average,
            totalQuizzes = quizMarks.Count
        });
    }

    // ============================================
    // العلامات (المدمجة) - باستخدام TeacherSubject
    // ============================================

    [HttpGet("marks")]
    public async Task<IActionResult> GetMarks(
        [FromQuery] int? subjectId, 
        [FromQuery] int? semester,
        [FromQuery] int? studentId)
    {
        var query = db.Marks
            .Include(m => m.Student)
            .Include(m => m.Subject)
            .Where(m => db.TeacherSubjects.Any(t => t.TeacherId == TeacherId && t.SubjectId == m.SubjectId));

        if (subjectId is not null) 
            query = query.Where(m => m.SubjectId == subjectId);
        
        if (semester is not null) 
            query = query.Where(m => m.Semester == semester);
        
        if (studentId is not null) 
            query = query.Where(m => m.StudentId == studentId);

        var marks = await query
            .OrderByDescending(m => m.UpdatedAt)
            .Select(m => new
            {
                m.Id,
                m.StudentId,
                StudentName = m.Student != null ? m.Student.Name : null,
                m.SubjectId,
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

        return Ok(marks);
    }

    [HttpGet("marks/student/{studentId:int}")]
    public async Task<IActionResult> GetStudentMarks(
        int studentId,
        [FromQuery] int? semester)
    {
        var schoolIds = await GetSchoolIdsAsync();
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.Id == studentId && schoolIds.Contains(s.SchoolId));
        
        if (student is null)
            return NotFound(new { message = "الطالب غير موجود" });

        var query = db.Marks
            .Include(m => m.Subject)
            .Where(m => m.StudentId == studentId && 
                       db.TeacherSubjects.Any(t => t.TeacherId == TeacherId && t.SubjectId == m.SubjectId));

        if (semester is not null) 
            query = query.Where(m => m.Semester == semester);

        var marks = await query
            .Select(m => new
            {
                m.Id,
                m.SubjectId,
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

        var average = marks.Any() 
            ? Math.Round(marks.Average(m => m.Total), 2) 
            : 0;

        var quizMarks = await db.QuizMarks
            .Where(q => q.StudentId == studentId && 
                       db.TeacherSubjects.Any(t => t.TeacherId == TeacherId && t.SubjectId == q.SubjectId))
            .ToListAsync();

        var quizAverage = quizMarks.Any() 
            ? Math.Round(quizMarks.Average(q => (double)q.Score / q.MaxScore * 100), 2) 
            : 0;

        return Ok(new
        {
            studentId = studentId,
            studentName = student.Name,
            semester = semester,
            marks = marks,
            average = average,
            quizAverage = quizAverage,
            totalQuizzes = quizMarks.Count
        });
    }

    // ============================================
    // تقارير الأداء
    // ============================================

    [HttpPost("performance-reports")]
    public async Task<IActionResult> CreatePerformanceReport(PerformanceReportRequest request)
    {
        var blocked = await rules.ValidateSecondPeriodAttendanceTakenAsync(TeacherId);
        if (blocked is not null) 
            return StatusCode(403, new { message = blocked });

        var subject = await db.TeacherSubjects
            .Include(t => t.Subject)
            .FirstOrDefaultAsync(t => t.TeacherId == TeacherId && t.SubjectId == request.SubjectId);
        
        if (subject is null) 
            return BadRequest(new { message = "هذه المادة ليست من موادك" });

        var schoolIds = await GetSchoolIdsAsync();
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && schoolIds.Contains(s.SchoolId));
        
        if (student is null) 
            return BadRequest(new { message = "الطالب غير موجود في مدارسك" });

        var report = new PerformanceReport
        {
            StudentId = request.StudentId,
            TeacherId = TeacherId,
            SubjectId = request.SubjectId,
            Semester = request.Semester,
            Behavior = request.Behavior ?? "",
            Notes = request.Notes ?? "",
            CreatedAt = DateTime.UtcNow
        };

        db.PerformanceReports.Add(report);
        await db.SaveChangesAsync();

        await notifier.SendAsync(student.Id, UserType.Student,
            "تقرير أداء جديد",
            $"تم إضافة تقرير أداء لك في مادة {subject.Subject!.Name}",
            "performance_report");

        return Created($"api/teacher/performance-reports/{report.Id}", report);
    }

    [HttpGet("performance-reports")]
    public async Task<IActionResult> GetPerformanceReports(
        [FromQuery] int? studentId,
        [FromQuery] int? subjectId)
    {
        var query = db.PerformanceReports
            .Include(r => r.Student)
            .Include(r => r.Subject)
            .Where(r => r.TeacherId == TeacherId);

        if (studentId is not null) 
            query = query.Where(r => r.StudentId == studentId);
        
        if (subjectId is not null) 
            query = query.Where(r => r.SubjectId == subjectId);

        var reports = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.StudentId,
                StudentName = r.Student != null ? r.Student.Name : null,
                r.SubjectId,
                SubjectName = r.Subject != null ? r.Subject.Name : null,
                r.Semester,
                r.Behavior,
                r.Notes,
                r.CreatedAt
            })
            .ToListAsync();

        return Ok(reports);
    }

    // ============================================
    // الشكاوى
    // ============================================

    [HttpPost("complaints")]
    public async Task<IActionResult> CreateComplaint(ComplaintRequest request)
    {
        var complaint = new Complaint
        {
            FromUserId = TeacherId,
            FromUserType = UserType.Employee,
            FromName = User.Identity?.Name ?? "",
            Against = request.Against,
            SchoolId = SchoolId,
            Content = request.Content,
            Status = ComplaintStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        var manager = await db.EmployeeSchools
            .Where(es => es.SchoolId == SchoolId && es.Role == EmployeeRole.Principal && es.IsActive)
            .Select(es => es.EmployeeId)
            .FirstOrDefaultAsync();

        if (manager != 0)
        {
            await notifier.SendAsync(manager, UserType.Employee,
                "شكوى جديدة من معلم",
                $"قام المعلم {User.Identity?.Name} بتقديم شكوى: {request.Content}",
                "complaint");
        }

        return Created($"api/teacher/complaints/{complaint.Id}", complaint);
    }

    [HttpGet("complaints")]
    public async Task<IActionResult> GetComplaints()
    {
        var complaints = await db.Complaints
            .Where(c => c.FromUserId == TeacherId && c.FromUserType == UserType.Employee)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Against,
                c.Content,
                c.Status,
                c.Resolution,
                c.CreatedAt
            })
            .ToListAsync();

        return Ok(complaints);
    }

    // ============================================
    // الملف الشخصي للمعلم (باستخدام LocalGradeNumber)
    // ============================================

    [HttpGet("full-profile")]
public async Task<IActionResult> GetFullProfile()
{
    var me = await db.Employees.FindAsync(TeacherId);
    if (me is null) 
        return NotFound();

    // ✅ الحصول على المدرسة الأساسية للمعلم من EmployeeSchool
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
        LocalEmployeeNumber = localEmployeeNumber,  // ✅ Local ID
        PrimarySchoolId = primarySchoolId,
        PrimarySchoolName = primarySchoolName,
        me.Phone,
        me.Address,
        me.BirthDate,
        me.Qualification,
        me.IsDismissed,
        me.CreatedAt
    };

    // المدارس التي يعمل بها المعلم
    var assignments = await db.TeacherAssignments
        .Where(t => t.EmployeeId == TeacherId)
        .Join(db.Schools, t => t.SchoolId, s => s.Id, (t, s) => new { SchoolId = s.Id, SchoolName = s.Name })
        .ToListAsync();

    var schools = new List<object>();
    foreach (var a in assignments)
    {
        // ✅ المواد التي يدرسها في هذه المدرسة (مع Local IDs)
        var subjects = await db.TeacherSubjects
            .Where(t => t.TeacherId == TeacherId && t.Subject!.SchoolId == a.SchoolId)
            .Include(t => t.Subject)
                .ThenInclude(s => s!.Grade)
            .Select(t => new
            {
                LocalSubjectId = t.Subject != null ? t.Subject.LocalSubjectId : 0,  // ✅ Local ID
                SubjectName = t.Subject != null ? t.Subject.Name : null,
                LocalGradeNumber = t.Subject != null && t.Subject.Grade != null ? t.Subject.Grade.LocalGradeNumber : 0,  // ✅ Local ID
                GradeName = t.Subject != null && t.Subject.Grade != null ? t.Subject.Grade.Name : null
            })
            .ToListAsync();

        // ✅ الشعب التي يدرس فيها (مع Local IDs)
        var sections = await db.TeacherGrades
            .Where(t => t.TeacherId == TeacherId && t.Section!.SchoolId == a.SchoolId)
            .Include(t => t.Section)
                .ThenInclude(s => s!.Grade)
            .Select(t => new
            {
                SectionName = t.Section != null ? t.Section.Name : null,
                LocalSectionNumber = t.Section != null ? t.Section.LocalSectionNumber : 0,  // ✅ Local ID
                GradeName = t.Section != null && t.Section.Grade != null ? t.Section.Grade.Name : null,
                LocalGradeNumber = t.Section != null && t.Section.Grade != null ? t.Section.Grade.LocalGradeNumber : 0  // ✅ Local ID
            })
            .Distinct()
            .ToListAsync();

        // ✅ LocalEmployeeNumber في هذه المدرسة
        var localEmpNumber = await db.EmployeeSchools
            .Where(es => es.EmployeeId == TeacherId && es.SchoolId == a.SchoolId && es.IsActive)
            .Select(es => es.LocalEmployeeNumber)
            .FirstOrDefaultAsync();

        var school = new
        {
            a.SchoolId,
            SchoolName = a.SchoolName,
            LocalEmployeeNumber = localEmpNumber,  // ✅ Local ID
            Subjects = subjects,
            Sections = sections
        };

        schools.Add(school);
    }

    // ✅ العلامات (مع Local IDs)
    var marks = await db.Marks
        .Where(m => db.TeacherSubjects.Any(t => t.TeacherId == TeacherId && t.SubjectId == m.SubjectId))
        .OrderByDescending(m => m.UpdatedAt).Take(500)
        .Select(m => new
        {
            m.Id,
            m.StudentId,
            StudentName = m.Student != null ? m.Student.Name : null,
            LocalStudentNumber = m.Student != null ? m.Student.LocalStudentNumber : 0,  // ✅ Local ID
            LocalSubjectId = m.Subject != null ? m.Subject.LocalSubjectId : 0,  // ✅ Local ID
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

    // ✅ حضور المعلم
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

    // ✅ الإجازات
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

    // ✅ تقارير الأداء (مع Local IDs)
    var perfReports = await db.PerformanceReports
        .Where(r => r.TeacherId == TeacherId)
        .Join(db.Subjects, r => r.SubjectId, s => s.Id, (r, s) => new { r, s })
        .OrderByDescending(x => x.r.CreatedAt)
        .Select(x => new
        {
            x.r.Id,
            x.r.StudentId,
            StudentName = x.r.Student != null ? x.r.Student.Name : null,
            LocalStudentNumber = x.r.Student != null ? x.r.Student.LocalStudentNumber : 0,  // ✅ Local ID
            SubjectName = x.s.Name,
            LocalSubjectId = x.s.LocalSubjectId,  // ✅ Local ID
            x.r.Semester,
            x.r.Behavior,
            x.r.Notes,
            x.r.CreatedAt
        })
        .ToListAsync();

    // ✅ الشكاوى
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

    // ✅ العقوبات
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

    // ✅ الإشعارات
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

    // ✅ الإحصائيات
    var statistics = new
    {
        TotalSchools = schools.Count,
        TotalSubjects = schools.Sum(s => ((dynamic)s).Subjects.Count),
        TotalSections = schools.Sum(s => ((dynamic)s).Sections.Count),
        TotalMarks = marks.Count,
        TotalAttendance = attendance.Count,
        TotalLeaves = leaves.Count,
        TotalPerformanceReports = perfReports.Count,
        TotalComplaints = complaints.Count,
        TotalPunishments = punishments.Count,
        TotalNotifications = notifications.Count
    };

    return Ok(new
    {
        success = true,
        message = "تم جلب الملف الكامل للمعلم بنجاح",
        data = new
        {
            Teacher = teacher,
            Statistics = statistics,
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
// إحصائيات المعلم (مع Local IDs)
// ============================================

[HttpGet("statistics")]
public async Task<IActionResult> GetStatistics()
{
    // ✅ جلب LocalEmployeeNumber
    var localEmployeeNumber = await db.EmployeeSchools
        .Where(es => es.EmployeeId == TeacherId && es.IsActive)
        .Select(es => es.LocalEmployeeNumber)
        .FirstOrDefaultAsync();

    var subjectsCount = await db.TeacherSubjects
        .CountAsync(t => t.TeacherId == TeacherId);

    var studentsCount = await db.Marks
        .Where(m => db.TeacherSubjects.Any(t => t.TeacherId == TeacherId && t.SubjectId == m.SubjectId))
        .Select(m => m.StudentId)
        .Distinct()
        .CountAsync();

    var marksCount = await db.Marks
        .CountAsync(m => db.TeacherSubjects.Any(t => t.TeacherId == TeacherId && t.SubjectId == m.SubjectId));

    var reportsCount = await db.PerformanceReports
        .CountAsync(r => r.TeacherId == TeacherId);

    var complaintsCount = await db.Complaints
        .CountAsync(c => c.FromUserId == TeacherId && c.FromUserType == UserType.Employee);

    // ✅ جلب المواد مع Local IDs
    var subjects = await db.TeacherSubjects
        .Where(t => t.TeacherId == TeacherId)
        .Include(t => t.Subject)
        .Select(t => new
        {
            LocalSubjectId = t.Subject != null ? t.Subject.LocalSubjectId : 0,
            SubjectName = t.Subject != null ? t.Subject.Name : null
        })
        .ToListAsync();

    return Ok(new
    {
        success = true,
        message = "تم جلب إحصائيات المعلم بنجاح",
        data = new
        {
            LocalEmployeeNumber = localEmployeeNumber,  // ✅ Local ID
            SubjectsCount = subjectsCount,
            StudentsCount = studentsCount,
            MarksCount = marksCount,
            ReportsCount = reportsCount,
            ComplaintsCount = complaintsCount,
            Subjects = subjects
        }
    });
}

    // ============================================
    // تقرير أداء الطالب
    // ============================================

    [HttpGet("student-performance/{studentId:int}")]
    public async Task<IActionResult> GetStudentPerformance(int studentId)
    {
        var schoolIds = await GetSchoolIdsAsync();
        var student = await db.Students
            .Include(s => s.Section)
            .ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(s => s.Id == studentId && schoolIds.Contains(s.SchoolId));
        
        if (student is null)
            return NotFound(new { message = "الطالب غير موجود" });

        // المواد التي يدرسها المعلم للطالب
        var subjects = await db.TeacherSubjects
            .Where(t => t.TeacherId == TeacherId)
            .Include(t => t.Subject)
            .Select(t => t.SubjectId)
            .ToListAsync();

        // العلامات
        var marks = await db.Marks
            .Where(m => m.StudentId == studentId && subjects.Contains(m.SubjectId))
            .Select(m => new
            {
                m.SubjectId,
                SubjectName = m.Subject!.Name,
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

        // علامات المذاكرات
        var quizMarks = await db.QuizMarks
            .Where(q => q.StudentId == studentId && subjects.Contains(q.SubjectId))
            .Select(q => new
            {
                q.SubjectId,
                SubjectName = q.Subject!.Name,
                q.QuizNumber,
                q.Score,
                q.MaxScore,
                Percentage = Math.Round((double)q.Score / q.MaxScore * 100, 2),
                q.Date,
                q.Notes
            })
            .ToListAsync();

        // تقارير الأداء
        var reports = await db.PerformanceReports
            .Where(r => r.StudentId == studentId && r.TeacherId == TeacherId)
            .Select(r => new
            {
                r.SubjectId,
                SubjectName = r.Subject!.Name,
                r.Semester,
                r.Behavior,
                r.Notes,
                r.CreatedAt
            })
            .ToListAsync();

        // الحضور
        var attendance = await db.StudentAttendances
            .Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.Date)
            .Take(50)
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

        // حساب المعدل العام
        var average = marks.Any() 
            ? Math.Round(marks.Average(m => m.Total), 2) 
            : 0;

        return Ok(new
        {
            student = new
            {
                student.Id,
                student.Name,
                student.Email,
                SectionName = student.Section?.Name,
                GradeName = student.Section?.Grade?.Name,
                LocalGradeNumber = student.Section?.Grade?.LocalGradeNumber
            },
            statistics = new
            {
                totalSubjects = subjects.Count,
                totalMarks = marks.Count,
                totalQuizMarks = quizMarks.Count,
                totalReports = reports.Count,
                average = average
            },
            attendance = new
            {
                total = totalAttendance,
                present = presentCount,
                absent = absentCount,
                percentage = attendancePercentage
            },
            marks = marks,
            quizMarks = quizMarks,
            reports = reports
        });
    }
    // ============================================
// Feed - الإعلانات والأنشطة مع Local IDs
// ============================================

[HttpGet("feed")]
public async Task<IActionResult> GetFeed()
{
    var now = DateTime.UtcNow;
    
    // ✅ الإعلانات مع Local IDs
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
            LocalAnnouncementId = a.LocalAnnouncementId,  // ✅ Local ID للإعلان
            a.Title,
            Description = a.Body,
            Date = a.CreatedAt.ToString("yyyy-MM-dd"),
            a.ExpiryDate,
            Type = "announcement"
        })
        .ToListAsync();

    // ✅ الأنشطة مع Local IDs
    var activities = await db.Activities
        .Where(a => a.SchoolId == SchoolId)
        .OrderByDescending(a => a.CreatedAt)
        .Select(a => new
        {
            a.Id,
            LocalActivityId = a.LocalActivityId,  // ✅ Local ID للنشاط
            Title = a.Name,
            Description = a.Description ?? a.Schedule ?? "",
            Date = a.CreatedAt.ToString("yyyy-MM-dd"),
            ExpiryDate = (DateTime?)null,
            Type = "activity"
        })
        .ToListAsync();

    // ✅ دمج الإعلانات والأنشطة في قائمة واحدة
    var allItems = new List<object>();
    allItems.AddRange(announcements);
    allItems.AddRange(activities);

    // ✅ ترتيب حسب التاريخ (الأحدث أولاً)
    var sortedFeed = allItems
        .OrderByDescending(x => DateTime.Parse(((dynamic)x).Date))
        .ToList();

    return Ok(new
    {
        success = true,
        message = "تم جلب البيانات بنجاح",
        data = new
        {
            announcements = announcements,  // ← مع Local IDs
            activities = activities,        // ← مع Local IDs
            feed = sortedFeed               // ← مع Local IDs
        }
    });
}

    // ============================================
    // الطلاب المعرضين للخطر
    // ============================================

    [HttpGet("at-risk-students")]
    public async Task<IActionResult> GetAtRiskStudents([FromQuery] decimal threshold = 50)
    {
        var schoolIds = await GetSchoolIdsAsync();
        
        // جلب جميع الطلاب في مدارس المعلم
        var students = await db.Students
            .Where(s => schoolIds.Contains(s.SchoolId) && s.IsActive)
            .ToListAsync();

        var atRiskStudents = new List<object>();

        foreach (var student in students)
        {
            // جلب مواد المعلم لهذا الطالب
            var subjectIds = await db.TeacherSubjects
                .Where(t => t.TeacherId == TeacherId)
                .Select(t => t.SubjectId)
                .ToListAsync();

            var marks = await db.Marks
                .Where(m => m.StudentId == student.Id && subjectIds.Contains(m.SubjectId))
                .ToListAsync();

            if (marks.Any())
            {
                var average = marks.Average(m => m.Total);
                
                if (average < threshold)
                {
                    // جلب آخر تقرير أداء
                    var lastReport = await db.PerformanceReports
                        .Where(r => r.StudentId == student.Id && r.TeacherId == TeacherId)
                        .OrderByDescending(r => r.CreatedAt)
                        .Select(r => new { r.Behavior, r.Notes, r.CreatedAt })
                        .FirstOrDefaultAsync();

                    atRiskStudents.Add(new
                    {
                        student.Id,
                        student.Name,
                        student.Email,
                        student.GuardianName,
                        student.GuardianPhone,
                        Average = Math.Round(average, 2),
                        Threshold = threshold,
                        MarksCount = marks.Count,
                        LastReport = lastReport
                    });
                }
            }
        }

        return Ok(new
        {
            threshold = threshold,
            totalAtRisk = atRiskStudents.Count,
            students = atRiskStudents.OrderBy(s => ((dynamic)s).Average).ToList()
        });
    }

    // ============================================
    // دوال مساعدة
    // ============================================

    private async Task<List<int>> GetSchoolIdsAsync() =>
        await db.TeacherAssignments
            .Where(t => t.EmployeeId == TeacherId)
            .Select(t => t.SchoolId)
            .ToListAsync();
}