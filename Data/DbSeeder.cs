using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Data;

public static class DbSeeder
{
    // Admin ثابت
    private const string AdminName = "أدمن الوزارة";
    private const string AdminEmail = "admin@moe.sy";
    private const string AdminPassword = "123456";

    public static async Task SeedAsync(AppDbContext db)
    {
        // ============================================
        // 1. إنشاء المدير الثابت (إذا لم يكن موجوداً)
        // ============================================
        var admin = await db.Admins.FirstOrDefaultAsync(a => a.Email == AdminEmail);
        
        if (admin is null)
        {
            admin = new Admin
            {
                Name = "أدمن الوزارة",
                Email = AdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword),
                CreatedAt = DateTime.UtcNow
            };
            db.Admins.Add(admin);
            await db.SaveChangesAsync();
        }

        // التحقق من وجود مدارس
        if (await db.Schools.AnyAsync())
        {
            return;
        }

        string Hash(string p) => BCrypt.Net.BCrypt.HashPassword(p);
        string DefaultPassword = "123456";

        // ============================================
        // 2. إنشاء المدرسة الأولى (الرئيسية)
        // ============================================
        var school1 = new School
        {
            Name = "مدرسة دمشق الثانوية",
            Type = SchoolType.Secondary,
            Address = "دمشق - المزة",
            Phone = "0111234567",
            AdminId = admin.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.Schools.Add(school1);
        await db.SaveChangesAsync();

        db.MarkConfigs.Add(new MarkConfig { SchoolId = school1.Id });

        // ============================================
        // 3. إنشاء المدرسة الثانية
        // ============================================
        var school2 = new School
        {
            Name = "مدرسة حلب التجريبية",
            Type = SchoolType.Secondary,
            Address = "حلب - السبع بحرات",
            Phone = "0211234567",
            AdminId = admin.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.Schools.Add(school2);
        await db.SaveChangesAsync();

        db.MarkConfigs.Add(new MarkConfig { SchoolId = school2.Id });

        // ============================================
        // 4. إنشاء الموظفين
        // ============================================
        // مدير المدرسة
        var manager1 = new Employee 
        { 
            Name = "مدير المدرسة 1", 
            Email = "m1@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678901",
            CreatedAt = DateTime.UtcNow
        };

        // أمين سر
        var secretary1 = new Employee 
        { 
            Name = "أمين السر 1", 
            Email = "s1@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678903",
            CreatedAt = DateTime.UtcNow
        };

        // موجه
        var counselor1 = new Employee 
        { 
            Name = "الموجه 1", 
            Email = "c1@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "123456796967",
            CreatedAt = DateTime.UtcNow
        };

        // أمين مكتبة
        var librarian1 = new Employee 
        { 
            Name = "أمين المكتبة 1", 
            Email = "l1@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678907",
            CreatedAt = DateTime.UtcNow
        };

        // مشرف نشاطات
        var supervisor1 = new Employee 
        { 
            Name = "مشرف النشاطات 1", 
            Email = "a1@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678909",
            CreatedAt = DateTime.UtcNow
        };

        // معلمين
        var teacher1 = new Employee 
        { 
            Name = "معلم الرياضيات", 
            Email = "t1@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "1234567886",
            CreatedAt = DateTime.UtcNow
        };

        var teacher2 = new Employee 
        { 
            Name = "معلم اللغة العربية", 
            Email = "t2@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "1234564565",
            CreatedAt = DateTime.UtcNow
        };

        var teacher3 = new Employee 
        { 
            Name = "معلم العلوم", 
            Email = "t3@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678913",
            CreatedAt = DateTime.UtcNow
        };

        var teacher4 = new Employee 
        { 
            Name = "معلم اللغة الإنجليزية", 
            Email = "t4@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678914",
            CreatedAt = DateTime.UtcNow
        };

        var teacher5 = new Employee 
        { 
            Name = "معلم التاريخ", 
            Email = "t5@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678915",
            CreatedAt = DateTime.UtcNow
        };

        var teacher6 = new Employee 
        { 
            Name = "معلم الجغرافيا", 
            Email = "t6@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678916",
            CreatedAt = DateTime.UtcNow
        };

        // ============================================
        // 5. إضافة جميع الموظفين
        // ============================================
        db.Employees.AddRange(
            manager1, secretary1, counselor1, librarian1, supervisor1,
            teacher1, teacher2, teacher3, teacher4, teacher5, teacher6
        );
        await db.SaveChangesAsync();

        // ============================================
        // 6. ربط الموظفين بالمدرسة
        // ============================================
        db.EmployeeSchools.AddRange(
            new EmployeeSchool { EmployeeId = manager1.Id, SchoolId = school1.Id, LocalEmployeeNumber = 1, Role = EmployeeRole.Principal, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = secretary1.Id, SchoolId = school1.Id, LocalEmployeeNumber = 2, Role = EmployeeRole.Secretary, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = counselor1.Id, SchoolId = school1.Id, LocalEmployeeNumber = 3, Role = EmployeeRole.Counselor, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = librarian1.Id, SchoolId = school1.Id, LocalEmployeeNumber = 4, Role = EmployeeRole.Librarian, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = supervisor1.Id, SchoolId = school1.Id, LocalEmployeeNumber = 5, Role = EmployeeRole.ActivitySupervisor, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = teacher1.Id, SchoolId = school1.Id, LocalEmployeeNumber = 6, Role = EmployeeRole.Teacher, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = teacher2.Id, SchoolId = school1.Id, LocalEmployeeNumber = 7, Role = EmployeeRole.Teacher, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = teacher3.Id, SchoolId = school1.Id, LocalEmployeeNumber = 8, Role = EmployeeRole.Teacher, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = teacher4.Id, SchoolId = school1.Id, LocalEmployeeNumber = 9, Role = EmployeeRole.Teacher, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = teacher5.Id, SchoolId = school1.Id, LocalEmployeeNumber = 10, Role = EmployeeRole.Teacher, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = teacher6.Id, SchoolId = school1.Id, LocalEmployeeNumber = 11, Role = EmployeeRole.Teacher, IsActive = true, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        // ============================================
        // 7. إضافة TeacherAssignments للمعلمين
        // ============================================
        db.TeacherAssignments.AddRange(
            new TeacherAssignment { EmployeeId = teacher1.Id, SchoolId = school1.Id },
            new TeacherAssignment { EmployeeId = teacher2.Id, SchoolId = school1.Id },
            new TeacherAssignment { EmployeeId = teacher3.Id, SchoolId = school1.Id },
            new TeacherAssignment { EmployeeId = teacher4.Id, SchoolId = school1.Id },
            new TeacherAssignment { EmployeeId = teacher5.Id, SchoolId = school1.Id },
            new TeacherAssignment { EmployeeId = teacher6.Id, SchoolId = school1.Id }
        );
        await db.SaveChangesAsync();

        // ============================================
        // 8. إنشاء الصفوف (من 1 إلى 12)
        // ============================================
        var currentYear = DateTime.Now.Year;
        var grades = new List<Grade>();

        for (int level = 1; level <= 12; level++)
        {
            var gradeName = level switch
            {
                1 => "الصف الأول",
                2 => "الصف الثاني",
                3 => "الصف الثالث",
                4 => "الصف الرابع",
                5 => "الصف الخامس",
                6 => "الصف السادس",
                7 => "الصف السابع",
                8 => "الصف الثامن",
                9 => "الصف التاسع",
                10 => "الصف العاشر",
                11 => "الصف الحادي عشر",
                12 => "الصف الثاني عشر",
                _ => $"الصف {level}"
            };

            var grade = new Grade
            {
                SchoolId = school1.Id,
                Name = gradeName,
                Level = level,
                LocalGradeNumber = level,
                CreatedAt = DateTime.UtcNow
            };
            db.Grades.Add(grade);
            grades.Add(grade);
        }
        await db.SaveChangesAsync();

        // ============================================
        // 9. إنشاء الشعب لكل صف
        // ============================================
        var sections = new List<Section>();
        foreach (var grade in grades)
        {
            // كل صف له شعبتين (أ و ب)
            for (int i = 1; i <= 2; i++)
            {
                var section = new Section
                {
                    Name = $"الشعبة {(i == 1 ? "أ" : "ب")}",
                    GradeId = grade.Id,
                    SchoolId = school1.Id,
                    CounselorId = counselor1.Id,
                    LocalSectionNumber = i,
                    CreatedAt = DateTime.UtcNow
                };
                db.Sections.Add(section);
                sections.Add(section);
            }
        }
        await db.SaveChangesAsync();

        // ============================================
        // 10. إنشاء المواد (لجميع الصفوف)
        // ============================================
        var subjects = new List<Subject>
        {
            new Subject { Name = "الرياضيات", TeacherId = teacher1.Id, SchoolId = school1.Id, LocalSubjectId = 1 },
            new Subject { Name = "اللغة العربية", TeacherId = teacher2.Id, SchoolId = school1.Id, LocalSubjectId = 2 },
            new Subject { Name = "العلوم", TeacherId = teacher3.Id, SchoolId = school1.Id, LocalSubjectId = 3 },
            new Subject { Name = "اللغة الإنجليزية", TeacherId = teacher4.Id, SchoolId = school1.Id, LocalSubjectId = 4 },
            new Subject { Name = "التاريخ", TeacherId = teacher5.Id, SchoolId = school1.Id, LocalSubjectId = 5 },
            new Subject { Name = "الجغرافيا", TeacherId = teacher6.Id, SchoolId = school1.Id, LocalSubjectId = 6 }
        };
        db.Subjects.AddRange(subjects);
        await db.SaveChangesAsync();

        // ============================================
        // 11. ربط المعلمين بالمواد
        // ============================================
        db.TeacherSubjects.AddRange(
            new TeacherSubject { TeacherId = teacher1.Id, SubjectId = subjects[0].Id }, // رياضيات
            new TeacherSubject { TeacherId = teacher2.Id, SubjectId = subjects[1].Id }, // عربي
            new TeacherSubject { TeacherId = teacher3.Id, SubjectId = subjects[2].Id }, // علوم
            new TeacherSubject { TeacherId = teacher4.Id, SubjectId = subjects[3].Id }, // إنجليزي
            new TeacherSubject { TeacherId = teacher5.Id, SubjectId = subjects[4].Id }, // تاريخ
            new TeacherSubject { TeacherId = teacher6.Id, SubjectId = subjects[5].Id }  // جغرافيا
        );
        await db.SaveChangesAsync();

        // ============================================
        // 12. ربط المعلمين بالشعب
        // ============================================
        foreach (var section in sections)
        {
            foreach (var subject in subjects)
            {
                db.TeacherGrades.Add(new TeacherGrade
                {
                    TeacherId = subject.TeacherId ?? 0,
                    SubjectId = subject.Id,
                    SectionId = section.Id
                });
            }
        }
        await db.SaveChangesAsync();

        // ============================================
        // 13. إنشاء الطلاب (5 طلاب لكل صف)
        // ============================================
        var students = new List<Student>();
        var studentCounter = 1;

        var studentNames = new[]
        {
            "أحمد محمد", "ليلى خالد", "سامر علي", "نورا سعيد", "محمود حسن",
            "فاطمة علي", "حسن حسين", "زينب محمود", "عمر خالد", "منى سليم",
            "خالد عمر", "سارة أحمد", "محمد علي", "ريما ناصر", "حسام يوسف",
            "ناديا سامر", "باسل خضر", "هيا محمد", "رامي ناجي", "سوسن عادل"
        };

        var guardianNames = new[]
        {
            "محمد أحمد", "خالد يوسف", "علي سامر", "سعيد نورا", "حسن محمود",
            "علي فاطمة", "حسين حسن", "محمود زينب", "خالد عمر", "سليم منى"
        };

        var random = new Random();

        for (int gradeIndex = 0; gradeIndex < grades.Count; gradeIndex++)
        {
            var grade = grades[gradeIndex];
            var gradeSections = sections.Where(s => s.GradeId == grade.Id).ToList();

            // 5 طلاب لكل صف
            for (int i = 0; i < 5; i++)
            {
                var section = gradeSections[i % gradeSections.Count];
                var nameIndex = (gradeIndex * 5 + i) % studentNames.Length;
                var guardianIndex = (gradeIndex * 5 + i) % guardianNames.Length;

                var student = new Student
                {
                    Name = studentNames[nameIndex],
                    Email = $"s{studentCounter}@school.sy",
                    PasswordHash = Hash(DefaultPassword),
                    SchoolId = school1.Id,
                    SectionId = section.Id,
                    LocalStudentNumber = studentCounter,
                    GuardianName = guardianNames[guardianIndex],
                    GuardianPhone = $"099{studentCounter:D7}",
                    BloodType = new[] { "O+", "A+", "B+", "AB+", "O-" }[random.Next(5)],
                    BirthDate = new DateTime(2008 + random.Next(0, 8), random.Next(1, 13), random.Next(1, 28)),
                    Address = $"دمشق - شارع {random.Next(1, 50)}",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                students.Add(student);
                studentCounter++;
            }
        }
        db.Students.AddRange(students);
        await db.SaveChangesAsync();

        // ============================================
// 14. إنشاء العلامات للطلاب - جميع المواد الستة
// ============================================

// ✅ جلب جميع الطلاب بعد إضافتهم
var allStudents = await db.Students
    .Include(s => s.Section)
    .ThenInclude(sec => sec!.Grade)
    .Where(s => s.SchoolId == school1.Id)
    .OrderBy(s => s.LocalStudentNumber)
    .ToListAsync();

var marks = new List<Mark>();


// ✅ دالة مساعدة لإضافة علامة
void AddMark(int studentId, int subjectId, int semester, int academicYear,
             decimal oral, decimal quiz1, decimal quiz2, 
             decimal homework, decimal finalExam,
             decimal maxOral = 10, decimal maxQuiz1 = 10, 
             decimal maxQuiz2 = 10, decimal maxHomework = 10, 
             decimal maxFinalExam = 40)
{
    var total = oral + quiz1 + quiz2 + homework + finalExam;
    var maxTotal = maxOral + maxQuiz1 + maxQuiz2 + maxHomework + maxFinalExam;
    
    marks.Add(new Mark
    {
        StudentId = studentId,
        SubjectId = subjectId,
        Semester = semester,
        SchoolId = school1.Id,
        AcademicYear = academicYear, // ✅ السنة الدراسية
        
        // العلامات المكتسبة
        Oral = oral,
        Quiz1 = quiz1,
        Quiz2 = quiz2,
        Homework = homework,
        FinalExam = finalExam,
        Total = total,
        
        // العلامات الكاملة
        MaxOral = maxOral,
        MaxQuiz1 = maxQuiz1,
        MaxQuiz2 = maxQuiz2,
        MaxHomework = maxHomework,
        MaxFinalExam = maxFinalExam,
        
        Notes = $"علامات الفصل {semester} - السنة {academicYear}",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    });
}

// ============================================
// 1. أحمد محمد - الصف الأول - ناجح
// ============================================
var ahmed = allStudents.First(s => s.Name == "أحمد محمد" && s.LocalStudentNumber == 1);

// الفصل الأول - أحمد ناجح في جميع المواد (السنة الحالية)
AddMark(ahmed.Id, subjects[0].Id, 1, currentYear, 8, 9, 7, 8, 35); // الرياضيات - ناجح
AddMark(ahmed.Id, subjects[1].Id, 1, currentYear, 7, 8, 9, 7, 32); // اللغة العربية - ناجح
AddMark(ahmed.Id, subjects[2].Id, 1, currentYear, 9, 7, 8, 9, 36); // العلوم - ناجح
AddMark(ahmed.Id, subjects[3].Id, 1, currentYear, 6, 8, 7, 8, 30); // اللغة الإنجليزية - ناجح
AddMark(ahmed.Id, subjects[4].Id, 1, currentYear, 8, 9, 7, 8, 33); // التاريخ - ناجح
AddMark(ahmed.Id, subjects[5].Id, 1, currentYear, 7, 8, 9, 7, 31); // الجغرافيا - ناجح

// الفصل الثاني - أحمد ناجح ممتاز (السنة الحالية)
AddMark(ahmed.Id, subjects[0].Id, 2, currentYear, 9, 8, 9, 8, 38); // الرياضيات - ممتاز
AddMark(ahmed.Id, subjects[1].Id, 2, currentYear, 8, 9, 8, 9, 35); // اللغة العربية - ناجح
AddMark(ahmed.Id, subjects[2].Id, 2, currentYear, 9, 9, 8, 9, 37); // العلوم - ممتاز
AddMark(ahmed.Id, subjects[3].Id, 2, currentYear, 7, 8, 9, 8, 33); // اللغة الإنجليزية - ناجح
AddMark(ahmed.Id, subjects[4].Id, 2, currentYear, 8, 9, 8, 9, 36); // التاريخ - ممتاز
AddMark(ahmed.Id, subjects[5].Id, 2, currentYear, 7, 8, 9, 8, 32); // الجغرافيا - ناجح

// ============================================
// 2. ليلى خالد - الصف الأول - راسبة
// ============================================
var leila = allStudents.First(s => s.Name == "ليلى خالد" && s.LocalStudentNumber == 2);

// الفصل الأول - ليلى راسبة في جميع المواد (السنة الحالية)
AddMark(leila.Id, subjects[0].Id, 1, currentYear, 3, 4, 2, 3, 15); // الرياضيات - راسب
AddMark(leila.Id, subjects[1].Id, 1, currentYear, 4, 3, 5, 4, 18); // اللغة العربية - راسب
AddMark(leila.Id, subjects[2].Id, 1, currentYear, 5, 4, 3, 5, 20); // العلوم - راسب
AddMark(leila.Id, subjects[3].Id, 1, currentYear, 3, 5, 4, 3, 16); // اللغة الإنجليزية - راسب
AddMark(leila.Id, subjects[4].Id, 1, currentYear, 4, 3, 5, 4, 17); // التاريخ - راسب
AddMark(leila.Id, subjects[5].Id, 1, currentYear, 3, 5, 4, 3, 15); // الجغرافيا - راسب

// الفصل الثاني - ليلى راسبة (السنة الحالية)
AddMark(leila.Id, subjects[0].Id, 2, currentYear, 4, 3, 5, 4, 17); // الرياضيات - راسب
AddMark(leila.Id, subjects[1].Id, 2, currentYear, 3, 5, 4, 3, 15); // اللغة العربية - راسب
AddMark(leila.Id, subjects[2].Id, 2, currentYear, 5, 4, 3, 5, 19); // العلوم - راسب
AddMark(leila.Id, subjects[3].Id, 2, currentYear, 4, 3, 5, 4, 18); // اللغة الإنجليزية - راسب
AddMark(leila.Id, subjects[4].Id, 2, currentYear, 3, 5, 4, 3, 16); // التاريخ - راسب
AddMark(leila.Id, subjects[5].Id, 2, currentYear, 4, 3, 5, 4, 17); // الجغرافيا - راسب

// ============================================
// 3. سامر علي - الصف الأول - ناجح
// ============================================
var samer = allStudents.First(s => s.Name == "سامر علي" && s.LocalStudentNumber == 3);

// الفصل الأول - سامر ناجح (السنة الحالية)
AddMark(samer.Id, subjects[0].Id, 1, currentYear, 7, 8, 6, 7, 31); // الرياضيات - ناجح
AddMark(samer.Id, subjects[1].Id, 1, currentYear, 8, 7, 8, 9, 33); // اللغة العربية - ناجح
AddMark(samer.Id, subjects[2].Id, 1, currentYear, 6, 9, 7, 8, 29); // العلوم - ناجح
AddMark(samer.Id, subjects[3].Id, 1, currentYear, 8, 8, 7, 9, 34); // اللغة الإنجليزية - ناجح
AddMark(samer.Id, subjects[4].Id, 1, currentYear, 7, 8, 6, 8, 30); // التاريخ - ناجح
AddMark(samer.Id, subjects[5].Id, 1, currentYear, 8, 7, 8, 9, 32); // الجغرافيا - ناجح

// الفصل الثاني - سامر ناجح ممتاز (السنة الحالية)
AddMark(samer.Id, subjects[0].Id, 2, currentYear, 8, 9, 8, 9, 36); // الرياضيات - ممتاز
AddMark(samer.Id, subjects[1].Id, 2, currentYear, 9, 8, 9, 8, 37); // اللغة العربية - ممتاز
AddMark(samer.Id, subjects[2].Id, 2, currentYear, 7, 9, 8, 9, 35); // العلوم - ناجح
AddMark(samer.Id, subjects[3].Id, 2, currentYear, 8, 9, 7, 9, 36); // اللغة الإنجليزية - ممتاز
AddMark(samer.Id, subjects[4].Id, 2, currentYear, 9, 8, 9, 8, 35); // التاريخ - ناجح
AddMark(samer.Id, subjects[5].Id, 2, currentYear, 8, 9, 7, 9, 34); // الجغرافيا - ناجح

// ============================================
// 4. نورا سعيد - الصف الأول - راسبة
// ============================================
var noura = allStudents.First(s => s.Name == "نورا سعيد" && s.LocalStudentNumber == 4);

// الفصل الأول - نورا راسبة (السنة الحالية)
AddMark(noura.Id, subjects[0].Id, 1, currentYear, 2, 3, 4, 2, 12); // الرياضيات - راسب
AddMark(noura.Id, subjects[1].Id, 1, currentYear, 3, 2, 3, 4, 14); // اللغة العربية - راسب
AddMark(noura.Id, subjects[2].Id, 1, currentYear, 4, 3, 2, 3, 15); // العلوم - راسب
AddMark(noura.Id, subjects[3].Id, 1, currentYear, 2, 4, 3, 2, 13); // اللغة الإنجليزية - راسب
AddMark(noura.Id, subjects[4].Id, 1, currentYear, 3, 2, 4, 3, 14); // التاريخ - راسب
AddMark(noura.Id, subjects[5].Id, 1, currentYear, 2, 4, 3, 2, 12); // الجغرافيا - راسب

// الفصل الثاني - نورا راسبة (السنة الحالية)
AddMark(noura.Id, subjects[0].Id, 2, currentYear, 3, 4, 2, 3, 16); // الرياضيات - راسب
AddMark(noura.Id, subjects[1].Id, 2, currentYear, 4, 3, 4, 2, 17); // اللغة العربية - راسب
AddMark(noura.Id, subjects[2].Id, 2, currentYear, 2, 5, 3, 4, 15); // العلوم - راسب
AddMark(noura.Id, subjects[3].Id, 2, currentYear, 3, 4, 2, 3, 14); // اللغة الإنجليزية - راسب
AddMark(noura.Id, subjects[4].Id, 2, currentYear, 4, 3, 4, 2, 16); // التاريخ - راسب
AddMark(noura.Id, subjects[5].Id, 2, currentYear, 3, 4, 2, 3, 15); // الجغرافيا - راسب

// ============================================
// 5. محمود حسن - الصف الأول - ناجح ممتاز
// ============================================
var mahmoud = allStudents.First(s => s.Name == "محمود حسن" && s.LocalStudentNumber == 5);

// الفصل الأول - محمود ناجح ممتاز (السنة الحالية)
AddMark(mahmoud.Id, subjects[0].Id, 1, currentYear, 9, 8, 9, 8, 38); // الرياضيات - ممتاز
AddMark(mahmoud.Id, subjects[1].Id, 1, currentYear, 8, 9, 8, 9, 36); // اللغة العربية - ممتاز
AddMark(mahmoud.Id, subjects[2].Id, 1, currentYear, 9, 9, 8, 8, 37); // العلوم - ممتاز
AddMark(mahmoud.Id, subjects[3].Id, 1, currentYear, 8, 8, 9, 9, 35); // اللغة الإنجليزية - ممتاز
AddMark(mahmoud.Id, subjects[4].Id, 1, currentYear, 9, 8, 9, 8, 37); // التاريخ - ممتاز
AddMark(mahmoud.Id, subjects[5].Id, 1, currentYear, 8, 9, 8, 9, 36); // الجغرافيا - ممتاز

// الفصل الثاني - محمود ناجح ممتاز (السنة الحالية)
AddMark(mahmoud.Id, subjects[0].Id, 2, currentYear, 9, 9, 9, 8, 39); // الرياضيات - ممتاز
AddMark(mahmoud.Id, subjects[1].Id, 2, currentYear, 8, 9, 9, 9, 38); // اللغة العربية - ممتاز
AddMark(mahmoud.Id, subjects[2].Id, 2, currentYear, 9, 8, 9, 9, 37); // العلوم - ممتاز
AddMark(mahmoud.Id, subjects[3].Id, 2, currentYear, 9, 9, 8, 9, 39); // اللغة الإنجليزية - ممتاز
AddMark(mahmoud.Id, subjects[4].Id, 2, currentYear, 8, 9, 9, 8, 38); // التاريخ - ممتاز
AddMark(mahmoud.Id, subjects[5].Id, 2, currentYear, 9, 9, 8, 9, 39); // الجغرافيا - ممتاز

// ============================================
// 6. فاطمة علي - الصف الثاني - ناجحة
// ============================================
var fatima = allStudents.First(s => s.Name == "فاطمة علي" && s.LocalStudentNumber == 6);

// الفصل الأول - فاطمة ناجحة (السنة الحالية)
AddMark(fatima.Id, subjects[0].Id, 1, currentYear, 7, 8, 7, 8, 32); // الرياضيات - ناجح
AddMark(fatima.Id, subjects[1].Id, 1, currentYear, 8, 7, 8, 7, 33); // اللغة العربية - ناجح
AddMark(fatima.Id, subjects[2].Id, 1, currentYear, 7, 9, 8, 8, 34); // العلوم - ناجح
AddMark(fatima.Id, subjects[3].Id, 1, currentYear, 8, 8, 7, 9, 35); // اللغة الإنجليزية - ناجح
AddMark(fatima.Id, subjects[4].Id, 1, currentYear, 7, 8, 7, 8, 31); // التاريخ - ناجح
AddMark(fatima.Id, subjects[5].Id, 1, currentYear, 8, 7, 8, 9, 32); // الجغرافيا - ناجح

// الفصل الثاني - فاطمة ناجحة (السنة الحالية)
AddMark(fatima.Id, subjects[0].Id, 2, currentYear, 8, 9, 8, 9, 36); // الرياضيات - ناجح
AddMark(fatima.Id, subjects[1].Id, 2, currentYear, 9, 8, 9, 8, 35); // اللغة العربية - ناجح
AddMark(fatima.Id, subjects[2].Id, 2, currentYear, 8, 9, 9, 8, 37); // العلوم - ممتاز
AddMark(fatima.Id, subjects[3].Id, 2, currentYear, 9, 8, 8, 9, 36); // اللغة الإنجليزية - ناجح
AddMark(fatima.Id, subjects[4].Id, 2, currentYear, 8, 9, 8, 9, 35); // التاريخ - ناجح
AddMark(fatima.Id, subjects[5].Id, 2, currentYear, 9, 8, 9, 8, 36); // الجغرافيا - ناجح

// ============================================
// 7. حسن حسين - الصف الثاني - راسب
// ============================================
var hassan = allStudents.First(s => s.Name == "حسن حسين" && s.LocalStudentNumber == 7);

// الفصل الأول - حسن راسب (السنة الحالية)
AddMark(hassan.Id, subjects[0].Id, 1, currentYear, 3, 4, 2, 3, 14); // الرياضيات - راسب
AddMark(hassan.Id, subjects[1].Id, 1, currentYear, 4, 3, 5, 4, 16); // اللغة العربية - راسب
AddMark(hassan.Id, subjects[2].Id, 1, currentYear, 2, 5, 3, 4, 15); // العلوم - راسب
AddMark(hassan.Id, subjects[3].Id, 1, currentYear, 5, 3, 4, 2, 17); // اللغة الإنجليزية - راسب
AddMark(hassan.Id, subjects[4].Id, 1, currentYear, 3, 4, 2, 3, 13); // التاريخ - راسب
AddMark(hassan.Id, subjects[5].Id, 1, currentYear, 4, 3, 5, 4, 15); // الجغرافيا - راسب

// الفصل الثاني - حسن راسب (السنة الحالية)
AddMark(hassan.Id, subjects[0].Id, 2, currentYear, 4, 3, 5, 4, 18); // الرياضيات - راسب
AddMark(hassan.Id, subjects[1].Id, 2, currentYear, 3, 5, 4, 3, 16); // اللغة العربية - راسب
AddMark(hassan.Id, subjects[2].Id, 2, currentYear, 5, 4, 3, 5, 19); // العلوم - راسب
AddMark(hassan.Id, subjects[3].Id, 2, currentYear, 4, 3, 5, 4, 17); // اللغة الإنجليزية - راسب
AddMark(hassan.Id, subjects[4].Id, 2, currentYear, 3, 5, 4, 3, 15); // التاريخ - راسب
AddMark(hassan.Id, subjects[5].Id, 2, currentYear, 4, 3, 5, 4, 18); // الجغرافيا - راسب

// ============================================
// 8. زينب محمود - الصف الثاني - ناجحة ممتازة
// ============================================
var zainab = allStudents.First(s => s.Name == "زينب محمود" && s.LocalStudentNumber == 8);

// الفصل الأول - زينب ناجحة ممتازة (السنة الحالية)
AddMark(zainab.Id, subjects[0].Id, 1, currentYear, 8, 9, 8, 9, 37); // الرياضيات - ممتاز
AddMark(zainab.Id, subjects[1].Id, 1, currentYear, 9, 8, 9, 8, 36); // اللغة العربية - ممتاز
AddMark(zainab.Id, subjects[2].Id, 1, currentYear, 8, 9, 9, 8, 38); // العلوم - ممتاز
AddMark(zainab.Id, subjects[3].Id, 1, currentYear, 9, 8, 8, 9, 37); // اللغة الإنجليزية - ممتاز
AddMark(zainab.Id, subjects[4].Id, 1, currentYear, 8, 9, 8, 9, 36); // التاريخ - ممتاز
AddMark(zainab.Id, subjects[5].Id, 1, currentYear, 9, 8, 9, 8, 37); // الجغرافيا - ممتاز

// الفصل الثاني - زينب ناجحة ممتازة (السنة الحالية)
AddMark(zainab.Id, subjects[0].Id, 2, currentYear, 9, 9, 9, 9, 39); // الرياضيات - ممتاز
AddMark(zainab.Id, subjects[1].Id, 2, currentYear, 8, 9, 9, 9, 38); // اللغة العربية - ممتاز
AddMark(zainab.Id, subjects[2].Id, 2, currentYear, 9, 8, 9, 9, 39); // العلوم - ممتاز
AddMark(zainab.Id, subjects[3].Id, 2, currentYear, 9, 9, 8, 9, 40); // اللغة الإنجليزية - ممتاز
AddMark(zainab.Id, subjects[4].Id, 2, currentYear, 8, 9, 9, 8, 38); // التاريخ - ممتاز
AddMark(zainab.Id, subjects[5].Id, 2, currentYear, 9, 8, 9, 9, 39); // الجغرافيا - ممتاز

// ============================================
// 9. عمر خالد - الصف الثاني - ناجح
// ============================================
var omar = allStudents.First(s => s.Name == "عمر خالد" && s.LocalStudentNumber == 9);

// الفصل الأول - عمر ناجح (السنة الحالية)
AddMark(omar.Id, subjects[0].Id, 1, currentYear, 7, 8, 7, 8, 31); // الرياضيات - ناجح
AddMark(omar.Id, subjects[1].Id, 1, currentYear, 8, 7, 8, 7, 32); // اللغة العربية - ناجح
AddMark(omar.Id, subjects[2].Id, 1, currentYear, 7, 8, 9, 8, 33); // العلوم - ناجح
AddMark(omar.Id, subjects[3].Id, 1, currentYear, 8, 7, 8, 9, 34); // اللغة الإنجليزية - ناجح
AddMark(omar.Id, subjects[4].Id, 1, currentYear, 7, 8, 7, 8, 30); // التاريخ - ناجح
AddMark(omar.Id, subjects[5].Id, 1, currentYear, 8, 7, 8, 9, 33); // الجغرافيا - ناجح

// الفصل الثاني - عمر ناجح (السنة الحالية)
AddMark(omar.Id, subjects[0].Id, 2, currentYear, 8, 9, 8, 9, 35); // الرياضيات - ناجح
AddMark(omar.Id, subjects[1].Id, 2, currentYear, 9, 8, 9, 8, 36); // اللغة العربية - ناجح
AddMark(omar.Id, subjects[2].Id, 2, currentYear, 8, 9, 8, 9, 37); // العلوم - ناجح
AddMark(omar.Id, subjects[3].Id, 2, currentYear, 9, 8, 9, 8, 35); // اللغة الإنجليزية - ناجح
AddMark(omar.Id, subjects[4].Id, 2, currentYear, 8, 9, 8, 9, 36); // التاريخ - ناجح
AddMark(omar.Id, subjects[5].Id, 2, currentYear, 9, 8, 9, 8, 34); // الجغرافيا - ناجح

// ============================================
// 10. منى سليم - الصف الثاني - راسبة
// ============================================
var mona = allStudents.First(s => s.Name == "منى سليم" && s.LocalStudentNumber == 10);

// الفصل الأول - منى راسبة (السنة الحالية)
AddMark(mona.Id, subjects[0].Id, 1, currentYear, 2, 3, 4, 2, 11); // الرياضيات - راسب
AddMark(mona.Id, subjects[1].Id, 1, currentYear, 3, 2, 3, 4, 13); // اللغة العربية - راسب
AddMark(mona.Id, subjects[2].Id, 1, currentYear, 4, 3, 2, 3, 14); // العلوم - راسب
AddMark(mona.Id, subjects[3].Id, 1, currentYear, 2, 4, 3, 2, 12); // اللغة الإنجليزية - راسب
AddMark(mona.Id, subjects[4].Id, 1, currentYear, 3, 2, 4, 3, 13); // التاريخ - راسب
AddMark(mona.Id, subjects[5].Id, 1, currentYear, 2, 4, 3, 2, 11); // الجغرافيا - راسب

// الفصل الثاني - منى راسبة (السنة الحالية)
AddMark(mona.Id, subjects[0].Id, 2, currentYear, 3, 4, 2, 3, 15); // الرياضيات - راسب
AddMark(mona.Id, subjects[1].Id, 2, currentYear, 4, 3, 4, 2, 16); // اللغة العربية - راسب
AddMark(mona.Id, subjects[2].Id, 2, currentYear, 2, 5, 3, 4, 14); // العلوم - راسب
AddMark(mona.Id, subjects[3].Id, 2, currentYear, 3, 4, 2, 3, 15); // اللغة الإنجليزية - راسب
AddMark(mona.Id, subjects[4].Id, 2, currentYear, 4, 3, 4, 2, 16); // التاريخ - راسب
AddMark(mona.Id, subjects[5].Id, 2, currentYear, 3, 4, 2, 3, 14); // الجغرافيا - راسب

// ============================================
// 11. خالد عمر - الصف الثالث - ناجح
// ============================================
var khaled = allStudents.First(s => s.Name == "خالد عمر" && s.LocalStudentNumber == 11);

// الفصل الأول - خالد ناجح (السنة الحالية)
AddMark(khaled.Id, subjects[0].Id, 1, currentYear, 8, 7, 8, 9, 34); // الرياضيات - ناجح
AddMark(khaled.Id, subjects[1].Id, 1, currentYear, 7, 8, 9, 8, 33); // اللغة العربية - ناجح
AddMark(khaled.Id, subjects[2].Id, 1, currentYear, 9, 8, 7, 8, 35); // العلوم - ناجح
AddMark(khaled.Id, subjects[3].Id, 1, currentYear, 8, 9, 8, 7, 34); // اللغة الإنجليزية - ناجح
AddMark(khaled.Id, subjects[4].Id, 1, currentYear, 7, 8, 9, 8, 32); // التاريخ - ناجح
AddMark(khaled.Id, subjects[5].Id, 1, currentYear, 8, 9, 8, 7, 33); // الجغرافيا - ناجح

// الفصل الثاني - خالد ناجح ممتاز (السنة الحالية)
AddMark(khaled.Id, subjects[0].Id, 2, currentYear, 9, 8, 9, 8, 37); // الرياضيات - ممتاز
AddMark(khaled.Id, subjects[1].Id, 2, currentYear, 8, 9, 8, 9, 36); // اللغة العربية - ممتاز
AddMark(khaled.Id, subjects[2].Id, 2, currentYear, 9, 8, 9, 9, 38); // العلوم - ممتاز
AddMark(khaled.Id, subjects[3].Id, 2, currentYear, 8, 9, 8, 9, 37); // اللغة الإنجليزية - ممتاز
AddMark(khaled.Id, subjects[4].Id, 2, currentYear, 9, 8, 9, 8, 36); // التاريخ - ممتاز
AddMark(khaled.Id, subjects[5].Id, 2, currentYear, 8, 9, 8, 9, 35); // الجغرافيا - ناجح

// ============================================
// 12. سارة أحمد - الصف الثالث - راسبة
// ============================================
var sara = allStudents.First(s => s.Name == "سارة أحمد" && s.LocalStudentNumber == 12);

// الفصل الأول - سارة راسبة (السنة الحالية)
AddMark(sara.Id, subjects[0].Id, 1, currentYear, 3, 4, 2, 3, 13); // الرياضيات - راسب
AddMark(sara.Id, subjects[1].Id, 1, currentYear, 4, 3, 5, 4, 15); // اللغة العربية - راسب
AddMark(sara.Id, subjects[2].Id, 1, currentYear, 2, 5, 3, 4, 14); // العلوم - راسب
AddMark(sara.Id, subjects[3].Id, 1, currentYear, 5, 3, 4, 2, 16); // اللغة الإنجليزية - راسب
AddMark(sara.Id, subjects[4].Id, 1, currentYear, 3, 4, 2, 3, 12); // التاريخ - راسب
AddMark(sara.Id, subjects[5].Id, 1, currentYear, 4, 3, 5, 4, 15); // الجغرافيا - راسب

// الفصل الثاني - سارة راسبة (السنة الحالية)
AddMark(sara.Id, subjects[0].Id, 2, currentYear, 4, 3, 5, 4, 17); // الرياضيات - راسب
AddMark(sara.Id, subjects[1].Id, 2, currentYear, 3, 5, 4, 3, 18); // اللغة العربية - راسب
AddMark(sara.Id, subjects[2].Id, 2, currentYear, 5, 4, 3, 5, 19); // العلوم - راسب
AddMark(sara.Id, subjects[3].Id, 2, currentYear, 4, 3, 5, 4, 16); // اللغة الإنجليزية - راسب
AddMark(sara.Id, subjects[4].Id, 2, currentYear, 3, 5, 4, 3, 17); // التاريخ - راسب
AddMark(sara.Id, subjects[5].Id, 2, currentYear, 4, 3, 5, 4, 18); // الجغرافيا - راسب

// ============================================
// 13. محمد علي - الصف الثالث - ناجح ممتاز
// ============================================
var mohammad = allStudents.First(s => s.Name == "محمد علي" && s.LocalStudentNumber == 13);

// الفصل الأول - محمد ناجح ممتاز (السنة الحالية)
AddMark(mohammad.Id, subjects[0].Id, 1, currentYear, 9, 8, 9, 8, 38); // الرياضيات - ممتاز
AddMark(mohammad.Id, subjects[1].Id, 1, currentYear, 8, 9, 8, 9, 37); // اللغة العربية - ممتاز
AddMark(mohammad.Id, subjects[2].Id, 1, currentYear, 9, 8, 9, 8, 36); // العلوم - ممتاز
AddMark(mohammad.Id, subjects[3].Id, 1, currentYear, 8, 9, 8, 9, 38); // اللغة الإنجليزية - ممتاز
AddMark(mohammad.Id, subjects[4].Id, 1, currentYear, 9, 8, 9, 8, 37); // التاريخ - ممتاز
AddMark(mohammad.Id, subjects[5].Id, 1, currentYear, 8, 9, 8, 9, 36); // الجغرافيا - ممتاز

// الفصل الثاني - محمد ناجح ممتاز (السنة الحالية)
AddMark(mohammad.Id, subjects[0].Id, 2, currentYear, 9, 9, 9, 8, 40); // الرياضيات - ممتاز
AddMark(mohammad.Id, subjects[1].Id, 2, currentYear, 8, 9, 9, 9, 39); // اللغة العربية - ممتاز
AddMark(mohammad.Id, subjects[2].Id, 2, currentYear, 9, 8, 9, 9, 38); // العلوم - ممتاز
AddMark(mohammad.Id, subjects[3].Id, 2, currentYear, 9, 9, 8, 9, 39); // اللغة الإنجليزية - ممتاز
AddMark(mohammad.Id, subjects[4].Id, 2, currentYear, 8, 9, 9, 8, 37); // التاريخ - ممتاز
AddMark(mohammad.Id, subjects[5].Id, 2, currentYear, 9, 8, 9, 9, 38); // الجغرافيا - ممتاز

// ============================================
// 14. ريما ناصر - الصف الثالث - ناجحة
// ============================================
var rima = allStudents.First(s => s.Name == "ريما ناصر" && s.LocalStudentNumber == 14);

// الفصل الأول - ريما ناجحة (السنة الحالية)
AddMark(rima.Id, subjects[0].Id, 1, currentYear, 7, 8, 7, 8, 32); // الرياضيات - ناجح
AddMark(rima.Id, subjects[1].Id, 1, currentYear, 8, 7, 8, 7, 31); // اللغة العربية - ناجح
AddMark(rima.Id, subjects[2].Id, 1, currentYear, 7, 8, 9, 8, 33); // العلوم - ناجح
AddMark(rima.Id, subjects[3].Id, 1, currentYear, 8, 7, 8, 9, 34); // اللغة الإنجليزية - ناجح
AddMark(rima.Id, subjects[4].Id, 1, currentYear, 7, 8, 7, 8, 31); // التاريخ - ناجح
AddMark(rima.Id, subjects[5].Id, 1, currentYear, 8, 7, 8, 9, 32); // الجغرافيا - ناجح

// الفصل الثاني - ريما ناجحة (السنة الحالية)
AddMark(rima.Id, subjects[0].Id, 2, currentYear, 8, 9, 8, 9, 36); // الرياضيات - ناجح
AddMark(rima.Id, subjects[1].Id, 2, currentYear, 9, 8, 9, 8, 35); // اللغة العربية - ناجح
AddMark(rima.Id, subjects[2].Id, 2, currentYear, 8, 9, 9, 8, 37); // العلوم - ناجح
AddMark(rima.Id, subjects[3].Id, 2, currentYear, 9, 8, 8, 9, 36); // اللغة الإنجليزية - ناجح
AddMark(rima.Id, subjects[4].Id, 2, currentYear, 8, 9, 8, 9, 35); // التاريخ - ناجح
AddMark(rima.Id, subjects[5].Id, 2, currentYear, 9, 8, 9, 8, 36); // الجغرافيا - ناجح

// ============================================
// 15. حسام يوسف - الصف الثالث - راسب
// ============================================
var hossam = allStudents.First(s => s.Name == "حسام يوسف" && s.LocalStudentNumber == 15);

// الفصل الأول - حسام راسب (السنة الحالية)
AddMark(hossam.Id, subjects[0].Id, 1, currentYear, 2, 3, 4, 2, 10); // الرياضيات - راسب
AddMark(hossam.Id, subjects[1].Id, 1, currentYear, 3, 2, 3, 4, 12); // اللغة العربية - راسب
AddMark(hossam.Id, subjects[2].Id, 1, currentYear, 4, 3, 2, 3, 13); // العلوم - راسب
AddMark(hossam.Id, subjects[3].Id, 1, currentYear, 2, 4, 3, 2, 11); // اللغة الإنجليزية - راسب
AddMark(hossam.Id, subjects[4].Id, 1, currentYear, 3, 2, 4, 3, 12); // التاريخ - راسب
AddMark(hossam.Id, subjects[5].Id, 1, currentYear, 2, 4, 3, 2, 10); // الجغرافيا - راسب

// الفصل الثاني - حسام راسب (السنة الحالية)
AddMark(hossam.Id, subjects[0].Id, 2, currentYear, 3, 4, 2, 3, 14); // الرياضيات - راسب
AddMark(hossam.Id, subjects[1].Id, 2, currentYear, 4, 3, 4, 2, 15); // اللغة العربية - راسب
AddMark(hossam.Id, subjects[2].Id, 2, currentYear, 2, 5, 3, 4, 13); // العلوم - راسب
AddMark(hossam.Id, subjects[3].Id, 2, currentYear, 3, 4, 2, 3, 14); // اللغة الإنجليزية - راسب
AddMark(hossam.Id, subjects[4].Id, 2, currentYear, 4, 3, 4, 2, 15); // التاريخ - راسب
AddMark(hossam.Id, subjects[5].Id, 2, currentYear, 3, 4, 2, 3, 13); // الجغرافيا - راسب

// ============================================
// 16. ناديا سامر - الصف الرابع - ناجحة
// ============================================
var nadia = allStudents.First(s => s.Name == "ناديا سامر" && s.LocalStudentNumber == 16);

// الفصل الأول - ناديا ناجحة (السنة الحالية)
AddMark(nadia.Id, subjects[0].Id, 1, currentYear, 8, 7, 8, 7, 33); // الرياضيات - ناجح
AddMark(nadia.Id, subjects[1].Id, 1, currentYear, 7, 8, 7, 8, 32); // اللغة العربية - ناجح
AddMark(nadia.Id, subjects[2].Id, 1, currentYear, 8, 7, 9, 8, 34); // العلوم - ناجح
AddMark(nadia.Id, subjects[3].Id, 1, currentYear, 7, 8, 8, 7, 31); // اللغة الإنجليزية - ناجح
AddMark(nadia.Id, subjects[4].Id, 1, currentYear, 8, 7, 8, 7, 32); // التاريخ - ناجح
AddMark(nadia.Id, subjects[5].Id, 1, currentYear, 7, 8, 8, 7, 30); // الجغرافيا - ناجح

// الفصل الثاني - ناديا ناجحة ممتازة (السنة الحالية)
AddMark(nadia.Id, subjects[0].Id, 2, currentYear, 9, 8, 9, 8, 37); // الرياضيات - ممتاز
AddMark(nadia.Id, subjects[1].Id, 2, currentYear, 8, 9, 8, 9, 36); // اللغة العربية - ناجح
AddMark(nadia.Id, subjects[2].Id, 2, currentYear, 9, 8, 9, 9, 38); // العلوم - ممتاز
AddMark(nadia.Id, subjects[3].Id, 2, currentYear, 8, 9, 8, 9, 37); // اللغة الإنجليزية - ممتاز
AddMark(nadia.Id, subjects[4].Id, 2, currentYear, 9, 8, 9, 8, 36); // التاريخ - ناجح
AddMark(nadia.Id, subjects[5].Id, 2, currentYear, 8, 9, 8, 9, 35); // الجغرافيا - ناجح

// ============================================
// 17. باسل خضر - الصف الرابع - راسب
// ============================================
var basel = allStudents.First(s => s.Name == "باسل خضر" && s.LocalStudentNumber == 17);

// الفصل الأول - باسل راسب (السنة الحالية)
AddMark(basel.Id, subjects[0].Id, 1, currentYear, 3, 4, 2, 3, 12); // الرياضيات - راسب
AddMark(basel.Id, subjects[1].Id, 1, currentYear, 4, 3, 5, 4, 14); // اللغة العربية - راسب
AddMark(basel.Id, subjects[2].Id, 1, currentYear, 2, 5, 3, 4, 13); // العلوم - راسب
AddMark(basel.Id, subjects[3].Id, 1, currentYear, 5, 3, 4, 2, 15); // اللغة الإنجليزية - راسب
AddMark(basel.Id, subjects[4].Id, 1, currentYear, 3, 4, 2, 3, 11); // التاريخ - راسب
AddMark(basel.Id, subjects[5].Id, 1, currentYear, 4, 3, 5, 4, 14); // الجغرافيا - راسب

// الفصل الثاني - باسل راسب (السنة الحالية)
AddMark(basel.Id, subjects[0].Id, 2, currentYear, 4, 3, 5, 4, 16); // الرياضيات - راسب
AddMark(basel.Id, subjects[1].Id, 2, currentYear, 3, 5, 4, 3, 17); // اللغة العربية - راسب
AddMark(basel.Id, subjects[2].Id, 2, currentYear, 5, 4, 3, 5, 18); // العلوم - راسب
AddMark(basel.Id, subjects[3].Id, 2, currentYear, 4, 3, 5, 4, 15); // اللغة الإنجليزية - راسب
AddMark(basel.Id, subjects[4].Id, 2, currentYear, 3, 5, 4, 3, 16); // التاريخ - راسب
AddMark(basel.Id, subjects[5].Id, 2, currentYear, 4, 3, 5, 4, 17); // الجغرافيا - راسب

// ============================================
// 18. هيا محمد - الصف الرابع - ناجحة ممتازة
// ============================================
var haya = allStudents.First(s => s.Name == "هيا محمد" && s.LocalStudentNumber == 18);

// الفصل الأول - هيا ناجحة ممتازة (السنة الحالية)
AddMark(haya.Id, subjects[0].Id, 1, currentYear, 9, 8, 9, 8, 39); // الرياضيات - ممتاز
AddMark(haya.Id, subjects[1].Id, 1, currentYear, 8, 9, 8, 9, 38); // اللغة العربية - ممتاز
AddMark(haya.Id, subjects[2].Id, 1, currentYear, 9, 8, 9, 8, 37); // العلوم - ممتاز
AddMark(haya.Id, subjects[3].Id, 1, currentYear, 8, 9, 8, 9, 36); // اللغة الإنجليزية - ممتاز
AddMark(haya.Id, subjects[4].Id, 1, currentYear, 9, 8, 9, 8, 38); // التاريخ - ممتاز
AddMark(haya.Id, subjects[5].Id, 1, currentYear, 8, 9, 8, 9, 37); // الجغرافيا - ممتاز

// الفصل الثاني - هيا ناجحة ممتازة (السنة الحالية)
AddMark(haya.Id, subjects[0].Id, 2, currentYear, 9, 9, 9, 9, 40); // الرياضيات - ممتاز
AddMark(haya.Id, subjects[1].Id, 2, currentYear, 8, 9, 9, 9, 39); // اللغة العربية - ممتاز
AddMark(haya.Id, subjects[2].Id, 2, currentYear, 9, 8, 9, 9, 40); // العلوم - ممتاز
AddMark(haya.Id, subjects[3].Id, 2, currentYear, 9, 9, 8, 9, 38); // اللغة الإنجليزية - ممتاز
AddMark(haya.Id, subjects[4].Id, 2, currentYear, 8, 9, 9, 8, 39); // التاريخ - ممتاز
AddMark(haya.Id, subjects[5].Id, 2, currentYear, 9, 8, 9, 9, 40); // الجغرافيا - ممتاز

// ============================================
// 19. رامي ناجي - الصف الرابع - ناجح
// ============================================
var rami = allStudents.First(s => s.Name == "رامي ناجي" && s.LocalStudentNumber == 19);

// الفصل الأول - رامي ناجح (السنة الحالية)
AddMark(rami.Id, subjects[0].Id, 1, currentYear, 7, 8, 7, 8, 32); // الرياضيات - ناجح
AddMark(rami.Id, subjects[1].Id, 1, currentYear, 8, 7, 8, 7, 33); // اللغة العربية - ناجح
AddMark(rami.Id, subjects[2].Id, 1, currentYear, 7, 8, 9, 8, 34); // العلوم - ناجح
AddMark(rami.Id, subjects[3].Id, 1, currentYear, 8, 7, 8, 9, 35); // اللغة الإنجليزية - ناجح
AddMark(rami.Id, subjects[4].Id, 1, currentYear, 7, 8, 7, 8, 31); // التاريخ - ناجح
AddMark(rami.Id, subjects[5].Id, 1, currentYear, 8, 7, 8, 9, 32); // الجغرافيا - ناجح

// الفصل الثاني - رامي ناجح ممتاز (السنة الحالية)
AddMark(rami.Id, subjects[0].Id, 2, currentYear, 8, 9, 8, 9, 36); // الرياضيات - ناجح
AddMark(rami.Id, subjects[1].Id, 2, currentYear, 9, 8, 9, 8, 37); // اللغة العربية - ممتاز
AddMark(rami.Id, subjects[2].Id, 2, currentYear, 8, 9, 9, 8, 38); // العلوم - ممتاز
AddMark(rami.Id, subjects[3].Id, 2, currentYear, 9, 8, 8, 9, 37); // اللغة الإنجليزية - ممتاز
AddMark(rami.Id, subjects[4].Id, 2, currentYear, 8, 9, 8, 9, 35); // التاريخ - ناجح
AddMark(rami.Id, subjects[5].Id, 2, currentYear, 9, 8, 9, 8, 36); // الجغرافيا - ناجح

// ============================================
// 20. سوسن عادل - الصف الرابع - راسبة
// ============================================
var sowsan = allStudents.First(s => s.Name == "سوسن عادل" && s.LocalStudentNumber == 20);

// الفصل الأول - سوسن راسبة (السنة الحالية)
AddMark(sowsan.Id, subjects[0].Id, 1, currentYear, 2, 3, 4, 2, 11); // الرياضيات - راسب
AddMark(sowsan.Id, subjects[1].Id, 1, currentYear, 3, 2, 3, 4, 12); // اللغة العربية - راسب
AddMark(sowsan.Id, subjects[2].Id, 1, currentYear, 4, 3, 2, 3, 13); // العلوم - راسب
AddMark(sowsan.Id, subjects[3].Id, 1, currentYear, 2, 4, 3, 2, 10); // اللغة الإنجليزية - راسب
AddMark(sowsan.Id, subjects[4].Id, 1, currentYear, 3, 2, 4, 3, 11); // التاريخ - راسب
AddMark(sowsan.Id, subjects[5].Id, 1, currentYear, 2, 4, 3, 2, 10); // الجغرافيا - راسب

// الفصل الثاني - سوسن راسبة (السنة الحالية)
AddMark(sowsan.Id, subjects[0].Id, 2, currentYear, 3, 4, 2, 3, 14); // الرياضيات - راسب
AddMark(sowsan.Id, subjects[1].Id, 2, currentYear, 4, 3, 4, 2, 15); // اللغة العربية - راسب
AddMark(sowsan.Id, subjects[2].Id, 2, currentYear, 2, 5, 3, 4, 13); // العلوم - راسب
AddMark(sowsan.Id, subjects[3].Id, 2, currentYear, 3, 4, 2, 3, 12); // اللغة الإنجليزية - راسب
AddMark(sowsan.Id, subjects[4].Id, 2, currentYear, 4, 3, 4, 2, 14); // التاريخ - راسب
AddMark(sowsan.Id, subjects[5].Id, 2, currentYear, 3, 4, 2, 3, 13); // الجغرافيا - راسب

// ============================================
// حفظ جميع العلامات
// ============================================
db.Marks.AddRange(marks);
await db.SaveChangesAsync();

Console.WriteLine($"✅ تم إضافة {marks.Count} علامة لـ {allStudents.Count} طالب");
Console.WriteLine($"📊 السنة الدراسية: {currentYear}");
Console.WriteLine($"📚 كل طالب لديه {subjects.Count * 2} علامة (6 مواد × فصلين)");



Console.WriteLine($"✅ تم إضافة {marks.Count} علامة لـ {allStudents.Count} طالب");
Console.WriteLine($"📊 كل طالب لديه {subjects.Count * 2} علامة (6 مواد × فصلين)");

Console.WriteLine($"✅ تم إضافة {marks.Count} علامة لـ {allStudents.Count} طالب");

        // ============================================
        // 15. إنشاء الكتب
        // ============================================
        db.Books.AddRange(
            new Book { SchoolId = school1.Id, LocalBookNumber = 1, Title = "الأيام", Author = "طه حسين", Copies = 5, AvailableCopies = 5 },
            new Book { SchoolId = school1.Id, LocalBookNumber = 2, Title = "النحو الواضح", Author = "علي الجارم", Copies = 3, AvailableCopies = 3 },
            new Book { SchoolId = school1.Id, LocalBookNumber = 3, Title = "فيزياء الصف العاشر", Author = "أحمد زكي", Copies = 4, AvailableCopies = 4 },
            new Book { SchoolId = school1.Id, LocalBookNumber = 4, Title = "الكيمياء", Author = "مصطفى فهمي", Copies = 3, AvailableCopies = 3 },
            new Book { SchoolId = school1.Id, LocalBookNumber = 5, Title = "الأحياء", Author = "عبد الوهاب", Copies = 3, AvailableCopies = 3 }
        );
        await db.SaveChangesAsync();

        // ============================================
        // 16. إنشاء الأنشطة
        // ============================================
        db.Activities.AddRange(
            new Activity { SchoolId = school1.Id, LocalActivityId = 1, Title = "رحلة إلى تدمر", Description = "رحلة علمية إلى تدمر", CreatedAt = DateTime.UtcNow },
            new Activity { SchoolId = school1.Id, LocalActivityId = 2, Title = "مسابقة الرياضيات", Description = "مسابقة الرياضيات بين الطلاب", CreatedAt = DateTime.UtcNow },
            new Activity { SchoolId = school1.Id, LocalActivityId = 3, Title = "ورشة الفنون", Description = "ورشة فنية للرسم والنحت", CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        // ============================================
        // 17. إنشاء الإعلانات
        // ============================================
        db.Announcements.AddRange(
            new Announcement 
            { 
                SchoolId = school1.Id,
                LocalAnnouncementId = 1,
                Title = "بدء العام الدراسي", 
                Body = "يبدأ العام الدراسي الجديد يوم الأحد القادم",
                Type = AnnouncementType.General,
                Audience = AnnouncementAudience.All,
                CreatedById = manager1.Id,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            },
            new Announcement 
            { 
                SchoolId = school1.Id,
                LocalAnnouncementId = 2,
                Title = "موعد الامتحانات", 
                Body = "تبدأ الامتحانات النهائية يوم 15 يناير",
                Audience = AnnouncementAudience.Students,
                CreatedById = secretary1.Id,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            },
            new Announcement 
            { 
                SchoolId = school1.Id,
                LocalAnnouncementId = 3,
                Title = "اجتماع المعلمين", 
                Body = "اجتماع المعلمين يوم الأربعاء القادم",
                Audience = AnnouncementAudience.Teachers,
                CreatedById = manager1.Id,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }
        );
        await db.SaveChangesAsync();

        // ============================================
        // 18. إضافة بعض الحجوزات والإعارات للطلاب
        // ============================================
        var bookLoans = new List<BookLoan>();
        var bookReservations = new List<BookReservation>();

        for (int i = 0; i < 10; i++)
        {
            var student = students[i % students.Count];
            var book = await db.Books.FirstOrDefaultAsync(b => b.SchoolId == school1.Id);

            if (book == null) continue;

            // إعارة نشطة
            bookLoans.Add(new BookLoan
            {
                BookId = book.Id,
                StudentId = student.Id,
                LocalLoanNumber = i + 1,
                date = DateOnly.FromDateTime(DateTime.Now.AddDays(-10)),
                expiryDate = DateOnly.FromDateTime(DateTime.Now.AddDays(4)),
                Status = LoanStatus.Active,
                CreatedAt = DateTime.UtcNow
            });

            // حجز
            bookReservations.Add(new BookReservation
            {
                BookId = book.Id,
                StudentId = student.Id,
                Date = DateOnly.FromDateTime(DateTime.Now),
                ExpiryDate = DateOnly.FromDateTime(DateTime.Now.AddDays(7)),
                Status = ReservationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
        }

        db.BookLoans.AddRange(bookLoans);
        db.BookReservations.AddRange(bookReservations);
        await db.SaveChangesAsync();

        // ============================================
        // 19. إضافة تسجيلات في الأنشطة
        // ============================================
        var activityRegistrations = new List<ActivityRegistration>();
        var activities = await db.Activities.Where(a => a.SchoolId == school1.Id).ToListAsync();

        for (int i = 0; i < 15; i++)
        {
            var student = students[i % students.Count];
            var activity = activities[i % activities.Count];

            activityRegistrations.Add(new ActivityRegistration
            {
                ActivityId = activity.Id,
                StudentId = student.Id,
                Status = (RegistrationStatus)(i % 3),
            });
        }

        db.ActivityRegistrations.AddRange(activityRegistrations);
        await db.SaveChangesAsync();

        // ============================================
        // 20. إضافة تحذيرات لبعض الطلاب
        // ============================================
        var warnings = new List<Warning>();

        for (int i = 0; i < 5; i++)
        {
            var student = students[i * 3 % students.Count];
            warnings.Add(new Warning
            {
                StudentId = student.Id,
                Type = (WarningType)(i % 3),
                Reason = i == 0 ? "تأخر متكرر عن الحضور" :
                         i == 1 ? "عدم إنجاز الواجبات" :
                         i == 2 ? "سلوك غير لائق" :
                         i == 3 ? "غياب بدون عذر" : "تدني المستوى الدراسي",
                IssuedById = manager1.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        db.Warnings.AddRange(warnings);
        await db.SaveChangesAsync();

        // ============================================
        // 21. إضافة استدعاءات ولي الأمر
        // ============================================
        var summons = new List<GuardianSummon>();

        for (int i = 0; i < 3; i++)
        {
            var student = students[i * 4 % students.Count];
            summons.Add(new GuardianSummon
            {
                StudentId = student.Id,
                Reason = i == 0 ? "ضعف في الأداء الدراسي" :
                         i == 1 ? "سلوك غير مناسب" : "غياب متكرر",
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(7 + i)),
                CreatedAt = DateTime.UtcNow
            });
        }

        db.GuardianSummons.AddRange(summons);
        await db.SaveChangesAsync();
    }
}