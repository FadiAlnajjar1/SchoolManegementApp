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
        int semester = 2)
    {
        if (semester != 2)
        {
            throw new ArgumentException("لا يمكن الترقية إلا في نهاية الفصل الدراسي الثاني");
        }

        var markConfig = await db.MarkConfigs
            .FirstOrDefaultAsync(c => c.SchoolId == schoolId);
        
        var passPercent = markConfig?.PassPercent ?? 50;

        var currentGrade = await db.Grades
            .FirstOrDefaultAsync(g => g.SchoolId == schoolId && 
                                      g.LocalGradeNumber == currentGradeNumber &&
                                      g.AcademicYear == currentAcademicYear);

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
                                      g.LocalGradeNumber == currentGradeNumber + 1 &&
                                      g.AcademicYear == nextAcademicYear);

        var promoted = new List<Student>();
        var failed = new List<Student>();
        var graduated = new List<Student>();
        var historyEntries = new List<StudentGradeHistory>();

        foreach (var student in students)
        {
            // ✅ استخدام الدالة العامة
            var average = await GetStudentFinalAverageAsync(student.Id);
            var passed = average >= passPercent;

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

            if (passed)
            {
                if (nextGrade is not null)
                {
                    var nextSection = await GetOrCreateSectionAsync(schoolId, nextGrade.Id);
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

        await db.SaveChangesAsync();
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
        var tasks = new List<Task>();

        foreach (var student in promoted)
        {
            tasks.Add(notifier.SendAsync(student.Id, UserType.Student,
                "تهانينا! لقد تم ترقيتك",
                "لقد نجحت وتم ترقيتك إلى الصف التالي",
                "promotion"));
            tasks.Add(notifier.SendToGuardianAsync(student,
                "تهانينا! لقد تم ترقية ابنكم",
                $"لقد نجح {student.Name} وتم ترقيته إلى الصف التالي",
                "promotion"));
        }

        foreach (var student in failed)
        {
            tasks.Add(notifier.SendAsync(student.Id, UserType.Student,
                "للأسف، لم تنجح هذا العام",
                "نتمنى لك التوفيق في العام القادم",
                "failure"));
            tasks.Add(notifier.SendToGuardianAsync(student,
                "نتيجة ابنكم",
                $"للأسف، لم ينجح {student.Name} هذا العام",
                "failure"));
        }

        foreach (var student in graduated)
        {
            tasks.Add(notifier.SendAsync(student.Id, UserType.Student,
                "🎓 ألف مبروك! لقد تخرجت!",
                "تهانينا على تخرجك من المدرسة",
                "graduation"));
            tasks.Add(notifier.SendToGuardianAsync(student,
                "🎓 ألف مبروك! ابنكم تخرج!",
                $"تهانينا، لقد تخرج {student.Name} من المدرسة",
                "graduation"));
        }

        await Task.WhenAll(tasks);
    }
}