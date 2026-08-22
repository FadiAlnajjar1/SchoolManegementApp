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
[Route("api/student")]
[Authorize(Roles = Roles.Student)]
public class StudentController(AppDbContext db, NotificationService notifier) : ControllerBase
{
    private int StudentId => User.GetUserId();
    private int SchoolId => User.GetSchoolId();

    private async Task<Student?> MeAsync() => await db.Students.FindAsync(StudentId);

    // ============================================
    // صورة جدول الشعبة
    // ============================================

    [HttpGet("schedule-image")]
    public async Task<IActionResult> GetScheduleImage()
    {
        var me = await MeAsync();
        if (me?.SectionId is null)
            return NotFound(new { success = false, message = "أنت غير مسجل في أي شعبة" });

        var section = await db.Sections
            .Include(s => s.Grade)
            .FirstOrDefaultAsync(s => s.Id == me.SectionId && s.SchoolId == SchoolId);

        if (section is null)
            return NotFound(new { success = false, message = "الشعبة غير موجودة" });

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
                LocalGradeNumber = section.Grade != null ? section.Grade.LocalGradeNumber : 0,
                GradeName = section.Grade != null ? section.Grade.Name : null,
                LocalSectionNumber = section.LocalSectionNumber,
                SectionName = section.Name
            })
            .FirstOrDefaultAsync();

        if (image is null)
            return NotFound(new { success = false, message = "لا توجد صورة جدول لشعبتك" });

        return Ok(new
        {
            success = true,
            message = "تم جلب صورة الجدول بنجاح",
            data = image
        });
    }

    // ============================================
    // المواد الدراسية
    // ============================================

    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects()
    {
        var me = await MeAsync();
        if (me?.SectionId is null) 
            return Ok(new
            {
                success = true,
                message = "أنت غير مسجل في أي شعبة",
                data = new StudentSubjectsResponse
                {
                    Message = "أنت غير مسجل في أي شعبة",
                    Subjects = new List<StudentSubjectDto>()
                }
            });

        var sectionData = await db.Sections
            .Include(s => s.Grade)
            .Where(s => s.Id == me.SectionId && s.SchoolId == SchoolId)
            .Select(s => new
            {
                LocalSectionNumber = s.LocalSectionNumber,
                SectionName = s.Name,
                LocalGradeNumber = s.Grade != null ? s.Grade.LocalGradeNumber : 0,
                GradeName = s.Grade != null ? s.Grade.Name : "غير معروف",
                AcademicYear = s.Grade != null 
            })
            .FirstOrDefaultAsync();

        if (sectionData is null) 
            return Ok(new
            {
                success = true,
                message = "الشعبة غير موجودة",
                data = new StudentSubjectsResponse
                {
                    Message = "الشعبة غير موجودة",
                    Subjects = new List<StudentSubjectDto>()
                }
            });

        var subjects = await db.Subjects
            .Where(s => s.SchoolId == SchoolId && 
                        s.Grade != null && 
                        s.Grade.LocalGradeNumber == sectionData.LocalGradeNumber)
            .Select(s => new StudentSubjectDto
            {
                LocalSubjectId = s.LocalSubjectId,
                Name = s.Name,
                TeacherName = s.Teacher != null ? s.Teacher.Name : null,
                LocalTeacherNumber = db.EmployeeSchools
                    .Where(es => es.EmployeeId == s.TeacherId && 
                                 es.SchoolId == SchoolId && 
                                 es.IsActive)
                    .Select(es => (int?)es.LocalEmployeeNumber)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب المواد الدراسية بنجاح",
            data = new StudentSubjectsResponse
            {
                LocalSectionNumber = sectionData.LocalSectionNumber,
                SectionName = sectionData.SectionName,
                LocalGradeNumber = sectionData.LocalGradeNumber,
                GradeName = sectionData.GradeName,
                Subjects = subjects,
                TotalSubjects = subjects.Count
            }
        });
    }

    // ============================================
    // العلامات - مع فلترة حسب السنة
    // ============================================

    [HttpGet("marks")]
    public async Task<IActionResult> GetMarks([FromQuery] int? semester, [FromQuery] int? academicYear = null)
    {
        var query = db.Marks
            .Where(m => m.StudentId == StudentId);

        if (semester is not null) 
            query = query.Where(m => m.Semester == semester);

        // ✅ إضافة فلترة حسب السنة (اختياري)
        if (academicYear.HasValue)
            query = query.Where(m => m.AcademicYear == academicYear.Value);

        var marks = await query
            .OrderByDescending(m => m.AcademicYear)
            .ThenBy(m => m.Semester)
            .Select(m => new
            {
                SubjectName = db.Subjects
                    .Where(s => s.Id == m.SubjectId)
                    .Select(s => s.Name)
                    .FirstOrDefault() ?? "غير معروف",
                LocalSubjectId = db.Subjects
                    .Where(s => s.Id == m.SubjectId)
                    .Select(s => s.LocalSubjectId)
                    .FirstOrDefault(),
                LocalTeacherNumber = db.EmployeeSchools
                    .Where(es => es.EmployeeId == db.Subjects
                        .Where(s => s.Id == m.SubjectId)
                        .Select(s => s.TeacherId)
                        .FirstOrDefault() && 
                                 es.SchoolId == SchoolId && 
                                 es.IsActive)
                    .Select(es => (int?)es.LocalEmployeeNumber)
                    .FirstOrDefault(),
                TeacherName = db.Subjects
                    .Where(s => s.Id == m.SubjectId)
                    .Select(s => s.Teacher != null ? s.Teacher.Name : null)
                    .FirstOrDefault(),
                m.Semester,
                m.AcademicYear, // ✅ إضافة السنة
                m.Oral,
                m.Quiz1,
                m.Quiz2,
                m.Homework,
                m.FinalExam,
                m.Total,
                m.UpdatedAt
            })
            .ToListAsync();

        // ✅ تجميع العلامات حسب السنة
        var groupedByYear = marks
            .GroupBy(m => m.AcademicYear)
            .Select(g => new
            {
                AcademicYear = g.Key,
                Semester1Marks = g.Where(m => m.Semester == 1).ToList(),
                Semester2Marks = g.Where(m => m.Semester == 2).ToList(),
                Semester1Average = g.Where(m => m.Semester == 1).Any() 
                    ? Math.Round(g.Where(m => m.Semester == 1).Average(m => m.Total), 2) 
                    : 0,
                Semester2Average = g.Where(m => m.Semester == 2).Any() 
                    ? Math.Round(g.Where(m => m.Semester == 2).Average(m => m.Total), 2) 
                    : 0,
                YearAverage = Math.Round(g.Average(m => m.Total), 2)
            })
            .OrderByDescending(g => g.AcademicYear)
            .ToList();

        return Ok(new
        {
            success = true,
            message = "تم جلب العلامات بنجاح",
            data = new
            {
                Marks = marks,
                GroupedByYear = groupedByYear,
                TotalMarks = marks.Count,
                CurrentYear = DateTime.Now.Year
            }
        });
    }

    // ============================================
    // بطاقات التقارير
    // ============================================

    [HttpGet("report-cards")]
    public async Task<IActionResult> GetReportCards()
    {
        var reportCards = await db.ReportCards
            .Include(r => r.Subjects)
            .Where(r => r.StudentId == StudentId)
            .OrderByDescending(r => r.Year)
            .ThenByDescending(r => r.Semester)
            .Select(r => new
            {
                r.Id,
                r.Semester,
                r.Year,
                r.Average,
                r.Rank,
                r.Passed,
                Subjects = r.Subjects.Select(s => new
                {
                    s.SubjectName,
                    LocalSubjectId = db.Subjects
                        .Where(sub => sub.Name == s.SubjectName && sub.SchoolId == SchoolId)
                        .Select(sub => sub.LocalSubjectId)
                        .FirstOrDefault(),
                    s.Total,
                }).ToList()
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب بطاقات التقارير بنجاح",
            data = reportCards
        });
    }

    // ============================================
    // الحضور والغياب
    // ============================================

   [HttpGet("attendance")]
