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
    // المواد التي يدرسها المعلم (باستخدام Local IDs)
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

        return Ok(new
        {
            success = true,
            message = "تم جلب المواد بنجاح",
            data = result
        });
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

    // ============================================
    // حضور الطلاب (باستخدام Local IDs)
    // ============================================

    [HttpPost("attendance")]
    public async Task<IActionResult> TakeAttendance(StudentAttendanceLocalRequest request)
    {
        // ✅ البحث عن الشعبة باستخدام Local IDs
        var section = await db.Sections
            .Include(s => s.Grade)
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.Grade != null &&
                                      s.Grade.LocalGradeNumber == request.LocalGradeNumber &&
                                      s.LocalSectionNumber == request.LocalSectionNumber);

        if (section is null)
            return BadRequest(new { success = false, message = $"لا توجد شعبة برقم {request.LocalSectionNumber} في الصف {request.LocalGradeNumber}" });

        // ✅ التحقق من أن المعلم يدرس هذه الشعبة
        var teachesSection = await db.TeacherGrades
            .AnyAsync(tg => tg.TeacherId == TeacherId && tg.SectionId == section.Id);
        
        if (!teachesSection)
            return BadRequest(new { success = false, message = "أنت لا تدرس في هذه الشعبة" });

        // التحقق من وجود صورة جدول للمعلم
        var hasScheduleImage = await db.ScheduleImages
            .AnyAsync(s => s.SchoolId == SchoolId && 
                          s.TeacherId == TeacherId && 
                          s.Type == ScheduleImageType.Teacher);
        
        if (!hasScheduleImage)
            return BadRequest(new { success = false, message = "لم يتم رفع جدولك الدراسي بعد، يرجى التواصل مع الإدارة" });

        // ✅ تحويل الطلب إلى استخدام SectionId
        var attendanceRequest = new StudentAttendanceRequest
        {
            SectionId = section.Id,
            Date = DateOnly.FromDateTime(DateTime.Today),
            Entries = request.Entries.Select(e => new StudentAttendanceEntry
            {
                StudentId = e.LocalStudentNumber,
                Status = e.Status,
                Justification = e.Justification
            }).ToList()
        };

        return await AttendanceHelper.RecordAsync(db, attendanceRequest, TeacherId, this);
    }

    // ============================================
    // إدارة العلامات (باستخدام Local IDs)
    // ============================================

    [HttpPost("marks")]
    public async Task<IActionResult> UpsertMark(MarkLocalRequest request)
    {
        var blocked = await rules.ValidateSecondPeriodAttendanceTakenAsync(TeacherId);
        if (blocked is not null) 
            return StatusCode(403, new { message = blocked });

        // ✅ البحث عن المادة باستخدام LocalSubjectId
        var subject = await db.Subjects
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalSubjectId == request.LocalSubjectId);

        if (subject is null)
            return BadRequest(new { success = false, message = $"لا توجد مادة برقم {request.LocalSubjectId}" });

        // ✅ التحقق من أن المادة يدرسها المعلم
        var teacherSubject = await db.TeacherSubjects
            .FirstOrDefaultAsync(t => t.TeacherId == TeacherId && t.SubjectId == subject.Id);
        
        if (teacherSubject is null) 
            return BadRequest(new { success = false, message = "هذه المادة ليست من موادك" });

        // ✅ البحث عن الطالب باستخدام LocalStudentNumber
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalStudentNumber == request.LocalStudentNumber);
        
        if (student is null) 
            return BadRequest(new { success = false, message = $"لا يوجد طالب برقم {request.LocalStudentNumber}" });

        // التحقق من حدود العلامات
        var config = await db.MarkConfigs
            .FirstOrDefaultAsync(c => c.SchoolId == SchoolId)
            ?? new MarkConfig { SchoolId = SchoolId };

        if (request.Oral > config.MaxOral || request.Quiz1 > config.MaxQuiz1 ||
            request.Quiz2 > config.MaxQuiz2 || request.Homework > config.MaxHomework ||
            request.FinalExam > config.MaxFinalExam)
            return BadRequest(new { success = false, message = "علامة تتجاوز الحد الأعلى المضبوط للمدرسة" });

        // البحث عن علامة موجودة أو إنشاء جديدة
        var mark = await db.Marks
            .FirstOrDefaultAsync(m => m.StudentId == student.Id && 
                                      m.SubjectId == subject.Id && 
                                      m.Semester == request.Semester);
        
        if (mark is null)
        {
            mark = new Mark 
            { 
                StudentId = student.Id, 
                SubjectId = subject.Id, 
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
            $"رُصدت علامتك في {subject.Name} (الفصل {request.Semester}): {mark.Total}", 
            "mark");

        return Ok(new
        {
            success = true,
            message = "تم تسجيل العلامة بنجاح",
            data = new
            {
                mark.Id,
                StudentLocalNumber = student.LocalStudentNumber,
                StudentName = student.Name,
                LocalSubjectId = subject.LocalSubjectId,
                SubjectName = subject.Name,
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
    // علامات المذاكرات (باستخدام Local IDs)
    // ============================================

    [HttpPost("marks/quiz")]
    public async Task<IActionResult> AddQuizMark(QuizMarkLocalRequest request)
    {
        var blocked = await rules.ValidateSecondPeriodAttendanceTakenAsync(TeacherId);
        if (blocked is not null) 
            return StatusCode(403, new { message = blocked });

        // ✅ البحث عن المادة باستخدام LocalSubjectId
        var subject = await db.Subjects
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalSubjectId == request.LocalSubjectId);

        if (subject is null)
            return BadRequest(new { success = false, message = $"لا توجد مادة برقم {request.LocalSubjectId}" });

        // ✅ التحقق من أن المادة يدرسها المعلم
        var teacherSubject = await db.TeacherSubjects
            .FirstOrDefaultAsync(t => t.TeacherId == TeacherId && t.SubjectId == subject.Id);
    
        if (teacherSubject is null) 
            return BadRequest(new { success = false, message = "هذه المادة ليست من موادك" });

        // ✅ البحث عن الطالب باستخدام LocalStudentNumber
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalStudentNumber == request.LocalStudentNumber);
    
        if (student is null) 
            return BadRequest(new { success = false, message = $"لا يوجد طالب برقم {request.LocalStudentNumber}" });

        // التحقق من عدم وجود تكرار
        var existingQuiz = await db.QuizMarks
            .AnyAsync(q => q.StudentId == student.Id && 
                          q.SubjectId == subject.Id && 
                          q.Semester == request.Semester &&
                          q.QuizNumber == request.QuizNumber);
    
        if (existingQuiz)
            return BadRequest(new { success = false, message = "هذه المذاكرة مسجلة بالفعل لهذا الطالب" });

        // إنشاء سجل مذاكرة
        var quizMark = new QuizMark
        {
            StudentId = student.Id,
            SubjectId = subject.Id,
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
            .Where(q => q.StudentId == student.Id && 
                       q.SubjectId == subject.Id && 
                       q.Semester == request.Semester)
            .ToListAsync();

        var quizAverage = quizMarks.Any() 
            ? Math.Round(quizMarks.Average(q => (double)q.Score / q.MaxScore * 100), 2)
            : 0;

        return Ok(new
        {
            success = true,
            message = "تم تسجيل علامة المذاكرة بنجاح",
            data = new
            {
                quizMark.Id,
                StudentLocalNumber = student.LocalStudentNumber,
                StudentName = student.Name,
                LocalSubjectId = subject.LocalSubjectId,
                SubjectName = subject.Name,
                quizMark.Semester,
                quizMark.QuizNumber,
                quizMark.Score,
                quizMark.MaxScore,
                Percentage = Math.Round((double)quizMark.Score / quizMark.MaxScore * 100, 2),
                quizMark.Date,
                quizMark.Notes,
                QuizAverage = quizAverage
            }
        });
    }

    [HttpGet("marks/quiz/{localStudentNumber:int}/{localSubjectId:int}/{semester:int}")]
    public async Task<IActionResult> GetQuizMarks(int localStudentNumber, int localSubjectId, int semester)
    {
        // ✅ البحث عن الطالب
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalStudentNumber == localStudentNumber);
        
        if (student is null)
            return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber}" });

        // ✅ البحث عن المادة
        var subject = await db.Subjects
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalSubjectId == localSubjectId);

        if (subject is null)
            return NotFound(new { success = false, message = $"لا توجد مادة برقم {localSubjectId}" });

        // ✅ التحقق من أن المادة يدرسها المعلم
        var teacherSubject = await db.TeacherSubjects
            .AnyAsync(t => t.TeacherId == TeacherId && t.SubjectId == subject.Id);
        
        if (!teacherSubject)
            return BadRequest(new { success = false, message = "هذه المادة ليست من موادك" });

        var quizMarks = await db.QuizMarks
            .Where(q => q.StudentId == student.Id && 
                       q.SubjectId == subject.Id && 
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
            success = true,
            message = "تم جلب علامات المذاكرات بنجاح",
            data = new
            {
                StudentLocalNumber = localStudentNumber,
                StudentName = student.Name,
                LocalSubjectId = localSubjectId,
                SubjectName = subject.Name,
                semester = semester,
                QuizMarks = quizMarks,
                Average = average,
                TotalQuizzes = quizMarks.Count
            }
        });
    }

    // ============================================
    // العلامات (باستخدام Local IDs)
    // ============================================

    [HttpGet("marks")]
    public async Task<IActionResult> GetMarks(
        [FromQuery] int? localSubjectId, 
        [FromQuery] int? semester,
        [FromQuery] int? localStudentNumber)
    {
        var query = db.Marks
            .Include(m => m.Student)
            .Include(m => m.Subject)
            .Where(m => db.TeacherSubjects.Any(t => t.TeacherId == TeacherId && t.SubjectId == m.SubjectId));

        // ✅ فلترة حسب المادة باستخدام LocalSubjectId
        if (localSubjectId.HasValue)
        {
            var subject = await db.Subjects
                .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                          s.LocalSubjectId == localSubjectId.Value);
            if (subject is not null)
                query = query.Where(m => m.SubjectId == subject.Id);
        }
        
        if (semester is not null) 
            query = query.Where(m => m.Semester == semester);
        
        // ✅ فلترة حسب الطالب باستخدام LocalStudentNumber
        if (localStudentNumber is not null)
        {
            var student = await db.Students
                .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                          s.LocalStudentNumber == localStudentNumber.Value);
            if (student is not null)
                query = query.Where(m => m.StudentId == student.Id);
        }

        var marks = await query
            .OrderByDescending(m => m.UpdatedAt)
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

        return Ok(new
        {
            success = true,
            message = "تم جلب العلامات بنجاح",
            data = marks
        });
    }

    [HttpGet("marks/student/{localStudentNumber:int}")]
    public async Task<IActionResult> GetStudentMarks(
        int localStudentNumber,
        [FromQuery] int? semester)
    {
        // ✅ البحث عن الطالب
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalStudentNumber == localStudentNumber);
        
        if (student is null)
            return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber}" });

        var query = db.Marks
            .Include(m => m.Subject)
            .Where(m => m.StudentId == student.Id && 
                       db.TeacherSubjects.Any(t => t.TeacherId == TeacherId && t.SubjectId == m.SubjectId));

        if (semester is not null) 
            query = query.Where(m => m.Semester == semester);

        var marks = await query
            .Select(m => new
            {
                m.Id,
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

        var average = marks.Any() 
            ? Math.Round(marks.Average(m => m.Total), 2) 
            : 0;

        var quizMarks = await db.QuizMarks
            .Where(q => q.StudentId == student.Id && 
                       db.TeacherSubjects.Any(t => t.TeacherId == TeacherId && t.SubjectId == q.SubjectId))
            .ToListAsync();

        var quizAverage = quizMarks.Any() 
            ? Math.Round(quizMarks.Average(q => (double)q.Score / q.MaxScore * 100), 2) 
            : 0;

        return Ok(new
        {
            success = true,
            message = "تم جلب علامات الطالب بنجاح",
            data = new
            {
                StudentLocalNumber = localStudentNumber,
                StudentName = student.Name,
                semester = semester,
                Marks = marks,
                Average = average,
                QuizAverage = quizAverage,
                TotalQuizzes = quizMarks.Count
            }
        });
    }

    // ============================================
    // تقارير الأداء (باستخدام Local IDs)
    // ============================================

    [HttpPost("performance-reports")]
    public async Task<IActionResult> CreatePerformanceReport(PerformanceReportLocalRequest request)
    {
        var blocked = await rules.ValidateSecondPeriodAttendanceTakenAsync(TeacherId);
        if (blocked is not null) 
            return StatusCode(403, new { message = blocked });

        // ✅ البحث عن المادة باستخدام LocalSubjectId
        var subject = await db.Subjects
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalSubjectId == request.LocalSubjectId);

        if (subject is null)
            return BadRequest(new { success = false, message = $"لا توجد مادة برقم {request.LocalSubjectId}" });

        // ✅ التحقق من أن المادة يدرسها المعلم
        var teacherSubject = await db.TeacherSubjects
            .FirstOrDefaultAsync(t => t.TeacherId == TeacherId && t.SubjectId == subject.Id);
        
        if (teacherSubject is null) 
            return BadRequest(new { success = false, message = "هذه المادة ليست من موادك" });

        // ✅ البحث عن الطالب باستخدام LocalStudentNumber
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalStudentNumber == request.LocalStudentNumber);
        
        if (student is null) 
            return BadRequest(new { success = false, message = $"لا يوجد طالب برقم {request.LocalStudentNumber}" });

        var report = new PerformanceReport
        {
            StudentId = student.Id,
            TeacherId = TeacherId,
            SubjectId = subject.Id,
            Semester = request.Semester,
            Behavior = request.Behavior ?? "",
            Notes = request.Notes ?? "",
            CreatedAt = DateTime.UtcNow
        };

        db.PerformanceReports.Add(report);
        await db.SaveChangesAsync();

        await notifier.SendAsync(student.Id, UserType.Student,
            "تقرير أداء جديد",
            $"تم إضافة تقرير أداء لك في مادة {subject.Name}",
            "performance_report");

        return Created($"api/teacher/performance-reports/{report.Id}", new
        {
            success = true,
            message = "تم إضافة تقرير الأداء بنجاح",
            data = new
            {
                report.Id,
                StudentLocalNumber = student.LocalStudentNumber,
                StudentName = student.Name,
                LocalSubjectId = subject.LocalSubjectId,
                SubjectName = subject.Name,
                report.Semester,
                report.Behavior,
                report.Notes,
                report.CreatedAt
            }
        });
    }

    [HttpGet("performance-reports")]
    public async Task<IActionResult> GetPerformanceReports(
        [FromQuery] int? localStudentNumber,
        [FromQuery] int? localSubjectId)
    {
        var query = db.PerformanceReports
            .Include(r => r.Student)
            .Include(r => r.Subject)
            .Where(r => r.TeacherId == TeacherId);

        // ✅ فلترة حسب الطالب باستخدام LocalStudentNumber
        if (localStudentNumber.HasValue)
        {
            var student = await db.Students
                .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                          s.LocalStudentNumber == localStudentNumber.Value);
            if (student is not null)
                query = query.Where(r => r.StudentId == student.Id);
        }
        
        // ✅ فلترة حسب المادة باستخدام LocalSubjectId
        if (localSubjectId.HasValue)
        {
            var subject = await db.Subjects
                .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                          s.LocalSubjectId == localSubjectId.Value);
            if (subject is not null)
                query = query.Where(r => r.SubjectId == subject.Id);
        }

        var reports = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                StudentLocalNumber = r.Student != null ? r.Student.LocalStudentNumber : 0,
                StudentName = r.Student != null ? r.Student.Name : null,
                LocalSubjectId = r.Subject != null ? r.Subject.LocalSubjectId : 0,
                SubjectName = r.Subject != null ? r.Subject.Name : null,
                r.Semester,
                r.Behavior,
                r.Notes,
                r.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب تقارير الأداء بنجاح",
            data = reports
        });
    }

    // ============================================
    // الشكاوى (باستخدام Local IDs)
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

        return Created($"api/teacher/complaints/{complaint.Id}", new
        {
            success = true,
            message = "تم إنشاء الشكوى بنجاح",
            data = new
            {
                complaint.Id,
                complaint.Against,
                complaint.Content,
                complaint.Status,
                complaint.CreatedAt
            }
        });
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

        return Ok(new
        {
            success = true,
            message = "تم جلب شكواك بنجاح",
            data = complaints
        });
    }

    // ============================================
    // الملف الشخصي للمعلم (باستخدام Local IDs)
    // ============================================

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
            var subjects = await db.TeacherSubjects
                .Where(t => t.TeacherId == TeacherId && t.Subject!.SchoolId == a.SchoolId)
                .Include(t => t.Subject)
                    .ThenInclude(s => s!.Grade)
                .Select(t => new
                {
                    LocalSubjectId = t.Subject != null ? t.Subject.LocalSubjectId : 0,
                    SubjectName = t.Subject != null ? t.Subject.Name : null,
                    LocalGradeNumber = t.Subject != null && t.Subject.Grade != null ? t.Subject.Grade.LocalGradeNumber : 0,
                    GradeName = t.Subject != null && t.Subject.Grade != null ? t.Subject.Grade.Name : null
                })
                .ToListAsync();

            var sections = await db.TeacherGrades
                .Where(t => t.TeacherId == TeacherId && t.Section!.SchoolId == a.SchoolId)
                .Include(t => t.Section)
                    .ThenInclude(s => s!.Grade)
                .Select(t => new
                {
                    SectionName = t.Section != null ? t.Section.Name : null,
                    LocalSectionNumber = t.Section != null ? t.Section.LocalSectionNumber : 0,
                    GradeName = t.Section != null && t.Section.Grade != null ? t.Section.Grade.Name : null,
                    LocalGradeNumber = t.Section != null && t.Section.Grade != null ? t.Section.Grade.LocalGradeNumber : 0
                })
                .Distinct()
                .ToListAsync();

            var localEmpNumber = await db.EmployeeSchools
                .Where(es => es.EmployeeId == TeacherId && es.SchoolId == a.SchoolId && es.IsActive)
                .Select(es => es.LocalEmployeeNumber)
                .FirstOrDefaultAsync();

            var school = new
            {
                a.SchoolId,
                SchoolName = a.SchoolName,
                LocalEmployeeNumber = localEmpNumber,
                Subjects = subjects,
                Sections = sections
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
                LocalEmployeeNumber = localEmployeeNumber,
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
    // تقرير أداء الطالب (باستخدام Local IDs)
    // ============================================

    [HttpGet("student-performance/{localStudentNumber:int}")]
    public async Task<IActionResult> GetStudentPerformance(int localStudentNumber)
    {
        var student = await db.Students
            .Include(s => s.Section)
                .ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalStudentNumber == localStudentNumber);
        
        if (student is null)
            return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber}" });

        var subjects = await db.TeacherSubjects
            .Where(t => t.TeacherId == TeacherId)
            .Include(t => t.Subject)
            .Select(t => t.SubjectId)
            .ToListAsync();

        var marks = await db.Marks
            .Where(m => m.StudentId == student.Id && subjects.Contains(m.SubjectId))
            .Select(m => new
            {
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

        var quizMarks = await db.QuizMarks
            .Where(q => q.StudentId == student.Id && subjects.Contains(q.SubjectId))
            .Select(q => new
            {
                LocalSubjectId = q.Subject != null ? q.Subject.LocalSubjectId : 0,
                SubjectName = q.Subject != null ? q.Subject.Name : null,
                q.QuizNumber,
                q.Score,
                q.MaxScore,
                Percentage = Math.Round((double)q.Score / q.MaxScore * 100, 2),
                q.Date,
                q.Notes
            })
            .ToListAsync();

        var reports = await db.PerformanceReports
            .Where(r => r.StudentId == student.Id && r.TeacherId == TeacherId)
            .Select(r => new
            {
                LocalSubjectId = r.Subject != null ? r.Subject.LocalSubjectId : 0,
                SubjectName = r.Subject != null ? r.Subject.Name : null,
                r.Semester,
                r.Behavior,
                r.Notes,
                r.CreatedAt
            })
            .ToListAsync();

        var attendance = await db.StudentAttendances
            .Where(a => a.StudentId == student.Id)
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

        var average = marks.Any() 
            ? Math.Round(marks.Average(m => m.Total), 2) 
            : 0;

        return Ok(new
        {
            success = true,
            message = "تم جلب أداء الطالب بنجاح",
            data = new
            {
                student = new
                {
                    student.Id,
                    student.Name,
                    student.Email,
                    student.LocalStudentNumber,
                    SectionName = student.Section?.Name,
                    LocalSectionNumber = student.Section?.LocalSectionNumber ?? 0,
                    GradeName = student.Section?.Grade?.Name,
                    LocalGradeNumber = student.Section?.Grade?.LocalGradeNumber ?? 0
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
            }
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
                        a.Audience == AnnouncementAudience.Students ||
                        a.Audience == AnnouncementAudience.Teachers) &&
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
    // الطلاب المعرضين للخطر (باستخدام Local IDs)
    // ============================================

    [HttpGet("at-risk-students")]
    public async Task<IActionResult> GetAtRiskStudents([FromQuery] decimal threshold = 50)
    {
        var students = await db.Students
            .Where(s => s.SchoolId == SchoolId && s.IsActive)
            .ToListAsync();

        var atRiskStudents = new List<object>();

        foreach (var student in students)
        {
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
                        student.LocalStudentNumber,
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
            success = true,
            message = "تم جلب الطلاب المعرضين للخطر بنجاح",
            data = new
            {
                Threshold = threshold,
                TotalAtRisk = atRiskStudents.Count,
                Students = atRiskStudents.OrderBy(s => ((dynamic)s).Average).ToList()
            }
        });
    }
}