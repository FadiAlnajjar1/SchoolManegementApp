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

        if (await db.Students.AnyAsync(s => s.Email == request.Email && s.SchoolId == SchoolId))
            return BadRequest(new { success = false, message = "البريد الإلكتروني موجود مسبقاً" });

        // التحقق من الشعبة
        if (request.LocalSectionNumber.HasValue)
        {
            var sectionExists = await db.Sections
                .AnyAsync(s => s.SchoolId == SchoolId && 
                              s.LocalSectionNumber == request.LocalSectionNumber.Value);
            
            if (!sectionExists)
                return BadRequest(new { success = false, message = "الشعبة غير موجودة في هذه المدرسة" });
        }

        // حساب LocalStudentNumber
        var maxLocalNumber = await db.Students
            .Where(s => s.SchoolId == SchoolId)
            .Select(s => (int?)s.LocalStudentNumber)
            .MaxAsync() ?? 0;

        int newLocalNumber = maxLocalNumber + 1;

        // البحث عن SectionId
        int? sectionId = null;
        if (request.LocalSectionNumber.HasValue)
        {
            var section = await db.Sections
                .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                         s.LocalSectionNumber == request.LocalSectionNumber.Value);
            sectionId = section?.Id;
        }

        var student = new Student
        {
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            SchoolId = SchoolId,
            LocalStudentNumber = newLocalNumber,
            SectionId = sectionId,
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

        await notifier.SendAsync(
            student.Id,
            UserType.Student,
            "مرحباً في المدرسة",
            $"تم تسجيلك في مدرسة '{school.Name}' برقم طالب {newLocalNumber}",
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
                LocalSectionNumber = request.LocalSectionNumber,
                student.BirthDate,
                student.Address,
                student.GuardianName,
                student.GuardianPhone,
                student.CreatedAt
            }
        });
    }

    [HttpPut("students/{localStudentNumber:int}")]
    public async Task<IActionResult> UpdateStudent(int localStudentNumber, StudentUpdateRequesting request)
    {
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId &&
                                      s.LocalStudentNumber == localStudentNumber);

        if (student is null)
            return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber} في هذه المدرسة" });

        // تحديث الشعبة باستخدام LocalSectionNumber
        if (request.LocalSectionNumber.HasValue)
        {
            var sectionExists = await db.Sections
                .AnyAsync(s => s.SchoolId == SchoolId && 
                              s.LocalSectionNumber == request.LocalSectionNumber.Value);
            
            if (!sectionExists)
                return BadRequest(new { success = false, message = "الشعبة غير موجودة في هذه المدرسة" });

            var section = await db.Sections
                .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                         s.LocalSectionNumber == request.LocalSectionNumber.Value);
            student.SectionId = section?.Id;
        }

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

        if (request.BirthDate.HasValue)
            student.BirthDate = request.BirthDate;

        await db.SaveChangesAsync();

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
                LocalSectionNumber = student.Section?.LocalSectionNumber,
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
            return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber} في هذه المدرسة" });

        // حذف البيانات المرتبطة
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

        // حذف عضوية المكتبة
        var libraryMember = await db.LibraryMembers.FirstOrDefaultAsync(m => m.StudentId == student.Id);
        if (libraryMember is not null)
        {
            var bookLoans = await db.BookLoans.Where(l => l.MemberId == libraryMember.Id).ToListAsync();
            if (bookLoans.Any()) db.BookLoans.RemoveRange(bookLoans);

            var bookReservations = await db.BookReservations.Where(r => r.MemberId == libraryMember.Id).ToListAsync();
            if (bookReservations.Any()) db.BookReservations.RemoveRange(bookReservations);

            db.LibraryMembers.Remove(libraryMember);
        }

        db.Students.Remove(student);
        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = $"تم حذف الطالب رقم {localStudentNumber} بنجاح",
            data = new
            {
                LocalId = localStudentNumber,
                StudentName = student.Name
            }
        });
    }

    // ============================================
    // دوال مساعدة
    // ============================================

    private async Task NotifyAnnouncementAsync(Announcement announcement)
    {
        switch (announcement.Audience)
        {
            case AnnouncementAudience.All:
                break;
            case AnnouncementAudience.Students:
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
                        "announcement");
                }
                break;
            case AnnouncementAudience.Teachers:
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
                        "announcement");
                }
                break;
            case AnnouncementAudience.Parents:
                var parents = await db.Students
                    .Where(s => s.SchoolId == SchoolId && s.IsActive)
                    .ToListAsync();
                
                foreach (var student in parents)
                {
                    await notifier.SendToGuardianAsync(
                        student,
                        announcement.Title,
                        announcement.Body,
                        "announcement");
                }
                break;
        }
    }
}