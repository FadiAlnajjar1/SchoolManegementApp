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
        // 14. إنشاء العلامات للطلاب
        // ============================================
        var marks = new List<Mark>();
        var passPercent = 50m; // نسبة النجاح 50%

        foreach (var student in students)
        {
            foreach (var subject in subjects)
            {
                // التحقق من أن الطالب في شعبة تدرس هذه المادة
                var teacherGrade = await db.TeacherGrades
                    .FirstOrDefaultAsync(tg => tg.SectionId == student.SectionId && tg.SubjectId == subject.Id);

                if (teacherGrade == null) continue;

                // إنشاء علامات عشوائية مع بعض الطلاب راسبين
                var maxOral = 10;
                var maxQuiz1 = 10;
                var maxQuiz2 = 10;
                var maxHomework = 10;
                var maxFinalExam = 60;
                var maxTotal = maxOral + maxQuiz1 + maxQuiz2 + maxHomework + maxFinalExam;

                // بعض الطلاب يضعفون في بعض المواد
                var isWeakStudent = student.Id % 3 == 0; // كل ثالث طالب ضعيف

                // بعض المواد صعبة على بعض الطلاب
                var isHardSubject = subject.Id % 2 == 0; // المواد الزوجية أصعب

                // حساب العلامات
                decimal oral, quiz1, quiz2, homework, finalExam, total;

                if (isWeakStudent || isHardSubject)
                {
                    // طالب ضعيف أو مادة صعبة - ينجح أحياناً
                    oral = random.Next(0, (int)(maxOral * 0.6m));
                    quiz1 = random.Next(0, (int)(maxQuiz1 * 0.5m));
                    quiz2 = random.Next(0, (int)(maxQuiz2 * 0.5m));
                    homework = random.Next(0, (int)(maxHomework * 0.5m));
                    finalExam = random.Next(0, (int)(maxFinalExam * 0.4m));
                }
                else
                {
                    // طالب قوي
                    oral = random.Next((int)(maxOral * 0.7m), maxOral + 1);
                    quiz1 = random.Next((int)(maxQuiz1 * 0.7m), maxQuiz1 + 1);
                    quiz2 = random.Next((int)(maxQuiz2 * 0.7m), maxQuiz2 + 1);
                    homework = random.Next((int)(maxHomework * 0.7m), maxHomework + 1);
                    finalExam = random.Next((int)(maxFinalExam * 0.6m), maxFinalExam + 1);
                }

                total = oral + quiz1 + quiz2 + homework + finalExam;

                // ✅ التأكد من وجود راسبين في كل صف (نسبة 20-30%)
                if (student.LocalStudentNumber % 5 == 0 && total > passPercent)
                {
                    // اجعل بعض الطلاب راسبين
                    total = random.Next(0, (int)passPercent);
                    // تعديل العلامات لتناسب المجموع
                    var ratio = total / maxTotal;
                    oral = Math.Min(oral * ratio, maxOral);
                    quiz1 = Math.Min(quiz1 * ratio, maxQuiz1);
                    quiz2 = Math.Min(quiz2 * ratio, maxQuiz2);
                    homework = Math.Min(homework * ratio, maxHomework);
                    finalExam = Math.Min(finalExam * ratio, maxFinalExam);
                    total = oral + quiz1 + quiz2 + homework + finalExam;
                }

                var mark = new Mark
                {
                    StudentId = student.Id,
                    SubjectId = subject.Id,
                    SchoolId = school1.Id,
                    Semester = 2,
                    Oral = Math.Round(oral, 2),
                    Quiz1 = Math.Round(quiz1, 2),
                    Quiz2 = Math.Round(quiz2, 2),
                    Homework = Math.Round(homework, 2),
                    FinalExam = Math.Round(finalExam, 2),
                    Total = Math.Round(total, 2),
                    MaxOral = maxOral,
                    MaxQuiz1 = maxQuiz1,
                    MaxQuiz2 = maxQuiz2,
                    MaxHomework = maxHomework,
                    MaxFinalExam = maxFinalExam,
                    UpdatedAt = DateTime.UtcNow
                };
                marks.Add(mark);
            }
        }
        db.Marks.AddRange(marks);
        await db.SaveChangesAsync();

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