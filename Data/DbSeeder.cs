using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Data;

public static class DbSeeder
{
    // Admin ثابت
    private const string AdminName = "أدمن الوزارة";
    private const string AdminEmail = "admin@moe.sy";
    private const string AdminPassword = "Admin@123";

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
                Name = AdminName,
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
        // 4. إنشاء الموظفين للمدرسة الأولى
        // ============================================
        var manager = new Employee 
        { 
            Name = "مدير المدرسة", 
            Email = "principal@school.sy", 
            PasswordHash = Hash("Manager@123"),
            NationalId = "12345678901",
            CreatedAt = DateTime.UtcNow
        };

        var secretary = new Employee 
        { 
            Name = "أمين السر", 
            Email = "secretary@school.sy", 
            PasswordHash = Hash("Secretary@123"),
            NationalId = "12345678902",
            CreatedAt = DateTime.UtcNow
        };

        var counselor = new Employee 
        { 
            Name = "الموجه", 
            Email = "counselor@school.sy", 
            PasswordHash = Hash("Counselor@123"),
            NationalId = "12345678903",
            CreatedAt = DateTime.UtcNow
        };

        var librarian = new Employee 
        { 
            Name = "أمين المكتبة", 
            Email = "librarian@school.sy", 
            PasswordHash = Hash("Librarian@123"),
            NationalId = "12345678904",
            CreatedAt = DateTime.UtcNow
        };

        var supervisor = new Employee 
        { 
            Name = "مشرف النشاطات", 
            Email = "activities@school.sy", 
            PasswordHash = Hash("Activities@123"),
            NationalId = "12345678905",
            CreatedAt = DateTime.UtcNow
        };

        var teacher = new Employee 
        { 
            Name = "معلم الرياضيات", 
            Email = "teacher@school.sy", 
            PasswordHash = Hash("Teacher@123"),
            NationalId = "12345678906",
            CreatedAt = DateTime.UtcNow
        };

        var teacher2 = new Employee 
        { 
            Name = "معلمة العربية", 
            Email = "teacher2@school.sy", 
            PasswordHash = Hash("Teacher@123"),
            NationalId = "12345678907",
            CreatedAt = DateTime.UtcNow
        };

        // ============================================
        // 5. إنشاء الموظفين للمدرسة الثانية
        // ============================================
        var manager2 = new Employee 
        { 
            Name = "مدير مدرسة حلب", 
            Email = "principal2@school.sy", 
            PasswordHash = Hash("Manager@123"),
            NationalId = "12345678908",
            CreatedAt = DateTime.UtcNow
        };

        var teacher3 = new Employee 
        { 
            Name = "معلم العلوم", 
            Email = "teacher3@school.sy", 
            PasswordHash = Hash("Teacher@123"),
            NationalId = "12345678909",
            CreatedAt = DateTime.UtcNow
        };

        var teacher4 = new Employee 
        { 
            Name = "معلمة الإنجليزية", 
            Email = "teacher4@school.sy", 
            PasswordHash = Hash("Teacher@123"),
            NationalId = "12345678910",
            CreatedAt = DateTime.UtcNow
        };

        // ============================================
        // 6. إضافة جميع الموظفين
        // ============================================
        db.Employees.AddRange(manager, secretary, counselor, librarian, supervisor, teacher, teacher2, manager2, teacher3, teacher4);
        await db.SaveChangesAsync();

        // ============================================
        // 7. ربط الموظفين بالمدرسة الأولى (مع LocalEmployeeNumber)
        // ============================================
        var employeeSchools1 = new List<EmployeeSchool>
        {
            new EmployeeSchool { 
                EmployeeId = manager.Id, 
                SchoolId = school1.Id, 
                LocalEmployeeNumber = 1,
                Role = EmployeeRole.Principal, 
                IsActive = true, 
                CreatedAt = DateTime.UtcNow 
            },
            new EmployeeSchool { 
                EmployeeId = secretary.Id, 
                SchoolId = school1.Id, 
                LocalEmployeeNumber = 2,
                Role = EmployeeRole.Secretary, 
                IsActive = true, 
                CreatedAt = DateTime.UtcNow 
            },
            new EmployeeSchool { 
                EmployeeId = counselor.Id, 
                SchoolId = school1.Id, 
                LocalEmployeeNumber = 3,
                Role = EmployeeRole.Counselor, 
                IsActive = true, 
                CreatedAt = DateTime.UtcNow 
            },
            new EmployeeSchool { 
                EmployeeId = librarian.Id, 
                SchoolId = school1.Id, 
                LocalEmployeeNumber = 4,
                Role = EmployeeRole.Librarian, 
                IsActive = true, 
                CreatedAt = DateTime.UtcNow 
            },
            new EmployeeSchool { 
                EmployeeId = supervisor.Id, 
                SchoolId = school1.Id, 
                LocalEmployeeNumber = 5,
                Role = EmployeeRole.ActivitySupervisor, 
                IsActive = true, 
                CreatedAt = DateTime.UtcNow 
            },
            new EmployeeSchool { 
                EmployeeId = teacher.Id, 
                SchoolId = school1.Id, 
                LocalEmployeeNumber = 6,
                Role = EmployeeRole.Teacher, 
                IsActive = true, 
                CreatedAt = DateTime.UtcNow 
            },
            new EmployeeSchool { 
                EmployeeId = teacher2.Id, 
                SchoolId = school1.Id, 
                LocalEmployeeNumber = 7,
                Role = EmployeeRole.Teacher, 
                IsActive = true, 
                CreatedAt = DateTime.UtcNow 
            }
        };