public async Task<IActionResult> GetAttendance()
{
    var attendance = await db.StudentAttendances
        .Where(a => a.StudentId == StudentId && !a.IsDeleted)  // ✅ إضافة الشرط
        .OrderByDescending(a => a.Date)
        .Take(200)
        .Select(a => new
        {
            a.Date,
            a.Status,
            StatusName = a.Status.ToString(),
            a.SectionId,
            LocalSectionNumber = db.Sections
                .Where(s => s.Id == a.SectionId)
                .Select(s => s.LocalSectionNumber)
                .FirstOrDefault()
        })
        .ToListAsync();

    return Ok(new
    {
        success = true,
        message = "تم جلب سجل الحضور بنجاح",
        data = attendance
    });
}
    // ============================================
    // Feed - الإعلانات والأنشطة
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
    // الشكاوى
    // ============================================

    [HttpPost("complaints")]
    public async Task<IActionResult> CreateComplaint(ComplaintRequest request)
    {
        var me = await MeAsync();
        if (me is null) return NotFound();

        var complaint = new Complaint
        {
            FromUserId = StudentId,
            FromUserType = UserType.Student,
            FromName = me.Name,
            Against = request.Against,
            SchoolId = SchoolId,
            Content = request.Content,
            CreatedAt = DateTime.UtcNow
        };

        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        return Created($"api/student/complaints/{complaint.Id}", new
        {
            success = true,
            message = "تم إنشاء الشكوى بنجاح",
            data = new
            {
                complaint.Id,
                complaint.FromUserId,
                complaint.FromName,
                complaint.Against,
                complaint.Content,
                complaint.Status,
                complaint.CreatedAt
            }
        });
    }

    [HttpGet("complaints")]
    public async Task<IActionResult> GetMyComplaints()
    {
        var complaints = await db.Complaints
            .Where(c => c.FromUserId == StudentId && c.FromUserType == UserType.Student)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Against,
                c.Content,
                c.Status,
                StatusName = c.Status.ToString(),
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
    // الأنشطة
    // ============================================

    [HttpGet("activities")]
    public async Task<IActionResult> GetActivities()
    {
        var activities = await db.Activities
            .Where(a => a.SchoolId == SchoolId)
            .Select(a => new
            {
                a.Id,
                LocalActivityId = a.LocalActivityId,
                a.Title,
                a.Description,
                RegisteredCount = db.ActivityRegistrations
                    .Count(r => r.ActivityId == a.Id && r.Status == RegistrationStatus.Approved),
                IsRegistered = db.ActivityRegistrations
                    .Any(r => r.ActivityId == a.Id && r.StudentId == StudentId),
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب الأنشطة بنجاح",
            data = activities
        });
    }

    [HttpPost("activities/{activityId:int}/register")]
    public async Task<IActionResult> RegisterActivity(int activityId)
    {
        var activity = await db.Activities
            .FirstOrDefaultAsync(a => a.SchoolId == SchoolId && 
                                      a.Id == activityId);
        
        if (activity is null) 
            return NotFound(new { success = false, message = "النشاط غير موجود" });

        var existingRegistration = await db.ActivityRegistrations
            .FirstOrDefaultAsync(r => r.ActivityId == activity.Id && r.StudentId == StudentId);

        if (existingRegistration is not null)
        {
            if (existingRegistration.Status == RegistrationStatus.Approved)
                return BadRequest(new { success = false, message = "أنت مسجل في هذا النشاط بالفعل" });
            if (existingRegistration.Status == RegistrationStatus.Pending)
                return BadRequest(new { success = false, message = "طلب التسجيل قيد المراجعة" });
        }

        var approved = await db.ActivityRegistrations
            .CountAsync(r => r.ActivityId == activity.Id && r.Status == RegistrationStatus.Approved);

        var registration = new ActivityRegistration 
        { 
            ActivityId = activity.Id, 
            StudentId = StudentId,
            Status = RegistrationStatus.Pending,
            RejectionReason = string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        db.ActivityRegistrations.Add(registration);
        await db.SaveChangesAsync();

        return Created($"api/student/activities/{activityId}/register", new
        {
            success = true,
            message = "تم التسجيل في النشاط بنجاح",
            data = new
            {
                registration.Id,
                ActivityId = activityId,
                registration.StudentId,
                registration.Status,
                StatusName = registration.Status.ToString(),
                registration.CreatedAt
            }
        });
    }

    [HttpGet("activities/registrations")]
    public async Task<IActionResult> GetMyRegistrations()
    {
        var registrations = await db.ActivityRegistrations
            .Include(r => r.Activity)
            .Where(r => r.StudentId == StudentId)
            .Select(r => new
            {
                r.Id,
                r.ActivityId,
                ActivityLocalId = r.Activity != null ? r.Activity.LocalActivityId : 0,
                ActivityName = r.Activity != null ? r.Activity.Title : "غير معروف",
                ActivityDescription = r.Activity != null ? r.Activity.Description : "غير معروف",
                r.Status,
                StatusName = r.Status.ToString(),
                r.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب تسجيلاتك في الأنشطة بنجاح",
            data = registrations
        });
    }

    [HttpDelete("activities/registrations/{activityId:int}")]
    public async Task<IActionResult> CancelRegistration(int activityId)
    {
        var studentId = User.GetUserId();
        
        if (studentId <= 0)
            return Unauthorized(new { success = false, message = "الطالب غير مسجل الدخول" });

        var student = await db.Students
            .FirstOrDefaultAsync(s => s.Id == studentId && 
                                      s.SchoolId == SchoolId &&
                                      s.IsActive);

        if (student is null)
            return NotFound(new { success = false, message = "الطالب غير موجود في هذه المدرسة" });

        var activity = await db.Activities
            .FirstOrDefaultAsync(a => a.SchoolId == SchoolId && 
                                      a.Id == activityId);

        if (activity is null)
            return NotFound(new { success = false, message = $"لا يوجد نشاط بهذا المعرف" });

        var registration = await db.ActivityRegistrations
            .Include(r => r.Activity)
            .FirstOrDefaultAsync(r => r.ActivityId == activity.Id && 
                                      r.StudentId == student.Id);

        if (registration is null)
            return NotFound(new { success = false, message = "أنت غير مسجل في هذا النشاط" });

        if (registration.Status == RegistrationStatus.Approved)
            return BadRequest(new { 
                success = false, 
                message = "لا يمكن إلغاء تسجيل تمت الموافقة عليه، راجع مشرف النشاطات" 
            });

        db.ActivityRegistrations.Remove(registration);
        await db.SaveChangesAsync();

        await notifier.SendAsync(
            student.Id,
            UserType.Student,
            "تم إلغاء التسجيل في النشاط",
            $"تم إلغاء تسجيلك في النشاط \"{activity.Title}\"",
            "activity"
        );

        return Ok(new
        {
            success = true,
            message = "تم إلغاء التسجيل بنجاح",
            data = new
            {
                ActivityId = activityId,
                ActivityName = activity.Title,
                StudentLocalNumber = student.LocalStudentNumber,
                StudentName = student.Name
            }
        });
    }

    // ============================================
    // المكتبة - الطالب يحجز الكتاب
    // ============================================

    [HttpGet("library/books")]
    public async Task<IActionResult> GetBooks()
    {
        var books = await db.Books
            .Where(b => b.SchoolId == SchoolId)
            .Select(b => new
            {
                LocalBookNumber = b.LocalBookNumber,
                b.Title,
                b.Author,
                b.Copies,
                b.AvailableCopies,
                b.ReservedCopies,
                AvailableForLoan = b.AvailableCopies - b.ReservedCopies,
                IsAvailable = (b.AvailableCopies - b.ReservedCopies) > 0
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب الكتب بنجاح",
            data = books
        });
    }

    [HttpGet("library/books/{localBookNumber:int}")]
    public async Task<IActionResult> GetBook(int localBookNumber)
    {
        var book = await db.Books
            .Where(b => b.SchoolId == SchoolId && 
                        b.LocalBookNumber == localBookNumber)
            .Select(b => new
            {
                LocalBookNumber = b.LocalBookNumber,
                b.Title,
                b.Author,
                b.Copies,
                b.AvailableCopies,
                b.ReservedCopies,
                AvailableForLoan = b.AvailableCopies - b.ReservedCopies,
                IsAvailable = (b.AvailableCopies - b.ReservedCopies) > 0,
                b.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (book is null)
            return NotFound(new { success = false, message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" });

        return Ok(new
        {
            success = true,
            message = "تم جلب الكتاب بنجاح",
            data = book
        });
    }

    [HttpPost("library/books/{localBookNumber:int}/reserve")]
    public async Task<IActionResult> ReserveBook(int localBookNumber)
    {
        var studentId = User.GetUserId();
        
        if (studentId <= 0)
            return Unauthorized(new { success = false, message = "الطالب غير مسجل الدخول" });

        var book = await db.Books
            .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                      b.LocalBookNumber == localBookNumber);
        
        if (book is null) 
            return NotFound(new { success = false, message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" });

        var availableForLoan = book.AvailableCopies - book.ReservedCopies;
        if (availableForLoan <= 0)
            return BadRequest(new { 
                success = false, 
                message = "لا توجد نسخ متاحة من هذا الكتاب حالياً" 
            });

        var activeLoan = await db.BookLoans
            .AnyAsync(l => l.BookId == book.Id && 
                          l.StudentId == studentId && 
                          l.Status == LoanStatus.Active);

        if (activeLoan)
            return BadRequest(new { 
                success = false, 
                message = "الكتاب مستعار من قبلك بالفعل" 
            });

        var existingReservation = await db.BookReservations
            .FirstOrDefaultAsync(r => r.BookId == book.Id && 
                                      r.StudentId == studentId);

        if (existingReservation is not null)
        {
            if (existingReservation.Status == ReservationStatus.Pending)
            {
                return BadRequest(new { 
                    success = false, 
                    message = "لديك طلب حجز معلق على هذا الكتاب، يرجى الانتظار حتى يتم الرد عليه" 
                });
            }
            
            if (existingReservation.Status == ReservationStatus.Approved)
            {
                return BadRequest(new { 
                    success = false, 
                    message = "تمت الموافقة على حجزك لهذا الكتاب مسبقاً، يمكنك استعارته" 
                });
            }

            db.BookReservations.Remove(existingReservation);
            await db.SaveChangesAsync();
        }

        var reservation = new BookReservation
        {
            BookId = book.Id,
            StudentId = studentId,
            Date = DateOnly.FromDateTime(DateTime.Today),
            ExpiryDate = null,
            Status = ReservationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.BookReservations.Add(reservation);
        await db.SaveChangesAsync();

        var studentName = await db.Students
            .Where(s => s.Id == studentId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync() ?? "طالب";

        await notifier.SendToLibrarianAsync(
            SchoolId,
            "طلب حجز كتاب جديد",
            $"الطالب {studentName} يطلب حجز كتاب \"{book.Title}\"",
            "reservation_request");

        return Created($"api/student/library/reservations/{reservation.Id}", new
        {
            success = true,
            message = "تم إرسال طلب حجز الكتاب بنجاح، في انتظار موافقة أمين المكتبة",
            data = new
            {
                reservation.Id,
                LocalBookNumber = book.LocalBookNumber,
                BookTitle = book.Title,
                BookAuthor = book.Author,
                reservation.Date,
                reservation.ExpiryDate,
                reservation.Status,
                StatusName = "Pending",
                StatusArabic = "قيد الانتظار",
                reservation.CreatedAt,
                AvailableCopies = book.AvailableCopies,
                ReservedCopies = book.ReservedCopies,
                AvailableForLoan = book.AvailableCopies - book.ReservedCopies
            }
        });
    }

    [HttpDelete("library/books/{localBookNumber:int}/reserve")]
    public async Task<IActionResult> CancelReservation(int localBookNumber)
    {
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                      b.LocalBookNumber == localBookNumber);
        
        if (book is null) 
            return NotFound(new { success = false, message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" });

        var reservation = await db.BookReservations
            .FirstOrDefaultAsync(r => r.BookId == book.Id && 
                                      r.StudentId == StudentId && 
                                      (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Approved));

        if (reservation is null)
            return NotFound(new { 
                success = false, 
                message = "لا يوجد حجز نشط لك على هذا الكتاب" 
            });

        if (reservation.Status == ReservationStatus.Approved)
        {
            book.ReservedCopies--;
        }

        reservation.Status = ReservationStatus.Cancelled;
        reservation.RejectionReason = "تم الإلغاء من قبل الطالب";
        reservation.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "تم إلغاء حجز الكتاب بنجاح",
            data = new
            {
                LocalBookNumber = book.LocalBookNumber,
                BookTitle = book.Title,
                reservation.Status,
                StatusName = "Cancelled",
                StatusArabic = "ملغي",
                AvailableCopies = book.AvailableCopies,
                ReservedCopies = book.ReservedCopies,
                AvailableForLoan = book.AvailableCopies - book.ReservedCopies
            }
        });
    }

    [HttpGet("library/reservations")]
    public async Task<IActionResult> GetMyReservations()
    {
        var reservations = await db.BookReservations
            .Include(r => r.Book)
            .Where(r => r.StudentId == StudentId)
            .OrderByDescending(r => r.Date)
            .Select(r => new
            {
                r.Id,
                BookTitle = r.Book != null ? r.Book.Title : "غير معروف",
                LocalBookNumber = r.Book != null ? r.Book.LocalBookNumber : 0,
                r.Date,
                r.ExpiryDate,
                r.Status,
                StatusName = r.Status.ToString(),
                StatusArabic = r.Status == ReservationStatus.Pending ? "قيد الانتظار" :
                              r.Status == ReservationStatus.Approved ? "تمت الموافقة (لم تستعار بعد)" :
                              r.Status == ReservationStatus.Rejected ? "مرفوض" :
                              r.Status == ReservationStatus.Fulfilled ? "تم الاستعارة" :
                              r.Status == ReservationStatus.Cancelled ? "ملغي" : "غير معروف",
                r.RejectionReason,
                r.CreatedAt,
                r.UpdatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب حجوزاتك بنجاح",
            data = new
            {
                TotalReservations = reservations.Count,
                PendingReservations = reservations.Count(r => r.Status == ReservationStatus.Pending),
                ApprovedReservations = reservations.Count(r => r.Status == ReservationStatus.Approved),
                Reservations = reservations
            }
        });
    }

    [HttpGet("library/loans")]
    public async Task<IActionResult> GetMyLoans()
    {
        var loans = await db.BookLoans
            .Where(l => l.StudentId == StudentId)
            .OrderByDescending(l => l.date)
            .Select(l => new
            {
                LocalLoanNumber = l.LocalLoanNumber,
                BookTitle = l.Book != null ? l.Book.Title : "غير معروف",
                LocalBookNumber = l.Book != null ? l.Book.LocalBookNumber : 0,
                l.date,
                l.expiryDate,
                l.ReturnDate,
                Status = l.Status.ToString(),
                IsOverdue = l.expiryDate < DateOnly.FromDateTime(DateTime.Today) && l.Status == LoanStatus.Active
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب استعاراتك بنجاح",
            data = new
            {
                Loans = loans,
                TotalLoans = loans.Count,
                ActiveLoans = loans.Count(l => l.Status == "Active")
            }
        });
    }

    // ============================================
    // الملف الشخصي الكامل
    // ============================================

    [HttpGet("full-profile")]
public async Task<IActionResult> GetFullProfile()
{
    var me = await db.Students
        .Include(s => s.Section)
            .ThenInclude(sec => sec!.Grade)
        .FirstOrDefaultAsync(s => s.Id == StudentId);

    if (me is null) return NotFound();

    var studentInfo = new
    {
        me.Name,
        me.Email,
        LocalStudentNumber = me.LocalStudentNumber,
        SectionName = me.Section?.Name,
        LocalSectionNumber = me.Section?.LocalSectionNumber ?? 0,
        GradeName = me.Section?.Grade?.Name,
        LocalGradeNumber = me.Section?.Grade?.LocalGradeNumber ?? 0,
        AcademicYear = me.Section?.Grade != null ? new
        {
            me.Section.Grade.Id,
            me.Section.Grade.Name,
            me.Section.Grade.Level,
            me.Section.Grade.LocalGradeNumber
        } : null,
        me.GuardianName,
        me.GuardianPhone,
        me.BloodType,
        me.ChronicDiseases,
        me.Allergies,
        me.HealthNotes,
        me.BirthDate,
        me.Address,
        me.DismissalWarning,
        me.IsPhoneVerified,
        me.CreatedAt
    };

    var subjects = new List<SubjectDto>();
    if (me.SectionId is not null && me.Section?.Grade != null)
    {
        var subjectList = await db.Subjects
            .Where(s => s.SchoolId == SchoolId && 
                        s.Grade != null && 
                        s.Grade.LocalGradeNumber == me.Section.Grade.LocalGradeNumber)
            .Select(s => new SubjectDto
            {
                LocalSubjectId = s.LocalSubjectId,
                SubjectName = s.Name,
            })
            .ToListAsync();
        
        subjects.AddRange(subjectList);
    }

    var marks = await db.Marks
        .Where(m => m.StudentId == StudentId)
        .Select(m => new 
        {
            m.Id,
            m.Semester,
            m.AcademicYear,
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
            TeacherName = m.Subject != null && m.Subject.Teacher != null ? m.Subject.Teacher.Name : null,
            LocalTeacherNumber = db.EmployeeSchools
                .Where(es => es.EmployeeId == (m.Subject != null ? m.Subject.TeacherId : 0) && 
                             es.SchoolId == SchoolId && 
                             es.IsActive)
                .Select(es => (int?)es.LocalEmployeeNumber)
                .FirstOrDefault()
        })
        .ToListAsync();

    var semester1Marks = marks
        .Where(m => m.Semester == 1)
        .Select(m => new
        {
            m.LocalSubjectId,
            m.SubjectName,
            m.AcademicYear,
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
            m.TeacherName,
            m.LocalTeacherNumber,
            m.Notes,
            m.UpdatedAt
        })
        .OrderBy(m => m.LocalSubjectId)
        .ToList();

    var semester2Marks = marks
        .Where(m => m.Semester == 2)
        .Select(m => new
        {
            m.LocalSubjectId,
            m.SubjectName,
            m.AcademicYear,
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
            m.TeacherName,
            m.LocalTeacherNumber,
            m.Notes,
            m.UpdatedAt
        })
        .OrderBy(m => m.LocalSubjectId)
        .ToList();

    var semester1Average = semester1Marks.Any() 
        ? Math.Round(semester1Marks.Average(m => m.Total), 2) 
        : 0;

    var semester2Average = semester2Marks.Any() 
        ? Math.Round(semester2Marks.Average(m => m.Total), 2) 
        : 0;

    var finalAverage = marks.Any() 
        ? Math.Round(marks.Average(m => m.Total), 2) 
        : 0;

    var subjectsStatistics = new
    {
        TotalSubjects = subjects.Count,
        SubjectsWithMarks = marks.Select(m => m.LocalSubjectId).Distinct().Count(),
        SubjectsWithoutMarks = subjects.Count - marks.Select(m => m.LocalSubjectId).Distinct().Count()
    };

    var marksStatistics = new
    {
        TotalMarks = marks.Count,
        PassedSubjects = marks.Count(m => m.Total >= 60),
        FailedSubjects = marks.Count(m => m.Total < 60),
        SuccessRate = marks.Any() ? 
            Math.Round((double)marks.Count(m => m.Total >= 60) / marks.Count * 100, 2) : 0,
        Semester1Count = semester1Marks.Count,
        Semester2Count = semester2Marks.Count,
        Semester1Average = semester1Average,
        Semester2Average = semester2Average,
        FinalAverage = finalAverage
    };

    var reportCards = await db.ReportCards
        .Where(r => r.StudentId == StudentId)
        .OrderByDescending(r => r.Year)
        .ThenByDescending(r => r.Semester)
        .Select(r => new
        {
            r.Semester,
            r.Year,
            r.Average,
            r.Rank,
            r.Passed,
            Subjects = r.Subjects.Select(s => new
            {
                s.SubjectName,
                LocalSubjectId = db.Subjects
                    .Where(sub => sub.Name == s.SubjectName && sub.SchoolId == SchoolId)
                    .Select(sub => sub.LocalSubjectId)
                    .FirstOrDefault(),
                s.Total,
            }).ToList()
        })
        .ToListAsync();

    // ✅ الحضور (تمت إضافة فلترة IsDeleted)
    var attendance = await db.StudentAttendances
        .Where(a => a.StudentId == StudentId && !a.IsDeleted)
        .OrderByDescending(a => a.Date)
        .Take(200)
        .Select(a => new
        {
            a.Date,
            Status = a.Status.ToString(),
            LocalSectionNumber = db.Sections
                .Where(s => s.Id == a.SectionId)
                .Select(s => s.LocalSectionNumber)
                .FirstOrDefault()
        })
        .ToListAsync();

    var scheduleImage = me.SectionId is not null ?
        await db.ScheduleImages
            .Where(s => s.SchoolId == SchoolId && 
                        s.SectionId == me.SectionId && 
                        s.Type == ScheduleImageType.Section)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.ImageUrl,
                s.Description,
                s.CreatedAt
            })
            .FirstOrDefaultAsync() : null;

    var loans = await db.BookLoans
        .Where(l => l.StudentId == StudentId)
        .OrderByDescending(l => l.date)
        .Select(l => new
        {
            LocalLoanNumber = l.LocalLoanNumber,
            BookTitle = l.Book != null ? l.Book.Title : "غير معروف",
            LocalBookNumber = l.Book != null ? l.Book.LocalBookNumber : 0,
            l.date,
            l.expiryDate,
            l.ReturnDate,
            Status = l.Status.ToString(),
            IsOverdue = l.expiryDate < DateOnly.FromDateTime(DateTime.Today) && l.Status == LoanStatus.Active
        })
        .ToListAsync();

    var reservations = await db.BookReservations
        .Where(r => r.StudentId == StudentId)
        .OrderByDescending(r => r.Date)
        .Select(r => new
        {
            BookTitle = r.Book != null ? r.Book.Title : "غير معروف",
            LocalBookNumber = r.Book != null ? r.Book.LocalBookNumber : 0,
            r.Date,
            r.ExpiryDate,
            Status = r.Status.ToString(),
            StatusArabic = r.Status == ReservationStatus.Pending ? "قيد الانتظار" :
                          r.Status == ReservationStatus.Approved ? "تمت الموافقة" :
                          r.Status == ReservationStatus.Rejected ? "مرفوض" :
                          r.Status == ReservationStatus.Fulfilled ? "تم الاستعارة" :
                          r.Status == ReservationStatus.Cancelled ? "ملغي" : "غير معروف",
            r.CreatedAt
        })
        .ToListAsync();

    var activities = await db.ActivityRegistrations
        .Include(r => r.Activity)
        .Where(r => r.StudentId == StudentId)
        .Select(r => new
        {
            ActivityName = r.Activity != null ? r.Activity.Title : "غير معروف",
            ActivityId = r.Activity != null ? r.Activity.Id : 0,
            Status = r.Status.ToString(),
            StatusArabic = r.Status == RegistrationStatus.Pending ? "قيد الانتظار" :
                          r.Status == RegistrationStatus.Approved ? "مقبول" :
                          r.Status == RegistrationStatus.Rejected ? "مرفوض" : "غير معروف",
            r.CreatedAt
        })
        .ToListAsync();

    var warnings = await db.Warnings
        .Where(w => w.StudentId == StudentId)
        .OrderByDescending(w => w.CreatedAt)
        .Select(w => new
        {
            Type = w.Type.ToString(),
            w.Reason,
            w.CreatedAt,
            IssuedBy = db.Employees
                .Where(e => e.Id == w.IssuedById)
                .Select(e => e.Name)
                .FirstOrDefault() ?? "الإدارة"
        })
        .ToListAsync();

    var punishments = await db.Punishments
        .Where(p => p.StudentId == StudentId)
        .OrderByDescending(p => p.CreatedAt)
        .Select(p => new
        {
            Type = p.Type.ToString(),
            p.Reason,
            p.CreatedAt,
            IssuedBy = db.Employees
                .Where(e => e.Id == p.IssuedById)
                .Select(e => e.Name)
                .FirstOrDefault() ?? "الإدارة"
        })
        .ToListAsync();

    var summons = await db.GuardianSummons
        .Where(s => s.StudentId == StudentId)
        .OrderByDescending(s => s.CreatedAt)
        .Select(s => new
        {
            s.Reason,
            s.Date,
            s.CreatedAt
        })
        .ToListAsync();

    var complaints = await db.Complaints
        .Where(c => c.FromUserId == StudentId && c.FromUserType == UserType.Student)
        .OrderByDescending(c => c.CreatedAt)
        .Select(c => new
        {
            c.Against,
            c.Content,
            Status = c.Status.ToString(),
            c.Resolution,
            c.CreatedAt
        })
        .ToListAsync();

    var notifications = await db.Notifications
        .Where(n => n.UserId == StudentId && n.UserType == UserType.Student)
        .OrderByDescending(n => n.CreatedAt)
        .Take(100)
        .Select(n => new
        {
            n.Title,
            n.Body,
            n.Type,
            n.IsRead,
            n.CreatedAt
        })
        .ToListAsync();

    var statistics = new
    {
        TotalSubjects = subjects.Count,
        TotalMarks = marks.Count,
        TotalSemester1Marks = semester1Marks.Count,
        TotalSemester2Marks = semester2Marks.Count,
        Semester1Average = semester1Average,
        Semester2Average = semester2Average,
        FinalAverage = finalAverage,
        TotalReportCards = reportCards.Count,
        TotalAttendance = attendance.Count,
        TotalActivities = activities.Count,
        TotalWarnings = warnings.Count,
        TotalPunishments = punishments.Count,
        TotalComplaints = complaints.Count,
        TotalNotifications = notifications.Count,
        TotalLoans = loans.Count,
        TotalReservations = reservations.Count,
        ActiveLoans = loans.Count(l => l.Status == "Active"),
        PendingReservations = reservations.Count(r => r.Status == "Pending")
    };

    return Ok(new
    {
        success = true,
        message = "تم جلب الملف الشخصي الكامل بنجاح",
        data = new
        {
            Student = studentInfo,
            
            Subjects = subjects.Select(s => new
            {
                s.LocalSubjectId,
                s.SubjectName,
                HasMarks = marks.Any(m => m.LocalSubjectId == s.LocalSubjectId)
            }).ToList(),
            
            SubjectsStatistics = subjectsStatistics,
            MarksStatistics = marksStatistics,
            
            Semester1Marks = semester1Marks,
            Semester2Marks = semester2Marks,
            
            Semester1Average = semester1Average,
            Semester2Average = semester2Average,
            FinalAverage = finalAverage,
            
            ReportCards = reportCards,
            Attendance = attendance,
            ScheduleImage = scheduleImage,
            
            Library = new
            {
                Loans = loans,
                Reservations = reservations,
                TotalLoans = loans.Count,
                ActiveLoans = loans.Count(l => l.Status == "Active"),
                TotalReservations = reservations.Count,
                PendingReservations = reservations.Count(r => r.Status == "Pending")
            },
            
            Activities = activities,
            Warnings = warnings,
            Punishments = punishments,
            Summons = summons,
            Complaints = complaints,
            Notifications = notifications,
            
            Statistics = statistics
        }
    });
}
}