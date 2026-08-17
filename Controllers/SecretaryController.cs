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
[Route("api/secretary")]
[Authorize(Roles = Roles.Secretary)]
public class SecretaryController(
    AppDbContext db,
    IWebHostEnvironment env,
    NotificationService notifier) : ControllerBase
{
    private int SchoolId => User.GetSchoolId();
    private int SecretaryId => User.GetUserId();

    // ============================================
    // إدارة الإعلانات (Announcements) - باستخدام Local IDs
    // ============================================

    [HttpPost("announcements")]
    public async Task<IActionResult> CreateAnnouncement(AnnouncementRequest request)
    {
        if (request.ExpiryDate.HasValue && request.ExpiryDate < DateTime.UtcNow)
            return BadRequest(new { success = false, message = "تاريخ الانتهاء يجب أن يكون في المستقبل" });

        var maxLocalId = await db.Announcements
            .Where(a => a.SchoolId == SchoolId && a.LocalAnnouncementId > 0)
            .Select(a => (int?)a.LocalAnnouncementId)
            .MaxAsync() ?? 0;

        int newLocalId = maxLocalId + 1;

        var announcement = new Announcement
        {
            SchoolId = SchoolId,
            LocalAnnouncementId = newLocalId,
            Title = request.Title,
            Body = request.Body,
            Audience = request.Audience,
            Type = request.Type,
            CreatedById = SecretaryId,
            ExpiryDate = request.ExpiryDate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Announcements.Add(announcement);
        await db.SaveChangesAsync();

        await NotifyAnnouncementAsync(announcement);

        return Created($"api/secretary/announcements/{announcement.LocalAnnouncementId}", new
        {
            success = true,
            message = "تم إنشاء الإعلان بنجاح",
            data = new
            {
                Id = announcement.Id,
                LocalId = announcement.LocalAnnouncementId,
                Title = announcement.Title,
                Description = announcement.Body,
                Date = announcement.CreatedAt.ToString("yyyy-MM-dd"),
                ExpiryDate = announcement.ExpiryDate,
                Audience = announcement.Audience.ToString(),
                Type = announcement.Type.ToString(),
                CreatedBy = User.Identity?.Name,
                Category = "announcement"
            }
        });
    }

    [HttpGet("announcements")]
    public async Task<IActionResult> GetAnnouncements()
    {
        var now = DateTime.UtcNow;
        
        var announcements = await db.Announcements
            .Where(a => a.SchoolId == SchoolId && 
                       a.IsActive &&
                       (a.ExpiryDate == null || a.ExpiryDate > now))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new 
            {
                Id = a.Id,
                LocalId = a.LocalAnnouncementId,
                a.Title,
                Description = a.Body,
                Date = a.CreatedAt.ToString("yyyy-MM-dd"),
                a.ExpiryDate
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب الإعلانات بنجاح",
            data = new
            {
                announcements = announcements
            }
        });
    }

    [HttpGet("announcements/{localId:int}")]
    public async Task<IActionResult> GetAnnouncement(int localId)
    {
        var announcement = await db.Announcements
            .Where(a => a.SchoolId == SchoolId && a.LocalAnnouncementId == localId)
            .Select(a => new
            {
                Id = a.Id,
                LocalId = a.LocalAnnouncementId,
                a.Title,
                Description = a.Body,
                Date = a.CreatedAt.ToString("yyyy-MM-dd"),
                a.ExpiryDate,
                Audience = a.Audience.ToString(),
                Type = a.Type.ToString(),
                CreatedBy = a.CreatedBy != null ? a.CreatedBy.Name : null,
                a.IsActive
            })
            .FirstOrDefaultAsync();

        if (announcement is null)
            return NotFound(new { success = false, message = $"لا يوجد إعلان برقم {localId}" });

        return Ok(new
        {
            success = true,
            message = "تم جلب الإعلان بنجاح",
            data = announcement
        });
    }

    [HttpPut("announcements/{localId:int}")]
    public async Task<IActionResult> UpdateAnnouncement(int localId, AnnouncementRequest request)
    {
        var announcement = await db.Announcements
            .FirstOrDefaultAsync(a => a.SchoolId == SchoolId && a.LocalAnnouncementId == localId);

        if (announcement is null)
            return NotFound(new { success = false, message = $"لا يوجد إعلان برقم {localId}" });

        if (request.ExpiryDate.HasValue && request.ExpiryDate < DateTime.UtcNow)
            return BadRequest(new { success = false, message = "تاريخ الانتهاء يجب أن يكون في المستقبل" });

        announcement.Title = request.Title;
        announcement.Body = request.Body;
        announcement.Audience = request.Audience;
        announcement.Type = request.Type;
        announcement.ExpiryDate = request.ExpiryDate;

        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "تم تحديث الإعلان بنجاح",
            data = new
            {
                Id = announcement.Id,
                LocalId = announcement.LocalAnnouncementId,
                announcement.Title,
                Description = announcement.Body,
                Date = announcement.CreatedAt.ToString("yyyy-MM-dd"),
                announcement.ExpiryDate,
                Audience = announcement.Audience.ToString(),
                Type = announcement.Type.ToString(),
                CreatedBy = User.Identity?.Name
            }
        });
    }

    [HttpDelete("announcements/{localId:int}")]
    public async Task<IActionResult> DeleteAnnouncement(int localId)
    {
        var announcement = await db.Announcements
            .FirstOrDefaultAsync(a => a.SchoolId == SchoolId && a.LocalAnnouncementId == localId);
        
        if (announcement is null)
            return NotFound(new { success = false, message = $"لا يوجد إعلان برقم {localId}" });

        db.Announcements.Remove(announcement);
        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = $"تم حذف الإعلان رقم {localId} بنجاح",
            data = new
            {
                LocalId = localId,
                Title = announcement.Title
            }
        });
    }

    // ============================================
    // إدارة الطلاب (Students) - باستخدام Local IDs
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

        return Ok(new
        {
            success = true,
            message = "تم جلب الطلاب بنجاح",
            data = students
        });
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
            return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber} في هذه المدرسة" });

        return Ok(new
        {
            success = true,
            message = "تم جلب بيانات الطالب بنجاح",
            data = new
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
            }
        });
    }

    [HttpPost("students")]
    public async Task<IActionResult> CreateStudent(StudentCreateRequest request)
    {
        var school = await db.Schools.FindAsync(SchoolId);
        if (school is null)
            return BadRequest(new { success = false, message = "المدرسة غير موجودة" });

        // ✅ التحقق من وجود البريد الإلكتروني
        if (await db.Students.AnyAsync(s => s.Email == request.Email && s.SchoolId == SchoolId))
            return BadRequest(new { success = false, message = "البريد الإلكتروني موجود مسبقاً" });

        // ✅ التحقق من العمر (يجب أن يكون الطالب أكبر من 5 سنوات)
        if (request.BirthDate.HasValue)
        {
            var age = CalculateAge(request.BirthDate.Value);
            if (age < 5)
                return BadRequest(new { success = false, message = "عمر الطالب يجب أن يكون 5 سنوات على الأقل" });
        }
        else
        {
            return BadRequest(new { success = false, message = "تاريخ الميلاد مطلوب" });
        }

        // ✅ التحقق من وجود الصف باستخدام LocalGradeNumber
        Grade? grade = null;
        if (request.LocalGradeNumber.HasValue)
        {
            grade = await db.Grades
                .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                          g.LocalGradeNumber == request.LocalGradeNumber.Value);
            
            if (grade is null)
                return BadRequest(new { success = false, message = $"لا يوجد صف برقم {request.LocalGradeNumber} في هذه المدرسة" });
        }

        // ✅ التحقق من وجود الشعبة باستخدام LocalSectionNumber (اختياري)
        Section? section = null;
        if (request.LocalSectionNumber.HasValue)
        {
            section = await db.Sections
                .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                          s.LocalSectionNumber == request.LocalSectionNumber.Value);
            
            if (section is null)
                return BadRequest(new { success = false, message = $"لا توجد شعبة برقم {request.LocalSectionNumber} في هذه المدرسة" });

            // ✅ التأكد من أن الشعبة تابعة للصف المحدد
            if (grade != null && section.GradeId != grade.Id)
                return BadRequest(new { success = false, message = "الشعبة غير تابعة للصف المحدد" });
        }

        // ✅ حساب LocalStudentNumber
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
            SectionId = section?.Id,
            GuardianName = request.GuardianName ?? "",
            GuardianPhone = request.GuardianPhone ?? "",
            BloodType = request.BloodType ?? "",
            ChronicDiseases = request.ChronicDiseases ?? "",
            Allergies = request.Allergies ?? "",
            HealthNotes = request.HealthNotes ?? "",
            BirthDate = request.BirthDate,
            Address = request.Address ?? "",
            CreatedAt = DateTime.UtcNow
        };

        db.Students.Add(student);
        await db.SaveChangesAsync();

        // ✅ إرسال إشعار للطالب
        var gradeName = grade?.Name ?? "غير محدد";
        var sectionName = section?.Name ?? "غير محدد";
        await notifier.SendAsync(
            student.Id,
            UserType.Student,
            "مرحباً في المدرسة",
            $"تم تسجيلك في مدرسة '{school.Name}' - الصف: {gradeName} - الشعبة: {sectionName} برقم طالب {newLocalNumber}",
            "registration"
        );

        return Created($"api/secretary/students/{newLocalNumber}", new
        {
            success = true,
            message = "تم إنشاء الطالب بنجاح",
            data = new
            {
                student.Id,
                student.Name,
                student.Email,
                student.LocalStudentNumber,
                student.SchoolId,
                SchoolName = school.Name,
                SectionId = student.SectionId,
                LocalSectionNumber = section?.LocalSectionNumber,
                SectionName = section?.Name,
                LocalGradeNumber = grade?.LocalGradeNumber,
                GradeName = grade?.Name,
                student.BirthDate,
                Age = CalculateAge(student.BirthDate.Value),
                student.Address,
                student.GuardianName,
                student.GuardianPhone,
                student.CreatedAt
            }
        });
    }

    [HttpPut("students/{localStudentNumber:int}")]
    public async Task<IActionResult> UpdateStudent(int localStudentNumber, StudentUpdateRequest request)
    {
        var student = await db.Students
            .Include(s => s.Section)
                .ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalStudentNumber == localStudentNumber);

        if (student is null)
            return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber} في هذه المدرسة" });

        // ✅ التحقق من العمر إذا تم تحديث تاريخ الميلاد
        if (request.BirthDate.HasValue)
        {
            var age = CalculateAge(request.BirthDate.Value);
            if (age < 5)
                return BadRequest(new { success = false, message = "عمر الطالب يجب أن يكون 5 سنوات على الأقل" });
            
            student.BirthDate = request.BirthDate;
        }

        // ✅ تحديث الصف باستخدام LocalGradeNumber
        if (request.LocalGradeNumber.HasValue)
        {
            var grade = await db.Grades
                .FirstOrDefaultAsync(g => g.SchoolId == SchoolId && 
                                          g.LocalGradeNumber == request.LocalGradeNumber.Value);
            
            if (grade is null)
                return BadRequest(new { success = false, message = $"لا يوجد صف برقم {request.LocalGradeNumber} في هذه المدرسة" });

            if (request.LocalSectionNumber.HasValue)
            {
                var section = await db.Sections
                    .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                              s.LocalSectionNumber == request.LocalSectionNumber.Value &&
                                              s.GradeId == grade.Id);
                
                if (section is null)
                    return BadRequest(new { success = false, message = $"لا توجد شعبة برقم {request.LocalSectionNumber} في الصف {request.LocalGradeNumber}" });
                
                student.SectionId = section.Id;
            }
            else
            {
                var firstSection = await db.Sections
                    .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && s.GradeId == grade.Id);
                student.SectionId = firstSection?.Id;
            }
        }
        else if (request.LocalSectionNumber.HasValue)
        {
            var section = await db.Sections
                .Include(s => s.Grade)
                .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                          s.LocalSectionNumber == request.LocalSectionNumber.Value);
            
            if (section is null)
                return BadRequest(new { success = false, message = $"لا توجد شعبة برقم {request.LocalSectionNumber} في هذه المدرسة" });

            student.SectionId = section.Id;
        }

        // ✅ تحديث البيانات الشخصية
        if (!string.IsNullOrWhiteSpace(request.Name))
            student.Name = request.Name;

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var existingEmail = await db.Students
                .AnyAsync(s => s.Email == request.Email && s.Id != student.Id && s.SchoolId == SchoolId);

            if (existingEmail)
                return BadRequest(new { success = false, message = "البريد الإلكتروني مستخدم بالفعل" });

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

        await db.SaveChangesAsync();

        // ✅ جلب البيانات المحدثة
        var updatedStudent = await db.Students
            .Include(s => s.Section)
                .ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(s => s.Id == student.Id);

        return Ok(new
        {
            success = true,
            message = "تم تحديث بيانات الطالب بنجاح",
            data = new
            {
                student.Id,
                student.Name,
                student.Email,
                student.LocalStudentNumber,
                student.SchoolId,
                student.SectionId,
                LocalSectionNumber = updatedStudent?.Section?.LocalSectionNumber,
                SectionName = updatedStudent?.Section?.Name,
                LocalGradeNumber = updatedStudent?.Section?.Grade?.LocalGradeNumber,
                GradeName = updatedStudent?.Section?.Grade?.Name,
                student.BirthDate,
                Age = student.BirthDate.HasValue ? CalculateAge(student.BirthDate.Value) : 0,
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
            return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber} في هذه المدرسة" });

        // ✅ حذف البيانات المرتبطة
        var marks = await db.Marks.Where(m => m.StudentId == student.Id).ToListAsync();
        if (marks.Any()) db.Marks.RemoveRange(marks);

        var reportCards = await db.ReportCards.Where(r => r.StudentId == student.Id).ToListAsync();
        if (reportCards.Any()) db.ReportCards.RemoveRange(reportCards);

        var attendances = await db.StudentAttendances.Where(a => a.StudentId == student.Id).ToListAsync();
        if (attendances.Any()) db.StudentAttendances.RemoveRange(attendances);

        var warnings = await db.Warnings.Where(w => w.StudentId == student.Id).ToListAsync();
        if (warnings.Any()) db.Warnings.RemoveRange(warnings);

        var punishments = await db.Punishments.Where(p => p.StudentId == student.Id).ToListAsync();
        if (punishments.Any()) db.Punishments.RemoveRange(punishments);

        var activityRegistrations = await db.ActivityRegistrations.Where(r => r.StudentId == student.Id).ToListAsync();
        if (activityRegistrations.Any()) db.ActivityRegistrations.RemoveRange(activityRegistrations);

        // ✅ حذف إعارات الكتب (باستخدام StudentId مباشرة)
        var bookLoans = await db.BookLoans.Where(l => l.StudentId == student.Id).ToListAsync();
        if (bookLoans.Any()) db.BookLoans.RemoveRange(bookLoans);

        // ✅ حذف حجوزات الكتب (باستخدام StudentId مباشرة)
        var bookReservations = await db.BookReservations.Where(r => r.StudentId == student.Id).ToListAsync();
        if (bookReservations.Any()) db.BookReservations.RemoveRange(bookReservations);

        // ✅ حذف طلبات الاستعارة (باستخدام StudentId مباشرة)
        var loanRequests = await db.BookLoanRequests.Where(r => r.StudentId == student.Id).ToListAsync();
        if (loanRequests.Any()) db.BookLoanRequests.RemoveRange(loanRequests);

        // ✅ حذف سجل الترقيات
        var gradeHistory = await db.StudentGradeHistory.Where(h => h.StudentId == student.Id).ToListAsync();
        if (gradeHistory.Any()) db.StudentGradeHistory.RemoveRange(gradeHistory);

        // ✅ حذف الشكاوى
        var complaints = await db.Complaints
            .Where(c => c.FromUserId == student.Id && c.FromUserType == UserType.Student)
            .ToListAsync();
        if (complaints.Any()) db.Complaints.RemoveRange(complaints);

        // ✅ حذف الإشعارات
        var notifications = await db.Notifications
            .Where(n => n.UserId == student.Id && n.UserType == UserType.Student)
            .ToListAsync();
        if (notifications.Any()) db.Notifications.RemoveRange(notifications);

        // ✅ حذف استدعاءات ولي الأمر
        var summons = await db.GuardianSummons.Where(s => s.StudentId == student.Id).ToListAsync();
        if (summons.Any()) db.GuardianSummons.RemoveRange(summons);

        // ✅ حذف الطالب
        db.Students.Remove(student);
        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = $"تم حذف الطالب رقم {localStudentNumber} وجميع بياناته بنجاح",
            data = new
            {
                LocalId = localStudentNumber,
                StudentName = student.Name
            }
        });
    }

    // ============================================
    // دالة حساب العمر
    // ============================================

    private int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;
        
        if (birthDate.Date > today.AddYears(-age))
            age--;
        
        return age;
    }

    // ============================================
    // إرسال إشعارات الإعلانات
    // ============================================

    private async Task NotifyAnnouncementAsync(Announcement announcement)
    {
        // ✅ إرسال للجميع (All)
        if (announcement.Audience == AnnouncementAudience.All)
        {
            // إرسال للطلاب
            var allStudents = await db.Students
                .Where(s => s.SchoolId == SchoolId && s.IsActive)
                .ToListAsync();
            
            foreach (var student in allStudents)
            {
                await notifier.SendAsync(
                    student.Id,
                    UserType.Student,
                    announcement.Title,
                    announcement.Body,
                    "announcement",
                    $"/announcements/{announcement.LocalAnnouncementId}"
                );
            }

            // إرسال للمعلمين
            var allTeachers = await db.EmployeeSchools
                .Where(es => es.SchoolId == SchoolId && 
                            es.Role == EmployeeRole.Teacher && 
                            es.IsActive)
                .Join(db.Employees,
                    es => es.EmployeeId,
                    e => e.Id,
                    (es, e) => e)
                .ToListAsync();
            
            foreach (var teacher in allTeachers)
            {
                await notifier.SendAsync(
                    teacher.Id,
                    UserType.Employee,
                    announcement.Title,
                    announcement.Body,
                    "announcement",
                    $"/announcements/{announcement.LocalAnnouncementId}"
                );
            }

            // إرسال للموظفين الآخرين
            var allEmployees = await db.EmployeeSchools
                .Where(es => es.SchoolId == SchoolId && es.IsActive)
                .Join(db.Employees,
                    es => es.EmployeeId,
                    e => e.Id,
                    (es, e) => e)
                .ToListAsync();
            
            foreach (var employee in allEmployees)
            {
                await notifier.SendAsync(
                    employee.Id,
                    UserType.Employee,
                    announcement.Title,
                    announcement.Body,
                    "announcement",
                    $"/announcements/{announcement.LocalAnnouncementId}"
                );
            }

            return;
        }

        // ✅ إرسال للطلاب فقط
        if (announcement.Audience == AnnouncementAudience.Students)
        {
            var students = await db.Students
                .Where(s => s.SchoolId == SchoolId && s.IsActive)
                .ToListAsync();
            
            foreach (var student in students)
            {
                await notifier.SendAsync(
                    student.Id,
                    UserType.Student,
                    announcement.Title,
                    announcement.Body,
                    "announcement",
                    $"/announcements/{announcement.LocalAnnouncementId}"
                );
            }
            return;
        }

        // ✅ إرسال للمعلمين فقط
        if (announcement.Audience == AnnouncementAudience.Teachers)
        {
            var teachers = await db.EmployeeSchools
                .Where(es => es.SchoolId == SchoolId && 
                            es.Role == EmployeeRole.Teacher && 
                            es.IsActive)
                .Join(db.Employees,
                    es => es.EmployeeId,
                    e => e.Id,
                    (es, e) => e)
                .ToListAsync();
            
            foreach (var teacher in teachers)
            {
                await notifier.SendAsync(
                    teacher.Id,
                    UserType.Employee,
                    announcement.Title,
                    announcement.Body,
                    "announcement",
                    $"/announcements/{announcement.LocalAnnouncementId}"
                );
            }
            return;
        }

        // ✅ إرسال لأولياء الأمور
        if (announcement.Audience == AnnouncementAudience.Parents)
        {
            var parents = await db.Students
                .Where(s => s.SchoolId == SchoolId && s.IsActive)
                .ToListAsync();
            
            foreach (var student in parents)
            {
                await notifier.SendToGuardianAsync(
                    student,
                    announcement.Title,
                    announcement.Body,
                    "announcement",
                    $"/announcements/{announcement.LocalAnnouncementId}"
                );
            }
            return;
        }
    }
}