        db.EmployeeSchools.AddRange(employeeSchools1);

        // ============================================
        // 8. ربط الموظفين بالمدرسة الثانية (مع LocalEmployeeNumber)
        // ============================================
        var employeeSchools2 = new List<EmployeeSchool>
        {
            new EmployeeSchool { 
                EmployeeId = manager2.Id, 
                SchoolId = school2.Id, 
                LocalEmployeeNumber = 1,
                Role = EmployeeRole.Principal, 
                IsActive = true, 
                CreatedAt = DateTime.UtcNow 
            },
            new EmployeeSchool { 
                EmployeeId = teacher3.Id, 
                SchoolId = school2.Id, 
                LocalEmployeeNumber = 2,
                Role = EmployeeRole.Teacher, 
                IsActive = true, 
                CreatedAt = DateTime.UtcNow 
            },
            new EmployeeSchool { 
                EmployeeId = teacher4.Id, 
                SchoolId = school2.Id, 
                LocalEmployeeNumber = 3,
                Role = EmployeeRole.Teacher, 
                IsActive = true, 
                CreatedAt = DateTime.UtcNow 
            }
        };

        db.EmployeeSchools.AddRange(employeeSchools2);
        await db.SaveChangesAsync();

        // ============================================
        // 9. إضافة TeacherAssignments للمعلمين
        // ============================================
        var teacherAssignments = new List<TeacherAssignment>
        {
            new TeacherAssignment { EmployeeId = teacher.Id, SchoolId = school1.Id },
            new TeacherAssignment { EmployeeId = teacher2.Id, SchoolId = school1.Id },
            new TeacherAssignment { EmployeeId = teacher3.Id, SchoolId = school2.Id },
            new TeacherAssignment { EmployeeId = teacher4.Id, SchoolId = school2.Id }
        };

        db.TeacherAssignments.AddRange(teacherAssignments);
        await db.SaveChangesAsync();

        // ============================================
        // 10. إنشاء الصفوف للمدرسة الأولى (مع LocalGradeNumber)
        // ============================================
        var grade10_1 = new Grade 
        { 
            SchoolId = school1.Id, 
            Name = "الصف العاشر",
            LocalGradeNumber = 1,
            AcademicYear = DateTime.Now.Year
        };
        db.Grades.Add(grade10_1);

        var grade11_1 = new Grade 
        { 
            SchoolId = school1.Id, 
            Name = "الصف الحادي عشر",
            LocalGradeNumber = 2,
            AcademicYear = DateTime.Now.Year
        };
        db.Grades.Add(grade11_1);
        await db.SaveChangesAsync();

        // ============================================
        // 11. إنشاء الصفوف للمدرسة الثانية (مع LocalGradeNumber)
        // ============================================
        var grade10_2 = new Grade 
        { 
            SchoolId = school2.Id, 
            Name = "الصف العاشر",
            LocalGradeNumber = 1,
            AcademicYear = DateTime.Now.Year
        };
        db.Grades.Add(grade10_2);
        await db.SaveChangesAsync();

        // ============================================
        // 12. إنشاء الشعب للمدرسة الأولى (مع LocalSectionNumber)
        // ============================================
        var sectionA1 = new Section
        {
            Name = "الشعبة الأولى",
            GradeId = grade10_1.Id,
            SchoolId = school1.Id,
            CounselorId = counselor.Id,
            LocalSectionNumber = 1
        };
        db.Sections.Add(sectionA1);

        var sectionB1 = new Section
        {
            Name = "الشعبة الثانية",
            GradeId = grade10_1.Id,
            SchoolId = school1.Id,
            CounselorId = counselor.Id,
            LocalSectionNumber = 2
        };
        db.Sections.Add(sectionB1);
        await db.SaveChangesAsync();

        // ============================================
        // 13. إنشاء الشعب للمدرسة الثانية (مع LocalSectionNumber)
        // ============================================
        var sectionA2 = new Section
        {
            Name = "الشعبة الأولى",
            GradeId = grade10_2.Id,
            SchoolId = school2.Id,
            CounselorId = null,
            LocalSectionNumber = 1
        };
        db.Sections.Add(sectionA2);
        await db.SaveChangesAsync();

