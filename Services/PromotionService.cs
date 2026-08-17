// Services/PromotionService.cs
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Data;
using SchoolManagement.Api.Dtos;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Services;

public class PromotionService(AppDbContext db, NotificationService notifier)
{
    public async Task<PromotionResponse> PromoteStudentsAsync(
        int schoolId,
        int currentGradeNumber,
        int currentAcademicYear,
        int nextAcademicYear,
        int semester = 2,
        decimal? passPercent = null)
    {
        if (semester != 2)
        {
            throw new ArgumentException("لا يمكن الترقية إلا في نهاية الفصل الدراسي الثاني");
        }

        // ✅ إذا لم يتم تمرير passPercent، اقرأه من قاعدة البيانات
        if (!passPercent.HasValue)
        {
            var markConfig = await db.MarkConfigs
                .FirstOrDefaultAsync(c => c.SchoolId == schoolId);
            passPercent = markConfig?.PassPercent ?? 50;
        }

        var currentGrade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == schoolId && 
                                      g.LocalGradeNumber == currentGradeNumber);

        if (currentGrade is null)
            throw new ArgumentException("الصف الحالي غير موجود");

        var students = await db.Students
            .Include(s => s.Section)
            .Where(s => s.SchoolId == schoolId && 
                        s.Section != null &&
                        s.Section.GradeId == currentGrade.Id &&
                        s.IsActive)
            .ToListAsync();

        if (!students.Any())
            return new PromotionResponse { Message = "لا يوجد طلاب للترقية" };

