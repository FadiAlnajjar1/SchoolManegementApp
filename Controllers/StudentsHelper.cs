using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Data;
using SchoolManagement.Api.Dtos;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Controllers;

public static class StudentsHelper
{
    public static async Task<IActionResult> CreateAsync(
        AppDbContext db, 
        int schoolId, 
        StudentCreateRequest request, 
        ControllerBase controller)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        
        if (await db.Students.AnyAsync(s => s.Email == email && s.SchoolId == schoolId))
            return controller.BadRequest(new { success = false, message = "الإيميل مستخدم بالفعل" });

        if (request.LocalSectionNumber.HasValue)
        {
            var sectionExists = await db.Sections
                .AnyAsync(s => s.SchoolId == schoolId && 
                              s.LocalSectionNumber == request.LocalSectionNumber.Value);
            
            if (!sectionExists)
                return controller.BadRequest(new { success = false, message = "الشعبة غير موجودة في مدرستك" });
        }

        var maxLocalNumber = await db.Students
            .Where(s => s.SchoolId == schoolId)
            .Select(s => (int?)s.LocalStudentNumber)
            .MaxAsync() ?? 0;

        int newLocalNumber = maxLocalNumber + 1;

        int? sectionId = null;
        if (request.LocalSectionNumber.HasValue)
        {
            var section = await db.Sections
                .FirstOrDefaultAsync(s => s.SchoolId == schoolId && 
                                         s.LocalSectionNumber == request.LocalSectionNumber.Value);
            sectionId = section?.Id;
        }