        // ============================================
        // 14. إنشاء المواد للمدرسة الأولى (مع LocalSubjectId)
        // ============================================
        var math = new Subject { Name = "الرياضيات", TeacherId = teacher.Id, SchoolId = school1.Id, LocalSubjectId = 1 };
        var arabic = new Subject { Name = "اللغة العربية", TeacherId = teacher2.Id, SchoolId = school1.Id, LocalSubjectId = 2 };
        var science = new Subject { Name = "العلوم", TeacherId = teacher.Id, SchoolId = school1.Id, LocalSubjectId = 3 };
        db.Subjects.AddRange(math, arabic, science);
        await db.SaveChangesAsync();

        // ============================================
        // 15. إنشاء المواد للمدرسة الثانية (مع LocalSubjectId)
        // ============================================
        var math2 = new Subject { Name = "الرياضيات", TeacherId = teacher3.Id, SchoolId = school2.Id, LocalSubjectId = 1 };
        var english = new Subject { Name = "اللغة الإنجليزية", TeacherId = teacher4.Id, SchoolId = school2.Id, LocalSubjectId = 2 };
        var science2 = new Subject { Name = "العلوم", TeacherId = teacher3.Id, SchoolId = school2.Id, LocalSubjectId = 3 };
        db.Subjects.AddRange(math2, english, science2);
        await db.SaveChangesAsync();

        // ============================================
        // 16. ربط المعلمين بالمواد (TeacherSubject)
        // ============================================
        db.TeacherSubjects.AddRange(
            new TeacherSubject { TeacherId = teacher.Id, SubjectId = math.Id },
            new TeacherSubject { TeacherId = teacher2.Id, SubjectId = arabic.Id },
            new TeacherSubject { TeacherId = teacher.Id, SubjectId = science.Id },
            new TeacherSubject { TeacherId = teacher3.Id, SubjectId = math2.Id },
            new TeacherSubject { TeacherId = teacher4.Id, SubjectId = english.Id },
            new TeacherSubject { TeacherId = teacher3.Id, SubjectId = science2.Id }
        );
        await db.SaveChangesAsync();

        // ============================================
        // 17. ربط المعلمين بالشعب (TeacherGrade)
        // ============================================
        db.TeacherGrades.AddRange(
            new TeacherGrade { TeacherId = teacher.Id, SubjectId = math.Id, SectionId = sectionA1.Id },
            new TeacherGrade { TeacherId = teacher2.Id, SubjectId = arabic.Id, SectionId = sectionA1.Id },
            new TeacherGrade { TeacherId = teacher.Id, SubjectId = science.Id, SectionId = sectionA1.Id },
            new TeacherGrade { TeacherId = teacher.Id, SubjectId = math.Id, SectionId = sectionB1.Id },
            new TeacherGrade { TeacherId = teacher2.Id, SubjectId = arabic.Id, SectionId = sectionB1.Id },
            new TeacherGrade { TeacherId = teacher3.Id, SubjectId = math2.Id, SectionId = sectionA2.Id },
            new TeacherGrade { TeacherId = teacher4.Id, SubjectId = english.Id, SectionId = sectionA2.Id },
            new TeacherGrade { TeacherId = teacher3.Id, SubjectId = science2.Id, SectionId = sectionA2.Id }
        );
        await db.SaveChangesAsync();

        // ============================================
        // 18. إنشاء الطلاب (مع LocalStudentNumber)
        // ============================================
        var students = new List<Student>
        {
            new Student
            {
                Name = "أحمد محمد",
                Email = "student1@school.sy",
                PasswordHash = Hash("Student@123"),
                SchoolId = school1.Id,
                SectionId = sectionA1.Id,
                LocalStudentNumber = 1,
                GuardianName = "محمد أحمد",
                GuardianPhone = "0991111111",
                BloodType = "O+",
                CreatedAt = DateTime.UtcNow
            },
            new Student
            {
                Name = "ليلى خالد",
                Email = "student2@school.sy",
                PasswordHash = Hash("Student@123"),
                SchoolId = school1.Id,
                SectionId = sectionA1.Id,
                LocalStudentNumber = 2,
                GuardianName = "خالد يوسف",
                GuardianPhone = "0992222222",
                BloodType = "A+",
                CreatedAt = DateTime.UtcNow
            },
            new Student
            {
                Name = "سامر علي",
                Email = "student3@school.sy",
                PasswordHash = Hash("Student@123"),
                SchoolId = school1.Id,
                SectionId = sectionB1.Id,
                LocalStudentNumber = 3,
                GuardianName = "علي سامر",
                GuardianPhone = "0993333333",
                BloodType = "B+",
                CreatedAt = DateTime.UtcNow
            },
            new Student
            {
                Name = "نورا سعيد",
                Email = "student4@school.sy",
                PasswordHash = Hash("Student@123"),
                SchoolId = school2.Id,
                SectionId = sectionA2.Id,
                LocalStudentNumber = 1,
                GuardianName = "سعيد نورا",
                GuardianPhone = "0994444444",
                BloodType = "AB+",
                CreatedAt = DateTime.UtcNow
            },
            new Student
            {
                Name = "محمود حسن",
                Email = "student5@school.sy",
                PasswordHash = Hash("Student@123"),
                SchoolId = school2.Id,
                SectionId = sectionA2.Id,
                LocalStudentNumber = 2,
                GuardianName = "حسن محمود",
                GuardianPhone = "0995555555",
                BloodType = "O-",
                CreatedAt = DateTime.UtcNow
            }
        };
        db.Students.AddRange(students);
        await db.SaveChangesAsync();