        var nextGrade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == schoolId && 
                                      g.LocalGradeNumber == currentGradeNumber + 1 );

        var promoted = new List<Student>();
        var failed = new List<Student>();
        var graduated = new List<Student>();
        var historyEntries = new List<StudentGradeHistory>();

        // ✅ استخدام معاملة لضمان تناسق البيانات
        using var transaction = await db.Database.BeginTransactionAsync();

        try
        {
            foreach (var student in students)
            {
                var average = await GetStudentFinalAverageAsync(student.Id);
                var passed = average >= passPercent.Value;

                // ✅ التحقق من عدم وجود سجل مكرر
                var existingHistory = await db.StudentGradeHistory
                    .AnyAsync(h => h.StudentId == student.Id && 
                                   h.AcademicYear == currentAcademicYear && 
                                   h.Semester == semester);

                if (!existingHistory)
                {
                    historyEntries.Add(new StudentGradeHistory
                    {
                        StudentId = student.Id,
                        GradeId = currentGrade.Id,
                        SectionId = student.SectionId ?? 0,
                        AcademicYear = currentAcademicYear,
                        Semester = semester,
                        IsPassed = passed,
                        Average = average,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (passed)
                {
                    if (nextGrade is not null)
                    {
                        var nextSection = await GetOrCreateSectionAsync(schoolId, nextGrade.Id);
                        
                        // ✅ التحقق من وجود الشعبة
                        var sectionExists = await db.Sections.AnyAsync(s => s.Id == nextSection.Id);
                        if (!sectionExists)
                        {
                            throw new Exception($"الشعبة {nextSection.Id} غير موجودة في قاعدة البيانات");
                        }
                        
                        student.SectionId = nextSection.Id;
                        promoted.Add(student);
                    }
                    else if (currentGradeNumber >= 12)
                    {
                        student.IsActive = false;
                        graduated.Add(student);
                    }
                    else
                    {
                        failed.Add(student);
                    }
                }
                else
                {
                    failed.Add(student);
                }
            }

            // ✅ حفظ سجل الترقية
            if (historyEntries.Any())
                db.StudentGradeHistory.AddRange(historyEntries);

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync();
            
            var innerMessage = ex.InnerException?.Message ?? ex.Message;
            
            if (innerMessage.Contains("duplicate key") || innerMessage.Contains("UK_StudentGradeHistory_StudentId_AcademicYear_Semester"))
                throw new Exception("تعارض في البيانات: الطالب مسجل بالفعل في هذه السنة والفصل الدراسي.");
            else if (innerMessage.Contains("NULL"))
                throw new Exception("بيانات ناقصة: تأكد من تعبئة جميع الحقول المطلوبة.");
            else if (innerMessage.Contains("foreign key"))
                throw new Exception("علاقة غير صحيحة: تأكد من وجود الشعبة والصف.");
            else
                throw new Exception($"خطأ في حفظ البيانات: {innerMessage}");
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }

        // ✅ إرسال الإشعارات بعد حفظ التغييرات بنجاح
        await SendNotificationsAsync(promoted, failed, graduated);

        return new PromotionResponse
        {
            Message = "تمت معالجة الترقية بنجاح",
            Statistics = new PromotionStatistics
            {
                Total = students.Count,
                Promoted = promoted.Count,
                Failed = failed.Count,
                Graduated = graduated.Count
            },
            Details = new PromotionDetails
            {
                CurrentGrade = currentGrade.Name,
                NextGrade = nextGrade?.Name ?? "لا يوجد",
                PromotedStudents = promoted.Select(s => new StudentBasicInfo { Id = s.Id, Name = s.Name }).ToList(),
                FailedStudents = failed.Select(s => new StudentFailInfo { Id = s.Id, Name = s.Name, SectionName = s.Section?.Name }).ToList(),
                GraduatedStudents = graduated.Select(s => new StudentBasicInfo { Id = s.Id, Name = s.Name }).ToList()
            }
        };
    }

    // ✅ جعل الدوال public للاستخدام من خارج الـ Service
    public async Task<decimal> GetStudentFinalAverageAsync(int studentId)
    {
        var marks = await db.Marks
            .Where(m => m.StudentId == studentId)
            .ToListAsync();

        return marks.Any() ? marks.Average(m => m.Total) : 0;
    }

    // ✅ جعل الدوال public للاستخدام من خارج الـ Service
    public async Task<decimal> GetStudentSemesterAverageAsync(int studentId, int semester)
    {
        var marks = await db.Marks
            .Where(m => m.StudentId == studentId && m.Semester == semester)
            .ToListAsync();

        return marks.Any() ? marks.Average(m => m.Total) : 0;
    }

    private async Task<Section> GetOrCreateSectionAsync(int schoolId, int gradeId)
    {
        var section = await db.Sections
            .FirstOrDefaultAsync(s => s.GradeId == gradeId);

        if (section is null)
        {
            var usedNumbers = await db.Sections
                .Where(s => s.GradeId == gradeId)
                .Select(s => s.LocalSectionNumber)
                .ToListAsync();

            int newLocalNumber = 1;
            while (usedNumbers.Contains(newLocalNumber)) newLocalNumber++;

            section = new Section
            {
                Name = $"الشعبة {GetSectionLetter(newLocalNumber)}",
                GradeId = gradeId,
                SchoolId = schoolId,
                LocalSectionNumber = newLocalNumber,
                CreatedAt = DateTime.UtcNow
            };

            db.Sections.Add(section);
            await db.SaveChangesAsync();
        }

        return section;
    }

    private string GetSectionLetter(int number) => number switch
    {
        1 => "أ",
        2 => "ب",
        3 => "ج",
        4 => "د",
        5 => "ه",
        6 => "و",
        7 => "ز",
        8 => "ح",
        9 => "ط",
        10 => "ي",
        _ => number.ToString()
    };

    private async Task SendNotificationsAsync(
        List<Student> promoted,
        List<Student> failed,
        List<Student> graduated)
    {
        // ✅ معالجة تسلسلية مع await لكل عملية
        foreach (var student in promoted)
        {
            await notifier.SendAsync(student.Id, UserType.Student,
                "تهانينا! لقد تم ترقيتك",
                "لقد نجحت وتم ترقيتك إلى الصف التالي",
                "promotion");
            await notifier.SendToGuardianAsync(student,
                "تهانينا! لقد تم ترقية ابنكم",
                $"لقد نجح {student.Name} وتم ترقيته إلى الصف التالي",
                "promotion");
        }

        foreach (var student in failed)
        {
            await notifier.SendAsync(student.Id, UserType.Student,
                "للأسف، لم تنجح هذا العام",
                "نتمنى لك التوفيق في العام القادم",
                "failure");
            await notifier.SendToGuardianAsync(student,
                "نتيجة ابنكم",
                $"للأسف، لم ينجح {student.Name} هذا العام",
                "failure");
        }

        foreach (var student in graduated)
        {
            await notifier.SendAsync(student.Id, UserType.Student,
                "🎓 ألف مبروك! لقد تخرجت!",
                "تهانينا على تخرجك من المدرسة",
                "graduation");
            await notifier.SendToGuardianAsync(student,
                "🎓 ألف مبروك! ابنكم تخرج!",
                $"تهانينا، لقد تخرج {student.Name} من المدرسة",
                "graduation");
        }
    }
}