using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Data;
using SchoolManagement.Api.Dtos;
using SchoolManagement.Api.Models;
using SchoolManagement.Api.Services;

namespace SchoolManagement.Api.Controllers;

public static class AttendanceHelper
{
    public static async Task<IActionResult> RecordAsync(
        AppDbContext db, 
        StudentAttendanceRequest request, 
        int takenById, 
        ControllerBase controller,
        NotificationService? notifier = null)  // ✅ إضافة NotificationService (اختياري)
    {
        // 1. التحقق من وجود الشعبة
        var sectionExists = await db.Sections
            .AnyAsync(s => s.Id == request.SectionId);
        
        if (!sectionExists)
            return controller.BadRequest(new { message = "الشعبة غير موجودة" });

        var absentStudents = new List<Student>();

        foreach (var entry in request.Entries)
        {
            // 2. التحقق من وجود الطالب في الشعبة
            var studentExists = await db.Students
                .AnyAsync(s => s.Id == entry.StudentId && s.SectionId == request.SectionId);
            
            if (!studentExists)
                return controller.BadRequest(new { message = $"الطالب {entry.StudentId} ليس في هذه الشعبة" });

            // 3. البحث عن سجل حضور سابق لنفس اليوم
            var existing = await db.StudentAttendances
                .FirstOrDefaultAsync(a => a.StudentId == entry.StudentId && a.Date == request.Date);
            
            if (existing is not null)
            {
                existing.Status = entry.Status;
                existing.TakenById = takenById;
            }
            else
            {
                db.StudentAttendances.Add(new StudentAttendance
                {
                    StudentId = entry.StudentId,
                    SectionId = request.SectionId,
                    Date = request.Date,
                    Status = entry.Status,
                    TakenById = takenById,
                });
            }

            // ✅ إذا كان الطالب غائب، أضفه للقائمة
            if (entry.Status == AttendanceStatus.Absent)
            {
                var student = await db.Students.FindAsync(entry.StudentId);
                if (student is not null)
                    absentStudents.Add(student);
            }
        }
        
        // 4. حفظ التغييرات
        await db.SaveChangesAsync();

        // ✅ 5. إرسال إشعارات لأولياء أمور الطلاب الغائبين
        if (notifier is not null && absentStudents.Any())
        {
            foreach (var student in absentStudents)
            {
                await notifier.SendToGuardianAsync(
                    student,
                    "⚠️ تنبيه: غياب",
                    $"ابنكم {student.Name} غاب اليوم {request.Date:yyyy-MM-dd}",
                    "attendance"
                );
            }
        }

        return controller.Ok(new 
        { 
            message = "تم تسجيل الحضور بنجاح",
            sectionId = request.SectionId,
            date = request.Date,
            studentsCount = request.Entries.Count,
            absentCount = absentStudents.Count
        });
    }
}