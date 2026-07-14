using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Auth;
using SchoolManagement.Api.Data;
using SchoolManagement.Api.Dtos;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/student")]
[Authorize(Roles = Roles.Student)]
public class StudentController(AppDbContext db) : ControllerBase
{
    private int StudentId => User.GetUserId();
    private int SchoolId => User.GetSchoolId();

    private async Task<Student?> MeAsync() => await db.Students.FindAsync(StudentId);

    // ============================================
    // جلب صورة جدول الشعبة للطالب
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
                AcademicYear = s.Grade != null ? s.Grade.AcademicYear : 0
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
                AcademicYear = sectionData.AcademicYear,
                Subjects = subjects,
                TotalSubjects = subjects.Count
            }
        });
    }

    // ============================================
    // العلامات
    // ============================================

    [HttpGet("marks")]
    public async Task<IActionResult> GetMarks([FromQuery] int? semester)
    {
        var query = db.Marks
            .Where(m => m.StudentId == StudentId);

        if (semester is not null) 
            query = query.Where(m => m.Semester == semester);

        var marks = await query
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
            .Where(a => a.StudentId == StudentId)
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
                a.Name,
                a.Description,
                a.Type,
                TypeName = a.Type.ToString(),
                a.Schedule,
                a.Capacity,
                RegisteredCount = db.ActivityRegistrations
                    .Count(r => r.ActivityId == a.Id && r.Status == RegistrationStatus.Approved),
                IsRegistered = db.ActivityRegistrations
                    .Any(r => r.ActivityId == a.Id && r.StudentId == StudentId),
                IsFull = db.ActivityRegistrations
                    .Count(r => r.ActivityId == a.Id && r.Status == RegistrationStatus.Approved) >= a.Capacity
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب الأنشطة بنجاح",
            data = activities
        });
    }

    [HttpPost("activities/{localActivityId:int}/register")]
    public async Task<IActionResult> RegisterActivity(int localActivityId)
    {
        var activity = await db.Activities
            .FirstOrDefaultAsync(a => a.SchoolId == SchoolId && 
                                      a.LocalActivityId == localActivityId);
            
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
            
        if (approved >= activity.Capacity)
            return BadRequest(new { success = false, message = "اكتملت سعة النشاط" });

        var registration = new ActivityRegistration 
        { 
            ActivityId = activity.Id, 
            StudentId = StudentId,
            Status = RegistrationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.ActivityRegistrations.Add(registration);
        await db.SaveChangesAsync();

        return Created($"api/student/activities/{localActivityId}/register", new
        {
            success = true,
            message = "تم التسجيل في النشاط بنجاح",
            data = new
            {
                registration.Id,
                ActivityLocalId = localActivityId,
                registration.ActivityId,
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
                ActivityName = r.Activity != null ? r.Activity.Name : "غير معروف",
                ActivityType = r.Activity != null ? r.Activity.Type.ToString() : "غير معروف",
                ActivitySchedule = r.Activity != null ? r.Activity.Schedule : null,
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

    [HttpDelete("activities/registrations/{localActivityId:int}/{localStudentNumber:int}")]
public async Task<IActionResult> CancelRegistration(int localActivityId, int localStudentNumber)
{
    // ✅ البحث عن النشاط باستخدام LocalActivityId
    var activity = await db.Activities
        .FirstOrDefaultAsync(a => a.SchoolId == SchoolId && 
                                  a.LocalActivityId == localActivityId);
    
    if (activity is null)
        return NotFound(new { success = false, message = $"لا يوجد نشاط برقم {localActivityId}" });

    // ✅ البحث عن الطالب باستخدام LocalStudentNumber
    var student = await db.Students
        .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                  s.LocalStudentNumber == localStudentNumber);
    
    if (student is null)
        return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber}" });

    // ✅ البحث عن التسجيل
    var registration = await db.ActivityRegistrations
        .Include(r => r.Activity)
        .FirstOrDefaultAsync(r => r.ActivityId == activity.Id && 
                                  r.StudentId == student.Id);

    if (registration is null)
        return NotFound(new { success = false, message = "الطالب غير مسجل في هذا النشاط" });

    // ✅ التحقق من الحالة
    if (registration.Status == RegistrationStatus.Approved)
        return BadRequest(new { success = false, message = "لا يمكن إلغاء تسجيل تمت الموافقة عليه، راجع مشرف النشاطات" });

    // ✅ حذف التسجيل
    db.ActivityRegistrations.Remove(registration);
    await db.SaveChangesAsync();

    return Ok(new
    {
        success = true,
        message = "تم إلغاء التسجيل بنجاح",
        data = new
        {
            ActivityLocalId = localActivityId,
            ActivityName = activity.Name,
            StudentLocalNumber = localStudentNumber,
            StudentName = student.Name
        }
    });
}

    // ============================================
    // المكتبة
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
                b.Isbn,
                b.Copies,
                AvailableCopies = b.AvailableCopies,
                IsAvailable = b.AvailableCopies > 0
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
                b.Isbn,
                b.Copies,
                b.AvailableCopies,
                IsAvailable = b.AvailableCopies > 0,
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

    [HttpGet("library/loans")]
    public async Task<IActionResult> GetMyLoans()
    {
        var member = await db.LibraryMembers
            .Where(m => m.StudentId == StudentId)
            .Select(m => new { m.Id, m.LocalMemberNumber })
            .FirstOrDefaultAsync();

        if (member is null)
            return Ok(new 
            { 
                success = true, 
                message = "لست عضواً في المكتبة", 
                data = new { loans = Array.Empty<object>() } 
            });

        var loans = await db.BookLoans
            .Where(l => l.MemberId == member.Id)
            .OrderByDescending(l => l.LoanDate)
            .Select(l => new
            {
                LocalLoanNumber = l.LocalLoanNumber,
                BookTitle = l.Book != null ? l.Book.Title : "غير معروف",
                LocalBookNumber = l.Book != null ? l.Book.LocalBookNumber : 0,
                l.LoanDate,
                l.DueDate,
                l.ReturnDate,
                Status = l.Status.ToString(),
                IsOverdue = l.DueDate < DateOnly.FromDateTime(DateTime.Today) && l.Status == LoanStatus.Active
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب استعاراتك بنجاح",
            data = new
            {
                MemberLocalNumber = member.LocalMemberNumber,
                Loans = loans,
                TotalLoans = loans.Count,
                ActiveLoans = loans.Count(l => l.Status == "Active")
            }
        });
    }

    [HttpPost("library/books/{localBookNumber:int}/reserve")]
    public async Task<IActionResult> ReserveBook(int localBookNumber)
    {
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                      b.LocalBookNumber == localBookNumber);
            
        if (book is null) 
            return NotFound(new { success = false, message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" });

        var member = await db.LibraryMembers
            .FirstOrDefaultAsync(m => m.StudentId == StudentId);
            
        if (member is null) 
            return BadRequest(new { success = false, message = "لست عضواً في المكتبة — راجع أمين المكتبة" });
            
        if (member.Status != MemberStatus.Active) 
            return BadRequest(new { success = false, message = "عضويتك موقوفة" });

        var existingReservation = await db.BookReservations
            .FirstOrDefaultAsync(r => r.BookId == book.Id && 
                                      r.MemberId == member.Id && 
                                      r.Status == ReservationStatus.Pending);
                                      
        if (existingReservation is not null)
            return BadRequest(new { success = false, message = "لديك حجز معلق على هذا الكتاب" });

        var reservation = new BookReservation
        {
            BookId = book.Id,
            MemberId = member.Id,
            Date = DateOnly.FromDateTime(DateTime.Today),
            Status = ReservationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.BookReservations.Add(reservation);
        await db.SaveChangesAsync();

        return Created($"api/student/library/reservations/{reservation.Id}", new
        {
            success = true,
            message = "تم حجز الكتاب بنجاح",
            data = new
            {
                LocalBookNumber = book.LocalBookNumber,
                BookTitle = book.Title,
                MemberLocalNumber = member.LocalMemberNumber,
                reservation.Date,
                Status = reservation.Status.ToString()
            }
        });
    }

    [HttpGet("library/reservations")]
    public async Task<IActionResult> GetMyReservations()
    {
        var member = await db.LibraryMembers
            .Where(m => m.StudentId == StudentId)
            .Select(m => new { m.Id, m.LocalMemberNumber })
            .FirstOrDefaultAsync();

        if (member is null)
            return Ok(new 
            { 
                success = true, 
                message = "لست عضواً في المكتبة", 
                data = new { reservations = Array.Empty<object>() } 
            });

        var reservations = await db.BookReservations
            .Where(r => r.MemberId == member.Id)
            .OrderByDescending(r => r.Date)
            .Select(r => new
            {
                BookTitle = r.Book != null ? r.Book.Title : "غير معروف",
                LocalBookNumber = r.Book != null ? r.Book.LocalBookNumber : 0,
                r.Date,
                Status = r.Status.ToString(),
                r.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب حجوزاتك بنجاح",
            data = new
            {
                MemberLocalNumber = member.LocalMemberNumber,
                Reservations = reservations,
                TotalReservations = reservations.Count,
                PendingReservations = reservations.Count(r => r.Status == "Pending")
            }
        });
    }

    // ============================================
    // التحذيرات والعقوبات
    // ============================================

    [HttpGet("warnings")]
    public async Task<IActionResult> GetMyWarnings()
    {
        var warnings = await db.Warnings
            .Where(w => w.StudentId == StudentId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new
            {
                w.Id,
                w.Type,
                TypeName = w.Type.ToString(),
                w.Reason,
                w.CreatedAt,
                IssuedBy = db.Employees
                    .Where(e => e.Id == w.IssuedById)
                    .Select(e => e.Name)
                    .FirstOrDefault() ?? "الإدارة"
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب التحذيرات بنجاح",
            data = warnings
        });
    }

    [HttpGet("punishments")]
    public async Task<IActionResult> GetMyPunishments()
    {
        var punishments = await db.Punishments
            .Where(p => p.StudentId == StudentId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.Type,
                TypeName = p.Type.ToString(),
                p.Reason,
                p.CreatedAt,
                IssuedBy = db.Employees
                    .Where(e => e.Id == p.IssuedById)
                    .Select(e => e.Name)
                    .FirstOrDefault() ?? "الإدارة"
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب العقوبات بنجاح",
            data = punishments
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
            AcademicYear = me.Section?.Grade?.AcademicYear ?? 0,
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

        var subjects = new List<object>();
        if (me.SectionId is not null && me.Section?.Grade != null)
        {
            var subjectList = await db.Subjects
                .Where(s => s.SchoolId == SchoolId && 
                            s.Grade != null && 
                            s.Grade.LocalGradeNumber == me.Section.Grade.LocalGradeNumber)
                .Select(s => new
                {
                    LocalSubjectId = s.LocalSubjectId,
                    s.Name,
                    TeacherName = s.Teacher != null ? s.Teacher.Name : null,
                    LocalTeacherNumber = db.EmployeeSchools
                        .Where(es => es.EmployeeId == s.TeacherId && 
                                     es.SchoolId == SchoolId && 
                                     es.IsActive)
                        .Select(es => (int?)es.LocalEmployeeNumber)
                        .FirstOrDefault()
                })
                .ToListAsync();
            
            subjects.AddRange(subjectList);
        }

        var marks = await db.Marks
            .Where(m => m.StudentId == StudentId)
            .OrderByDescending(m => m.Semester)
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

        var attendance = await db.StudentAttendances
            .Where(a => a.StudentId == StudentId)
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

        var member = await db.LibraryMembers
            .Where(m => m.StudentId == StudentId)
            .Select(m => new
            {
                LocalMemberNumber = m.LocalMemberNumber,
                Status = m.Status.ToString(),
                m.CreatedAt
            })
            .FirstOrDefaultAsync();

        var activities = await db.ActivityRegistrations
            .Where(r => r.StudentId == StudentId)
            .Select(r => new
            {
                ActivityName = db.Activities
                    .Where(a => a.Id == r.ActivityId)
                    .Select(a => a.Name)
                    .FirstOrDefault() ?? "غير معروف",
                ActivityType = db.Activities
                    .Where(a => a.Id == r.ActivityId)
                    .Select(a => a.Type.ToString())
                    .FirstOrDefault() ?? "غير معروف",
                Status = r.Status.ToString(),
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
            TotalReportCards = reportCards.Count,
            TotalAttendance = attendance.Count,
            TotalActivities = activities.Count,
            TotalWarnings = warnings.Count,
            TotalPunishments = punishments.Count,
            TotalComplaints = complaints.Count,
            TotalNotifications = notifications.Count,
        };

        return Ok(new
        {
            success = true,
            message = "تم جلب الملف الشخصي الكامل بنجاح",
            data = new
            {
                Student = studentInfo,
                Statistics = statistics,
                Subjects = subjects,
                Marks = marks,
                ReportCards = reportCards,
                Attendance = attendance,
                ScheduleImage = scheduleImage,
                Library = new
                {
                    Member = member,
                },
                Activities = activities,
                Warnings = warnings,
                Punishments = punishments,
                Summons = summons,
                Complaints = complaints,
                Notifications = notifications
            }
        });
    }
}