        var student = new Student
        {
            Name = request.Name,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            SchoolId = schoolId,
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

        return controller.Created($"api/students/{student.LocalStudentNumber}", new
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
                SectionId = student.SectionId,
                LocalSectionNumber = request.LocalSectionNumber,
                student.CreatedAt
            }
        });
    }

    public static async Task<IActionResult> UpdateAsync(
        AppDbContext db, 
        int schoolId, 
        int localStudentNumber, 
        StudentUpdateRequest request, 
        ControllerBase controller)
    {
        var student = await db.Students
            .Include(s => s.Section)
            .FirstOrDefaultAsync(s => s.SchoolId == schoolId && 
                                      s.LocalStudentNumber == localStudentNumber);

        if (student is null) 
            return controller.NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber}" });

        if (request.LocalSectionNumber.HasValue)
        {
            var sectionExists = await db.Sections
                .AnyAsync(s => s.SchoolId == schoolId && 
                              s.LocalSectionNumber == request.LocalSectionNumber.Value);
            
            if (!sectionExists)
                return controller.BadRequest(new { success = false, message = "الشعبة غير موجودة في مدرستك" });

            var section = await db.Sections
                .FirstOrDefaultAsync(s => s.SchoolId == schoolId && 
                                         s.LocalSectionNumber == request.LocalSectionNumber.Value);
            student.SectionId = section?.Id;
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
            student.Name = request.Name;

        if (!string.IsNullOrWhiteSpace(request.GuardianName))
            student.GuardianName = request.GuardianName;

        if (!string.IsNullOrWhiteSpace(request.GuardianPhone))
            student.GuardianPhone = request.GuardianPhone;

        if (!string.IsNullOrWhiteSpace(request.BloodType))
            student.BloodType = request.BloodType;

        // if (!string.IsNullOrWhiteSpace(request.ChronicDiseases))
        //     student.ChronicDiseases = request.ChronicDiseases;

        // if (!string.IsNullOrWhiteSpace(request.Allergies))
        //     student.Allergies = request.Allergies;

        // if (!string.IsNullOrWhiteSpace(request.HealthNotes))
        //     student.HealthNotes = request.HealthNotes;

        if (!string.IsNullOrWhiteSpace(request.Address))
            student.Address = request.Address;

        if (request.BirthDate.HasValue)
            student.BirthDate = request.BirthDate;
        
        if (!string.IsNullOrWhiteSpace(request.Password))
            student.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        await db.SaveChangesAsync();

        return controller.Ok(new
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
                SectionId = student.SectionId,
                LocalSectionNumber = student.Section?.LocalSectionNumber,
                student.GuardianName,
                student.GuardianPhone,
                student.CreatedAt
            }
        });
    }

    public static async Task<IActionResult> GetStudentAsync(
        AppDbContext db,
        int schoolId,
        int localStudentNumber,
        ControllerBase controller)
    {
        var student = await db.Students
            .Include(s => s.Section)
                .ThenInclude(sec => sec!.Grade)
            .Where(s => s.SchoolId == schoolId && 
                        s.LocalStudentNumber == localStudentNumber)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Email,
                s.LocalStudentNumber,
                s.SchoolId,
                s.SectionId,
                SectionName = s.Section != null ? s.Section.Name : null,
                LocalSectionNumber = s.Section != null ? s.Section.LocalSectionNumber : 0,
                GradeName = s.Section != null && s.Section.Grade != null ? s.Section.Grade.Name : null,
                LocalGradeNumber = s.Section != null && s.Section.Grade != null ? s.Section.Grade.LocalGradeNumber : 0,
                s.GuardianName,
                s.GuardianPhone,
                s.BloodType,
                s.BirthDate,
                s.Address,
                s.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (student is null)
            return controller.NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber}" });

        return controller.Ok(new
        {
            success = true,
            message = "تم جلب بيانات الطالب بنجاح",
            data = student
        });
    }

    public static async Task<IActionResult> DeleteAsync(
        AppDbContext db,
        int schoolId,
        int localStudentNumber,
        ControllerBase controller)
    {
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == schoolId && 
                                      s.LocalStudentNumber == localStudentNumber);

        if (student is null)
            return controller.NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber}" });

        // ✅ حذف العلامات
        var marks = await db.Marks.Where(m => m.StudentId == student.Id).ToListAsync();
        if (marks.Any()) db.Marks.RemoveRange(marks);

        // ✅ حذف بطاقات التقارير
        var reportCards = await db.ReportCards.Where(r => r.StudentId == student.Id).ToListAsync();
        if (reportCards.Any()) db.ReportCards.RemoveRange(reportCards);

        // ✅ حذف الحضور
        var attendances = await db.StudentAttendances.Where(a => a.StudentId == student.Id).ToListAsync();
        if (attendances.Any()) db.StudentAttendances.RemoveRange(attendances);

        // ✅ حذف التحذيرات
        var warnings = await db.Warnings.Where(w => w.StudentId == student.Id).ToListAsync();
        if (warnings.Any()) db.Warnings.RemoveRange(warnings);

        // ✅ حذف العقوبات
        var punishments = await db.Punishments.Where(p => p.StudentId == student.Id).ToListAsync();
        if (punishments.Any()) db.Punishments.RemoveRange(punishments);

        // ✅ حذف تسجيلات الأنشطة
        var activityRegistrations = await db.ActivityRegistrations.Where(r => r.StudentId == student.Id).ToListAsync();
        if (activityRegistrations.Any()) db.ActivityRegistrations.RemoveRange(activityRegistrations);

        // ✅ حذف عضوية المكتبة
        var libraryMember = await db.LibraryMembers.FirstOrDefaultAsync(m => m.StudentId == student.Id);
        if (libraryMember is not null)
        {
            var bookLoans = await db.BookLoans.Where(l => l.MemberId == libraryMember.Id).ToListAsync();
            if (bookLoans.Any()) db.BookLoans.RemoveRange(bookLoans);

            var bookReservations = await db.BookReservations.Where(r => r.MemberId == libraryMember.Id).ToListAsync();
            if (bookReservations.Any()) db.BookReservations.RemoveRange(bookReservations);

            db.LibraryMembers.Remove(libraryMember);
        }

        // ✅ حذف سجل الترقيات
        var gradeHistory = await db.StudentGradeHistory.Where(h => h.StudentId == student.Id).ToListAsync();
        if (gradeHistory.Any()) db.StudentGradeHistory.RemoveRange(gradeHistory);

        db.Students.Remove(student);
        await db.SaveChangesAsync();

        return controller.Ok(new
        {
            success = true,
            message = $"تم حذف الطالب رقم {localStudentNumber} بنجاح",
            data = new
            {
                student.Id,
                student.Name,
                student.LocalStudentNumber
            }
        });
    }
}