        // ============================================
        // 19. إنشاء أعضاء المكتبة (مع LocalMemberNumber)
        // ============================================
        db.LibraryMembers.AddRange(
            new LibraryMember { StudentId = students[0].Id, SchoolId = school1.Id, LocalMemberNumber = 1, Status = MemberStatus.Active },
            new LibraryMember { StudentId = students[1].Id, SchoolId = school1.Id, LocalMemberNumber = 2, Status = MemberStatus.Active },
            new LibraryMember { StudentId = students[2].Id, SchoolId = school1.Id, LocalMemberNumber = 3, Status = MemberStatus.Active },
            new LibraryMember { StudentId = students[3].Id, SchoolId = school2.Id, LocalMemberNumber = 1, Status = MemberStatus.Active },
            new LibraryMember { StudentId = students[4].Id, SchoolId = school2.Id, LocalMemberNumber = 2, Status = MemberStatus.Active }
        );
        await db.SaveChangesAsync();

        // ============================================
        // 20. إنشاء الكتب (مع LocalBookNumber)
        // ============================================
        db.Books.AddRange(
            new Book { SchoolId = school1.Id, LocalBookNumber = 1, Title = "الأيام", Author = "طه حسين", Isbn = "978-977-416-001-1", Copies = 5, AvailableCopies = 5 },
            new Book { SchoolId = school1.Id, LocalBookNumber = 2, Title = "النحو الواضح", Author = "علي الجارم", Isbn = "978-977-416-002-8", Copies = 3, AvailableCopies = 3 },
            new Book { SchoolId = school1.Id, LocalBookNumber = 3, Title = "فيزياء الصف العاشر", Author = "أحمد زكي", Isbn = "978-977-416-003-5", Copies = 4, AvailableCopies = 4 },
            new Book { SchoolId = school2.Id, LocalBookNumber = 1, Title = "English Grammar", Author = "John Smith", Isbn = "978-977-416-004-2", Copies = 3, AvailableCopies = 3 },
            new Book { SchoolId = school2.Id, LocalBookNumber = 2, Title = "Science Basics", Author = "Peter Jones", Isbn = "978-977-416-005-9", Copies = 2, AvailableCopies = 2 }
        );
        await db.SaveChangesAsync();

        // ============================================
        // 21. إنشاء النشاطات
        // ============================================
        db.Activities.AddRange(
            new Activity { SchoolId = school1.Id, Name = "رحلة إلى تدمر", Type = ActivityType.Trip, Schedule = "الخميس القادم 8 صباحاً", Capacity = 40, SupervisorId = supervisor.Id },
            new Activity { SchoolId = school2.Id, Name = "رحلة إلى قلعة حلب", Type = ActivityType.Trip, Schedule = "الثلاثاء القادم 9 صباحاً", Capacity = 35, SupervisorId = manager2.Id }
        );
        await db.SaveChangesAsync();

        // ============================================
        // 22. إنشاء إعلانات
        // ============================================
        db.Announcements.AddRange(
            new Announcement 
            { 
                SchoolId = school1.Id, 
                Title = "بدء العام الدراسي", 
                Body = "يبدأ العام الدراسي الجديد يوم الأحد القادم",
                Type = AnnouncementType.General,
                Audience = AnnouncementAudience.All,
                CreatedById = manager.Id,
                CreatedAt = DateTime.UtcNow
            },
            new Announcement 
            { 
                SchoolId = school2.Id, 
                Title = "اجتماع أولياء الأمور", 
                Body = "سيكون هناك اجتماع لأولياء الأمور يوم الخميس القادم",
                Type = AnnouncementType.General,
                Audience = AnnouncementAudience.Parents,
                CreatedById = manager2.Id,
                CreatedAt = DateTime.UtcNow
            }
        );
        await db.SaveChangesAsync();
    }
}