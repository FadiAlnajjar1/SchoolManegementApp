using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Data;
using SchoolManagement.Api.Dtos;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Controllers;

public static class AttendanceHelper
{
    public static async Task<IActionResult> RecordAsync(
        AppDbContext db, 
        StudentAttendanceRequest request, 
        int takenById, 
        ControllerBase controller)
    {
        // 1. التحقق من وجود الشعبة (اختياري ولكن مفيد)
        var sectionExists = await db.Sections
            .AnyAsync(s => s.Id == request.SectionId);
        
        if (!sectionExists)
            return controller.BadRequest(new { message = "الشعبة غير موجودة" });

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
                // تحديث السجل الموجود
                existing.Status = entry.Status;
                existing.TakenById = takenById;
            }
            else
            {
                // إنشاء سجل جديد
                db.StudentAttendances.Add(new StudentAttendance
                {
                    StudentId = entry.StudentId,
                    SectionId = request.SectionId,
                    Date = request.Date,
                    Status = entry.Status,
                    TakenById = takenById,
                });
            }
        }
        
        // 4. حفظ التغييرات
        await db.SaveChangesAsync();
        
        return controller.Ok(new 
        { 
            message = "تم تسجيل الحضور بنجاح",
            sectionId = request.SectionId,
            date = request.Date,
            studentsCount = request.Entries.Count
        });
    }
}