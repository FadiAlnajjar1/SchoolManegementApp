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
        // 4. إنشاء الموظفين للمدرسة الأولى
        // ============================================
        // مدير المدرسة (2)
        var manager1 = new Employee 
        { 
            Name = "مدير المدرسة 1", 
            Email = "m1@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678901",
            CreatedAt = DateTime.UtcNow
        };

        var manager2 = new Employee 
        { 
            Name = "مدير المدرسة 2", 
            Email = "m2@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678902",
            CreatedAt = DateTime.UtcNow
        };

        // أمين سر (2)
        var secretary1 = new Employee 
        { 
            Name = "أمين السر 1", 
            Email = "s1@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678903",
            CreatedAt = DateTime.UtcNow
        };

        var secretary2 = new Employee 
        { 
            Name = "أمين السر 2", 
            Email = "s2@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678904",
            CreatedAt = DateTime.UtcNow
        };

        // موجه (2)
        var counselor1 = new Employee 
        { 
            Name = "الموجه 1", 
            Email = "c1", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "123456796967",
            CreatedAt = DateTime.UtcNow
        };

        var counselor2 = new Employee 
        { 
            Name = "الموجه 2", 
            Email = "c2", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "123456346765",
            CreatedAt = DateTime.UtcNow
        };

        // أمين مكتبة (2)
        var librarian1 = new Employee 
        { 
            Name = "أمين المكتبة 1", 
            Email = "l1@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678907",
            CreatedAt = DateTime.UtcNow
        };

        var librarian2 = new Employee 
        { 
            Name = "أمين المكتبة 2", 
            Email = "l2@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678908",
            CreatedAt = DateTime.UtcNow
        };

        // مشرف نشاطات (2)
        var supervisor1 = new Employee 
        { 
            Name = "مشرف النشاطات 1", 
            Email = "a1@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678909",
            CreatedAt = DateTime.UtcNow
        };

        var supervisor2 = new Employee 
        { 
            Name = "مشرف النشاطات 2", 
            Email = "a2@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678910",
            CreatedAt = DateTime.UtcNow
        };

        // معلم (4)
        var teacher1 = new Employee 
        { 
            Name = "معلم الرياضيات", 
            Email = "t1", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "1234567886",
            CreatedAt = DateTime.UtcNow
        };

        var teacher2 = new Employee 
        { 
            Name = "معلم اللغة العربية", 
            Email = "t2", 
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

        // ============================================
        // 5. إنشاء الموظفين للمدرسة الثانية
        // ============================================
        var manager3 = new Employee 
        { 
            Name = "مدير مدرسة حلب 1", 
            Email = "m3@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678915",
            CreatedAt = DateTime.UtcNow
        };

        var manager4 = new Employee 
        { 
            Name = "مدير مدرسة حلب 2", 
            Email = "m4@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678916",
            CreatedAt = DateTime.UtcNow
        };

        var teacher5 = new Employee 
        { 
            Name = "معلم العلوم 2", 
            Email = "t5@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678917",
            CreatedAt = DateTime.UtcNow
        };

        var teacher6 = new Employee 
        { 
            Name = "معلم الإنجليزية 2", 
            Email = "t6@school.sy", 
            PasswordHash = Hash(DefaultPassword),
            NationalId = "12345678918",
            CreatedAt = DateTime.UtcNow
        };

        // ============================================
        // 6. إضافة جميع الموظفين
        // ============================================
        db.Employees.AddRange(
            manager1, manager2, secretary1, secretary2, 
            counselor1, counselor2, librarian1, librarian2,
            supervisor1, supervisor2, teacher1, teacher2, teacher3, teacher4,
            manager3, manager4, teacher5, teacher6
        );
        await db.SaveChangesAsync();

        // ============================================
        // 7. ربط الموظفين بالمدرسة الأولى
        // ============================================
        var employeeSchools1 = new List<EmployeeSchool>
        {
            // مديرين
            new EmployeeSchool { EmployeeId = manager1.Id, SchoolId = school1.Id, LocalEmployeeNumber = 1, Role = EmployeeRole.Principal, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = manager2.Id, SchoolId = school1.Id, LocalEmployeeNumber = 2, Role = EmployeeRole.Principal, IsActive = true, CreatedAt = DateTime.UtcNow },
            // أمناء سر
            new EmployeeSchool { EmployeeId = secretary1.Id, SchoolId = school1.Id, LocalEmployeeNumber = 3, Role = EmployeeRole.Secretary, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = secretary2.Id, SchoolId = school1.Id, LocalEmployeeNumber = 4, Role = EmployeeRole.Secretary, IsActive = true, CreatedAt = DateTime.UtcNow },
            // موجّهين
            new EmployeeSchool { EmployeeId = counselor1.Id, SchoolId = school1.Id, LocalEmployeeNumber = 5, Role = EmployeeRole.Counselor, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = counselor2.Id, SchoolId = school1.Id, LocalEmployeeNumber = 6, Role = EmployeeRole.Counselor, IsActive = true, CreatedAt = DateTime.UtcNow },
            // أمناء مكتبة
            new EmployeeSchool { EmployeeId = librarian1.Id, SchoolId = school1.Id, LocalEmployeeNumber = 7, Role = EmployeeRole.Librarian, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = librarian2.Id, SchoolId = school1.Id, LocalEmployeeNumber = 8, Role = EmployeeRole.Librarian, IsActive = true, CreatedAt = DateTime.UtcNow },
            // مشرفي نشاطات
            new EmployeeSchool { EmployeeId = supervisor1.Id, SchoolId = school1.Id, LocalEmployeeNumber = 9, Role = EmployeeRole.ActivitySupervisor, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = supervisor2.Id, SchoolId = school1.Id, LocalEmployeeNumber = 10, Role = EmployeeRole.ActivitySupervisor, IsActive = true, CreatedAt = DateTime.UtcNow },
            // معلمين
            new EmployeeSchool { EmployeeId = teacher1.Id, SchoolId = school1.Id, LocalEmployeeNumber = 11, Role = EmployeeRole.Teacher, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = teacher2.Id, SchoolId = school1.Id, LocalEmployeeNumber = 12, Role = EmployeeRole.Teacher, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = teacher3.Id, SchoolId = school1.Id, LocalEmployeeNumber = 13, Role = EmployeeRole.Teacher, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = teacher4.Id, SchoolId = school1.Id, LocalEmployeeNumber = 14, Role = EmployeeRole.Teacher, IsActive = true, CreatedAt = DateTime.UtcNow }
        };

        db.EmployeeSchools.AddRange(employeeSchools1);

        // ============================================
        // 8. ربط الموظفين بالمدرسة الثانية
        // ============================================
        var employeeSchools2 = new List<EmployeeSchool>
        {
            // مديرين
            new EmployeeSchool { EmployeeId = manager3.Id, SchoolId = school2.Id, LocalEmployeeNumber = 1, Role = EmployeeRole.Principal, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = manager4.Id, SchoolId = school2.Id, LocalEmployeeNumber = 2, Role = EmployeeRole.Principal, IsActive = true, CreatedAt = DateTime.UtcNow },
            // معلمين
            new EmployeeSchool { EmployeeId = teacher5.Id, SchoolId = school2.Id, LocalEmployeeNumber = 3, Role = EmployeeRole.Teacher, IsActive = true, CreatedAt = DateTime.UtcNow },
            new EmployeeSchool { EmployeeId = teacher6.Id, SchoolId = school2.Id, LocalEmployeeNumber = 4, Role = EmployeeRole.Teacher, IsActive = true, CreatedAt = DateTime.UtcNow }
        };

        db.EmployeeSchools.AddRange(employeeSchools2);
        await db.SaveChangesAsync();

        // ============================================
        // 9. إضافة TeacherAssignments للمعلمين
        // ============================================
        var teacherAssignments = new List<TeacherAssignment>
        {
            new TeacherAssignment { EmployeeId = teacher1.Id, SchoolId = school1.Id },
            new TeacherAssignment { EmployeeId = teacher2.Id, SchoolId = school1.Id },
            new TeacherAssignment { EmployeeId = teacher3.Id, SchoolId = school1.Id },
            new TeacherAssignment { EmployeeId = teacher4.Id, SchoolId = school1.Id },
            new TeacherAssignment { EmployeeId = teacher5.Id, SchoolId = school2.Id },
            new TeacherAssignment { EmployeeId = teacher6.Id, SchoolId = school2.Id }
        };

        db.TeacherAssignments.AddRange(teacherAssignments);
        await db.SaveChangesAsync();

        // ============================================
        // 10. إنشاء الصفوف للمدرسة الأولى
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
        // 11. إنشاء الصفوف للمدرسة الثانية
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
        // 12. إنشاء الشعب للمدرسة الأولى
        // ============================================
        var sectionA1 = new Section
        {
            Name = "الشعبة الأولى",
            GradeId = grade10_1.Id,
            SchoolId = school1.Id,
            CounselorId = counselor1.Id,
            LocalSectionNumber = 1
        };
        db.Sections.Add(sectionA1);

        var sectionB1 = new Section
        {
            Name = "الشعبة الثانية",
            GradeId = grade10_1.Id,
            SchoolId = school1.Id,
            CounselorId = counselor2.Id,
            LocalSectionNumber = 2
        };
        db.Sections.Add(sectionB1);

        var sectionC1 = new Section
        {
            Name = "الشعبة الثالثة",
            GradeId = grade11_1.Id,
            SchoolId = school1.Id,
            CounselorId = counselor1.Id,
            LocalSectionNumber = 1
        };
        db.Sections.Add(sectionC1);
        await db.SaveChangesAsync();

        // ============================================
        // 13. إنشاء الشعب للمدرسة الثانية
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
        // 14. إنشاء المواد للمدرسة الأولى
        // ============================================
        var math = new Subject { Name = "الرياضيات", TeacherId = teacher1.Id, SchoolId = school1.Id, LocalSubjectId = 1 };
        var arabic = new Subject { Name = "اللغة العربية", TeacherId = teacher2.Id, SchoolId = school1.Id, LocalSubjectId = 2 };
        var science = new Subject { Name = "العلوم", TeacherId = teacher3.Id, SchoolId = school1.Id, LocalSubjectId = 3 };
        var english = new Subject { Name = "اللغة الإنجليزية", TeacherId = teacher4.Id, SchoolId = school1.Id, LocalSubjectId = 4 };
        var history = new Subject { Name = "التاريخ", TeacherId = teacher1.Id, SchoolId = school1.Id, LocalSubjectId = 5 };
        var geography = new Subject { Name = "الجغرافيا", TeacherId = teacher3.Id, SchoolId = school1.Id, LocalSubjectId = 6 };
        db.Subjects.AddRange(math, arabic, science, english, history, geography);
        await db.SaveChangesAsync();

        // ============================================
        // 15. إنشاء المواد للمدرسة الثانية
        // ============================================
        var math2 = new Subject { Name = "الرياضيات", TeacherId = teacher5.Id, SchoolId = school2.Id, LocalSubjectId = 1 };
        var english2 = new Subject { Name = "اللغة الإنجليزية", TeacherId = teacher6.Id, SchoolId = school2.Id, LocalSubjectId = 2 };
        var science2 = new Subject { Name = "العلوم", TeacherId = teacher5.Id, SchoolId = school2.Id, LocalSubjectId = 3 };
        db.Subjects.AddRange(math2, english2, science2);
        await db.SaveChangesAsync();

        // ============================================
        // 16. ربط المعلمين بالمواد
        // ============================================
        db.TeacherSubjects.AddRange(
            new TeacherSubject { TeacherId = teacher1.Id, SubjectId = math.Id },
            new TeacherSubject { TeacherId = teacher2.Id, SubjectId = arabic.Id },
            new TeacherSubject { TeacherId = teacher3.Id, SubjectId = science.Id },
            new TeacherSubject { TeacherId = teacher4.Id, SubjectId = english.Id },
            new TeacherSubject { TeacherId = teacher1.Id, SubjectId = history.Id },
            new TeacherSubject { TeacherId = teacher3.Id, SubjectId = geography.Id },
            new TeacherSubject { TeacherId = teacher5.Id, SubjectId = math2.Id },
            new TeacherSubject { TeacherId = teacher6.Id, SubjectId = english2.Id },
            new TeacherSubject { TeacherId = teacher5.Id, SubjectId = science2.Id }
        );
        await db.SaveChangesAsync();

        // ============================================
        // 17. ربط المعلمين بالشعب
        // ============================================
        db.TeacherGrades.AddRange(
            new TeacherGrade { TeacherId = teacher1.Id, SubjectId = math.Id, SectionId = sectionA1.Id },
            new TeacherGrade { TeacherId = teacher2.Id, SubjectId = arabic.Id, SectionId = sectionA1.Id },
            new TeacherGrade { TeacherId = teacher3.Id, SubjectId = science.Id, SectionId = sectionA1.Id },
            new TeacherGrade { TeacherId = teacher4.Id, SubjectId = english.Id, SectionId = sectionA1.Id },
            new TeacherGrade { TeacherId = teacher1.Id, SubjectId = math.Id, SectionId = sectionB1.Id },
            new TeacherGrade { TeacherId = teacher2.Id, SubjectId = arabic.Id, SectionId = sectionB1.Id },
            new TeacherGrade { TeacherId = teacher3.Id, SubjectId = science.Id, SectionId = sectionB1.Id },
            new TeacherGrade { TeacherId = teacher4.Id, SubjectId = english.Id, SectionId = sectionB1.Id },
            new TeacherGrade { TeacherId = teacher1.Id, SubjectId = math.Id, SectionId = sectionC1.Id },
            new TeacherGrade { TeacherId = teacher2.Id, SubjectId = arabic.Id, SectionId = sectionC1.Id },
            new TeacherGrade { TeacherId = teacher3.Id, SubjectId = science.Id, SectionId = sectionC1.Id },
            new TeacherGrade { TeacherId = teacher4.Id, SubjectId = english.Id, SectionId = sectionC1.Id },
            new TeacherGrade { TeacherId = teacher5.Id, SubjectId = math2.Id, SectionId = sectionA2.Id },
            new TeacherGrade { TeacherId = teacher6.Id, SubjectId = english2.Id, SectionId = sectionA2.Id },
            new TeacherGrade { TeacherId = teacher5.Id, SubjectId = science2.Id, SectionId = sectionA2.Id }
        );
        await db.SaveChangesAsync();

        // ============================================
        // 18. إنشاء الطلاب (مع LocalStudentNumber)
        // ============================================
        var students = new List<Student>
        {
            // المدرسة الأولى - 5 طلاب
            new Student
            {
                Name = "أحمد محمد",
                Email = "s1",
                PasswordHash = Hash(DefaultPassword),
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
                Email = "s2",
                PasswordHash = Hash(DefaultPassword),
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
                Email = "s3@school.sy",
                PasswordHash = Hash(DefaultPassword),
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
                Email = "s4@school.sy",
                PasswordHash = Hash(DefaultPassword),
                SchoolId = school1.Id,
                SectionId = sectionC1.Id,
                LocalStudentNumber = 4,
                GuardianName = "سعيد نورا",
                GuardianPhone = "0994444444",
                BloodType = "AB+",
                CreatedAt = DateTime.UtcNow
            },
            new Student
            {
                Name = "محمود حسن",
                Email = "s5@school.sy",
                PasswordHash = Hash(DefaultPassword),
                SchoolId = school1.Id,
                SectionId = sectionC1.Id,
                LocalStudentNumber = 5,
                GuardianName = "حسن محمود",
                GuardianPhone = "0995555555",
                BloodType = "O-",
                CreatedAt = DateTime.UtcNow
            },
            // المدرسة الثانية - 3 طلاب
            new Student
            {
                Name = "فاطمة علي",
                Email = "s6@school.sy",
                PasswordHash = Hash(DefaultPassword),
                SchoolId = school2.Id,
                SectionId = sectionA2.Id,
                LocalStudentNumber = 1,
                GuardianName = "علي فاطمة",
                GuardianPhone = "0996666666",
                BloodType = "O+",
                CreatedAt = DateTime.UtcNow
            },
            new Student
            {
                Name = "حسن حسين",
                Email = "s7@school.sy",
                PasswordHash = Hash(DefaultPassword),
                SchoolId = school2.Id,
                SectionId = sectionA2.Id,
                LocalStudentNumber = 2,
                GuardianName = "حسين حسن",
                GuardianPhone = "0997777777",
                BloodType = "A+",
                CreatedAt = DateTime.UtcNow
            },
            new Student
            {
                Name = "زينب محمود",
                Email = "s8@school.sy",
                PasswordHash = Hash(DefaultPassword),
                SchoolId = school2.Id,
                SectionId = sectionA2.Id,
                LocalStudentNumber = 3,
                GuardianName = "محمود زينب",
                GuardianPhone = "0998888888",
                BloodType = "B+",
                CreatedAt = DateTime.UtcNow
            }
        };
        db.Students.AddRange(students);
        await db.SaveChangesAsync();

        // ============================================
        // 19. إنشاء أعضاء المكتبة
        // ============================================
        db.LibraryMembers.AddRange(
            new LibraryMember { StudentId = students[0].Id, SchoolId = school1.Id, LocalMemberNumber = 1, Status = MemberStatus.Active },
            new LibraryMember { StudentId = students[1].Id, SchoolId = school1.Id, LocalMemberNumber = 2, Status = MemberStatus.Active },
            new LibraryMember { StudentId = students[2].Id, SchoolId = school1.Id, LocalMemberNumber = 3, Status = MemberStatus.Active },
            new LibraryMember { StudentId = students[3].Id, SchoolId = school1.Id, LocalMemberNumber = 4, Status = MemberStatus.Active },
            new LibraryMember { StudentId = students[4].Id, SchoolId = school1.Id, LocalMemberNumber = 5, Status = MemberStatus.Active },
            new LibraryMember { StudentId = students[5].Id, SchoolId = school2.Id, LocalMemberNumber = 1, Status = MemberStatus.Active },
            new LibraryMember { StudentId = students[6].Id, SchoolId = school2.Id, LocalMemberNumber = 2, Status = MemberStatus.Active },
            new LibraryMember { StudentId = students[7].Id, SchoolId = school2.Id, LocalMemberNumber = 3, Status = MemberStatus.Active }
        );
        await db.SaveChangesAsync();

        // ============================================
        // 20. إنشاء الكتب
        // ============================================
        db.Books.AddRange(
            new Book { SchoolId = school1.Id, LocalBookNumber = 1, Title = "الأيام", Author = "طه حسين", Isbn = "978-977-416-001-1", Copies = 5, AvailableCopies = 5 },
            new Book { SchoolId = school1.Id, LocalBookNumber = 2, Title = "النحو الواضح", Author = "علي الجارم", Isbn = "978-977-416-002-8", Copies = 3, AvailableCopies = 3 },
            new Book { SchoolId = school1.Id, LocalBookNumber = 3, Title = "فيزياء الصف العاشر", Author = "أحمد زكي", Isbn = "978-977-416-003-5", Copies = 4, AvailableCopies = 4 },
            new Book { SchoolId = school1.Id, LocalBookNumber = 4, Title = "الكيمياء", Author = "مصطفى فهمي", Isbn = "978-977-416-004-2", Copies = 3, AvailableCopies = 3 },
            new Book { SchoolId = school1.Id, LocalBookNumber = 5, Title = "الأحياء", Author = "عبد الوهاب", Isbn = "978-977-416-005-9", Copies = 3, AvailableCopies = 3 },
            new Book { SchoolId = school2.Id, LocalBookNumber = 1, Title = "English Grammar", Author = "John Smith", Isbn = "978-977-416-006-6", Copies = 3, AvailableCopies = 3 },
            new Book { SchoolId = school2.Id, LocalBookNumber = 2, Title = "Science Basics", Author = "Peter Jones", Isbn = "978-977-416-007-3", Copies = 2, AvailableCopies = 2 },
            new Book { SchoolId = school2.Id, LocalBookNumber = 3, Title = "Mathematics", Author = "Robert Brown", Isbn = "978-977-416-008-0", Copies = 2, AvailableCopies = 2 }
        );
        await db.SaveChangesAsync();

        // ============================================
        // 21. إنشاء الأنشطة
        // ============================================
        db.Activities.AddRange(
            new Activity { SchoolId = school1.Id, LocalActivityId = 1, Name = "رحلة إلى تدمر", Type = ActivityType.Trip, Schedule = "الخميس القادم 8 صباحاً", Capacity = 40, SupervisorId = supervisor1.Id },
            new Activity { SchoolId = school1.Id, LocalActivityId = 2, Name = "مسابقة الرياضيات", Schedule = "الأحد القادم 10 صباحاً", Capacity = 30, SupervisorId = supervisor2.Id },
            new Activity { SchoolId = school1.Id, LocalActivityId = 3, Name = "ورشة الفنون", Schedule = "الثلاثاء القادم 2 مساءً", Capacity = 25, SupervisorId = supervisor1.Id },
            new Activity { SchoolId = school2.Id, LocalActivityId = 1, Name = "رحلة إلى قلعة حلب", Type = ActivityType.Trip, Schedule = "الثلاثاء القادم 9 صباحاً", Capacity = 35, SupervisorId = manager3.Id }
        );
        await db.SaveChangesAsync();

        // ============================================
        // 22. إنشاء الإعلانات (مع LocalAnnouncementId)
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
                CreatedById = manager2.Id,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            },
            new Announcement 
            { 
                SchoolId = school2.Id,
                LocalAnnouncementId = 1,
                Title = "اجتماع أولياء الأمور", 
                Body = "سيكون هناك اجتماع لأولياء الأمور يوم الخميس القادم",
                Type = AnnouncementType.General,
                Audience = AnnouncementAudience.Parents,
                CreatedById = manager3.Id,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }
        );
        await db.SaveChangesAsync();
    }
}