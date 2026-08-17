using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Dtos;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<School> Schools => Set<School>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeSchool> EmployeeSchools => Set<EmployeeSchool>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentGradeHistory> StudentGradeHistory => Set<StudentGradeHistory>();
    public DbSet<QuizMark> QuizMarks => Set<QuizMark>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();

    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<StudentAttendance> StudentAttendances => Set<StudentAttendance>();
    public DbSet<EmployeeAttendance> EmployeeAttendances => Set<EmployeeAttendance>();
    public DbSet<Leave> Leaves => Set<Leave>();

    public DbSet<Mark> Marks => Set<Mark>();
    public DbSet<MarkConfig> MarkConfigs => Set<MarkConfig>();
    public DbSet<ReportCard> ReportCards => Set<ReportCard>();
    public DbSet<ReportCardSubject> ReportCardSubjects => Set<ReportCardSubject>();
    public DbSet<PerformanceReport> PerformanceReports => Set<PerformanceReport>();

    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<Punishment> Punishments => Set<Punishment>();
    public DbSet<Warning> Warnings => Set<Warning>();
    public DbSet<GuardianSummon> GuardianSummons => Set<GuardianSummon>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<ScheduleImage> ScheduleImages => Set<ScheduleImage>();
    
    // 📚 إدارة المكتبة
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookLoan> BookLoans => Set<BookLoan>();
    public DbSet<BookReservation> BookReservations => Set<BookReservation>();
    public DbSet<BookLoanRequest> BookLoanRequests => Set<BookLoanRequest>();
    
    public DbSet<TeacherSubject> TeacherSubjects => Set<TeacherSubject>();
    public DbSet<TeacherGrade> TeacherGrades => Set<TeacherGrade>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityRegistration> ActivityRegistrations => Set<ActivityRegistration>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<TeacherAssignment> TeacherAssignments => Set<TeacherAssignment>();

    // ✅ إضافة DbSet خاص بـ FcmToken
    public DbSet<FcmToken> FcmTokens => Set<FcmToken>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ============================================
        // الفهارس الفريدة (Unique Indexes)
        // ============================================
        
        mb.Entity<Admin>().HasIndex(a => a.Email).IsUnique();
        mb.Entity<Employee>().HasIndex(e => e.Email).IsUnique();
        mb.Entity<Employee>().HasIndex(e => e.NationalId).IsUnique().HasFilter("[NationalId] IS NOT NULL");
        mb.Entity<Student>().HasIndex(s => s.Email).IsUnique();

        // ============================================
        // Local IDs - Student
        // ============================================
        mb.Entity<Student>()
            .Property(s => s.LocalStudentNumber)
            .IsRequired();

        mb.Entity<Student>()
            .HasIndex(s => new { s.SchoolId, s.LocalStudentNumber })
            .IsUnique()
            .HasDatabaseName("IX_Student_SchoolId_LocalStudentNumber");

        mb.Entity<Student>()
            .HasIndex(s => new { s.SchoolId, s.IsActive });

        mb.Entity<Student>()
            .HasIndex(s => s.GuardianPhone);

        // ============================================
        // Local IDs - Subject
        // ============================================
        mb.Entity<Subject>()
            .HasIndex(s => new { s.SchoolId, s.LocalSubjectId })
            .IsUnique()
            .HasDatabaseName("IX_Subject_SchoolId_LocalSubjectId");

        mb.Entity<Subject>()
            .HasIndex(s => new { s.SchoolId, s.Name })
            .IsUnique()
            .HasDatabaseName("IX_Subject_SchoolId_Name");

        // ============================================
        // Local IDs - Book
        // ============================================
        mb.Entity<Book>()
            .Property(b => b.LocalBookNumber)
            .IsRequired();

        mb.Entity<Book>()
            .HasIndex(b => new { b.SchoolId, b.LocalBookNumber })
            .IsUnique()
            .HasDatabaseName("IX_Book_SchoolId_LocalBookNumber");

        // ============================================
        // Local IDs - BookLoan
        // ============================================
        mb.Entity<BookLoan>()
            .Property(l => l.LocalLoanNumber)
            .IsRequired();

        mb.Entity<BookLoan>()
            .HasIndex(l => new { l.BookId, l.LocalLoanNumber })
            .IsUnique()
            .HasDatabaseName("IX_BookLoan_BookId_LocalLoanNumber");

        mb.Entity<BookLoan>()
            .HasIndex(l => new { l.StudentId, l.Status })
            .HasDatabaseName("IX_BookLoan_StudentId_Status");

        // ============================================
        // Local IDs - BookLoanRequest
        // ============================================
        mb.Entity<BookLoanRequest>()
            .Property(r => r.LocalRequestNumber)
            .IsRequired();

        mb.Entity<BookLoanRequest>()
            .HasIndex(r => new { r.BookId, r.LocalRequestNumber })
            .IsUnique()
            .HasDatabaseName("IX_BookLoanRequest_BookId_LocalRequestNumber");

        mb.Entity<BookLoanRequest>()
            .HasIndex(r => new { r.StudentId, r.Status })
            .HasDatabaseName("IX_BookLoanRequest_StudentId_Status");

        mb.Entity<BookLoanRequest>()
            .HasIndex(r => new { r.BookId, r.StudentId, r.Status })
            .HasDatabaseName("IX_BookLoanRequest_BookId_StudentId_Status");

        // ============================================
        // Local IDs - BookReservation
        // ============================================
        mb.Entity<BookReservation>()
            .HasIndex(r => new { r.BookId, r.StudentId, r.Status })
            .HasDatabaseName("IX_BookReservation_BookId_StudentId_Status");

        mb.Entity<BookReservation>()
            .HasIndex(r => new { r.BookId, r.StudentId })
            .IsUnique()
            .HasDatabaseName("IX_BookReservation_BookId_StudentId_Unique");

        // ============================================
        // Local IDs - Activity
        // ============================================
        mb.Entity<Activity>()
            .Property(a => a.LocalActivityId)
            .IsRequired();

        mb.Entity<Activity>()
            .HasIndex(a => new { a.SchoolId, a.LocalActivityId })
            .IsUnique()
            .HasDatabaseName("IX_Activity_SchoolId_LocalActivityId");

        // ============================================
        // Local IDs - Announcement
        // ============================================
        mb.Entity<Announcement>()
            .Property(a => a.LocalAnnouncementId)
            .IsRequired();

        mb.Entity<Announcement>()
            .HasIndex(a => new { a.SchoolId, a.LocalAnnouncementId })
            .IsUnique()
            .HasDatabaseName("IX_Announcement_SchoolId_LocalAnnouncementId");

        // ============================================
        // Local IDs - ScheduleImage
        // ============================================
        mb.Entity<ScheduleImage>()
            .HasIndex(s => new { s.SchoolId, s.SectionId, s.Type })
            .IsUnique()
            .HasFilter("[SectionId] IS NOT NULL")
            .HasDatabaseName("IX_ScheduleImage_SchoolId_SectionId_Type");

        mb.Entity<ScheduleImage>()
            .HasIndex(s => new { s.SchoolId, s.TeacherId, s.Type })
            .IsUnique()
            .HasFilter("[TeacherId] IS NOT NULL")
            .HasDatabaseName("IX_ScheduleImage_SchoolId_TeacherId_Type");

        // ============================================
        // Local IDs - Grade
        // ============================================
        mb.Entity<Grade>()
            .Property(g => g.LocalGradeNumber)
            .IsRequired();

        mb.Entity<Grade>()
            .HasIndex(g => new { g.SchoolId, g.LocalGradeNumber })
            .IsUnique();

        // ============================================
        // Local IDs - Section
        // ============================================
        mb.Entity<Section>()
            .Property(s => s.LocalSectionNumber)
            .IsRequired();

        mb.Entity<Section>()
            .HasIndex(s => new { s.GradeId, s.LocalSectionNumber })
            .IsUnique();

        // ============================================
        // تحويل Enums
        // ============================================
        
        mb.Entity<School>().Property(s => s.Type).HasConversion<string>().HasMaxLength(20);
        mb.Entity<EmployeeSchool>().Property(e => e.Role).HasConversion<string>().HasMaxLength(30);
        mb.Entity<StudentAttendance>().Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        mb.Entity<EmployeeAttendance>().Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        mb.Entity<Complaint>().Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        mb.Entity<Complaint>().Property(c => c.FromUserType).HasConversion<string>().HasMaxLength(20);
        mb.Entity<Warning>().Property(w => w.Type).HasConversion<string>().HasMaxLength(30);
        mb.Entity<Announcement>().Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
        mb.Entity<Announcement>().Property(a => a.Audience).HasConversion<string>().HasMaxLength(20);
        mb.Entity<ActivityRegistration>().Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        mb.Entity<Notification>().Property(n => n.UserType).HasConversion<string>().HasMaxLength(20);

        // Enums المكتبة - تخزين كـ int
        mb.Entity<BookLoan>()
            .Property(l => l.Status)
            .HasConversion<int>();

        mb.Entity<BookReservation>()
            .Property(r => r.Status)
            .HasConversion<int>();

        mb.Entity<BookLoanRequest>()
            .Property(r => r.Status)
            .HasConversion<int>();

        // ============================================
        // FcmToken - ✅ إضافة تكوين الجدول الجديد
        // ============================================
        
        // mb.Entity<FcmToken>()
        //     .HasIndex(t => t.Token)
        //     .IsUnique()
        //     .HasDatabaseName("IX_FcmToken_Token");

        // mb.Entity<FcmToken>()
        //     .HasIndex(t => new { t.UserId, t.UserType, t.IsActive })
        //     .HasDatabaseName("IX_FcmToken_UserId_UserType_IsActive");

        // mb.Entity<FcmToken>()
        //     .Property(t => t.Token)
        //     .IsRequired()
        //     .HasMaxLength(500);

        // mb.Entity<FcmToken>()
        //     .Property(t => t.Platform)
        //     .HasMaxLength(50);

        // mb.Entity<FcmToken>()
        //     .Property(t => t.DeviceName)
        //     .HasMaxLength(100);

        // mb.Entity<FcmToken>()
        //     .Property(t => t.AppVersion)
        //     .HasMaxLength(20);

        // ============================================
        // العلاقات (Relationships)
        // ============================================

        // -------------------- EmployeeSchool --------------------
        mb.Entity<EmployeeSchool>()
            .HasKey(es => es.Id);

        mb.Entity<EmployeeSchool>()
            .HasIndex(es => new { es.EmployeeId, es.SchoolId })
            .IsUnique();

        mb.Entity<EmployeeSchool>()
            .HasIndex(es => new { es.SchoolId, es.LocalEmployeeNumber })
            .IsUnique();

        mb.Entity<EmployeeSchool>()
            .HasOne(es => es.Employee)
            .WithMany(e => e.EmployeeSchools)
            .HasForeignKey(es => es.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<EmployeeSchool>()
            .HasOne(es => es.School)
            .WithMany(s => s.EmployeeSchools)
            .HasForeignKey(es => es.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        // -------------------- School --------------------
        mb.Entity<Grade>()
            .HasOne(g => g.School)
            .WithMany(s => s.Grades)
            .HasForeignKey(g => g.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Section>()
            .HasOne(s => s.School)
            .WithMany(sch => sch.Sections)
            .HasForeignKey(s => s.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Subject>()
            .HasOne(s => s.School)
            .WithMany(sch => sch.Subjects)
            .HasForeignKey(s => s.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        // -------------------- Grade --------------------
        mb.Entity<Section>()
            .HasOne(s => s.Grade)
            .WithMany(g => g.Sections)
            .HasForeignKey(s => s.GradeId)
            .OnDelete(DeleteBehavior.Restrict);

        // -------------------- Section --------------------
        mb.Entity<Section>()
            .HasOne(s => s.Counselor)
            .WithMany()
            .HasForeignKey(s => s.CounselorId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Student>()
            .HasOne(s => s.Section)
            .WithMany(sec => sec.Students)
            .HasForeignKey(s => s.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        // -------------------- Subject --------------------
        mb.Entity<Subject>()
            .HasOne(s => s.Teacher)
            .WithMany()
            .HasForeignKey(s => s.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        // -------------------- TeacherSubject --------------------
        mb.Entity<TeacherSubject>()
            .HasIndex(t => new { t.TeacherId, t.SubjectId })
            .IsUnique();

        mb.Entity<TeacherSubject>()
            .HasOne(t => t.Teacher)
            .WithMany(e => e.TeacherSubjects)
            .HasForeignKey(t => t.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<TeacherSubject>()
            .HasOne(t => t.Subject)
            .WithMany(s => s.TeacherSubjects)
            .HasForeignKey(t => t.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        // -------------------- TeacherGrade --------------------
        mb.Entity<TeacherGrade>()
            .HasIndex(t => new { t.TeacherId, t.SubjectId, t.SectionId })
            .IsUnique();

        mb.Entity<TeacherGrade>()
            .HasOne(t => t.Teacher)
            .WithMany(e => e.TeacherGrades)
            .HasForeignKey(t => t.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<TeacherGrade>()
            .HasOne(t => t.Subject)
            .WithMany(s => s.TeacherGrades)
            .HasForeignKey(t => t.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<TeacherGrade>()
            .HasOne(t => t.Section)
            .WithMany(s => s.TeacherGrades)
            .HasForeignKey(t => t.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        // -------------------- TeacherAssignment --------------------
        mb.Entity<TeacherAssignment>().HasKey(t => new { t.EmployeeId, t.SchoolId });

        // ============================================
        // StudentGradeHistory
        // ============================================
        mb.Entity<StudentGradeHistory>()
            .HasIndex(s => new { s.StudentId, s.GradeId, s.AcademicYear })
            .IsUnique();

        mb.Entity<StudentGradeHistory>()
            .HasOne(s => s.Student)
            .WithMany(st => st.GradeHistory)
            .HasForeignKey(s => s.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<StudentGradeHistory>()
            .HasOne(s => s.Grade)
            .WithMany()
            .HasForeignKey(s => s.GradeId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<StudentGradeHistory>()
            .HasOne(s => s.Section)
            .WithMany()
            .HasForeignKey(s => s.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        // ============================================
        // QuizMark
        // ============================================
        mb.Entity<QuizMark>()
            .HasIndex(q => new { q.StudentId, q.SubjectId, q.Semester, q.QuizNumber })
            .IsUnique();

        mb.Entity<QuizMark>()
            .HasOne(q => q.Student)
            .WithMany()
            .HasForeignKey(q => q.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<QuizMark>()
            .HasOne(q => q.Subject)
            .WithMany()
            .HasForeignKey(q => q.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<QuizMark>()
            .HasOne(q => q.EnteredBy)
            .WithMany()
            .HasForeignKey(q => q.EnteredById)
            .OnDelete(DeleteBehavior.Restrict);

        // ============================================
        // OtpCode
        // ============================================
        mb.Entity<OtpCode>()
            .HasIndex(o => new { o.PhoneNumber, o.Code })
            .IsUnique();

        mb.Entity<OtpCode>()
            .HasIndex(o => o.PhoneNumber);

        mb.Entity<OtpCode>()
            .HasIndex(o => o.ExpiresAt);

        // -------------------- Student --------------------
        mb.Entity<Student>()
            .HasOne(s => s.School)
            .WithMany()
            .HasForeignKey(s => s.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        // -------------------- ScheduleImage --------------------
        mb.Entity<ScheduleImage>()
            .HasOne(s => s.School)
            .WithMany()
            .HasForeignKey(s => s.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        
        mb.Entity<ScheduleImage>()
            .HasOne(s => s.Section)
            .WithMany()
            .HasForeignKey(s => s.SectionId)
            .OnDelete(DeleteBehavior.Restrict);
        
        mb.Entity<ScheduleImage>()
            .HasOne(s => s.Teacher)
            .WithMany()
            .HasForeignKey(s => s.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        // -------------------- StudentAttendance --------------------
        mb.Entity<StudentAttendance>()
            .HasOne(a => a.Student)
            .WithMany()
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<StudentAttendance>()
            .HasOne(a => a.Section)
            .WithMany()
            .HasForeignKey(a => a.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<StudentAttendance>()
            .HasIndex(a => new { a.StudentId, a.Date })
            .IsUnique();

        // -------------------- EmployeeAttendance --------------------
        mb.Entity<EmployeeAttendance>()
            .HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<EmployeeAttendance>()
            .HasIndex(a => new { a.EmployeeId, a.Date })
            .IsUnique();

        // -------------------- Leave --------------------
        mb.Entity<Leave>()
            .HasOne(l => l.Employee)
            .WithMany()
            .HasForeignKey(l => l.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // -------------------- Mark --------------------
        mb.Entity<Mark>()
            .HasOne(m => m.Student)
            .WithMany()
            .HasForeignKey(m => m.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Mark>()
            .HasOne(m => m.Subject)
            .WithMany()
            .HasForeignKey(m => m.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Mark>()
            .HasIndex(m => new { m.StudentId, m.SubjectId, m.Semester })
            .IsUnique();

        // -------------------- MarkConfig --------------------
        mb.Entity<MarkConfig>().HasIndex(c => c.SchoolId).IsUnique();

        // -------------------- ReportCard --------------------
        mb.Entity<ReportCard>()
            .HasOne(r => r.Student)
            .WithMany()
            .HasForeignKey(r => r.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<ReportCard>()
            .HasMany(r => r.Subjects)
            .WithOne()
            .HasForeignKey(s => s.ReportCardId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<ReportCard>()
            .HasIndex(r => new { r.StudentId, r.Semester, r.Year })
            .IsUnique();

        // -------------------- Book --------------------
        mb.Entity<Book>()
            .HasOne(b => b.School)
            .WithMany()
            .HasForeignKey(b => b.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        // -------------------- BookLoan --------------------
        mb.Entity<BookLoan>()
            .HasOne(l => l.Book)
            .WithMany()
            .HasForeignKey(l => l.BookId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<BookLoan>()
            .HasOne(l => l.Student)
            .WithMany()
            .HasForeignKey(l => l.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // -------------------- BookReservation --------------------
        mb.Entity<BookReservation>()
            .HasOne(r => r.Book)
            .WithMany()
            .HasForeignKey(r => r.BookId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<BookReservation>()
            .HasOne(r => r.Student)
            .WithMany()
            .HasForeignKey(r => r.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // -------------------- BookLoanRequest --------------------
        mb.Entity<BookLoanRequest>()
            .HasOne(r => r.Book)
            .WithMany()
            .HasForeignKey(r => r.BookId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<BookLoanRequest>()
            .HasOne(r => r.Student)
            .WithMany()
            .HasForeignKey(r => r.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // -------------------- Activity --------------------
        mb.Entity<Activity>()
            .HasOne(a => a.School)
            .WithMany()
            .HasForeignKey(a => a.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Activity>()
            .HasOne(a => a.Supervisor)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        // -------------------- ActivityRegistration --------------------
        mb.Entity<ActivityRegistration>()
            .HasOne(r => r.Activity)
            .WithMany()
            .HasForeignKey(r => r.ActivityId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<ActivityRegistration>()
            .HasOne(r => r.Student)
            .WithMany()
            .HasForeignKey(r => r.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<ActivityRegistration>()
            .HasIndex(r => new { r.ActivityId, r.StudentId })
            .IsUnique();

        // -------------------- Complaint --------------------
        mb.Entity<Complaint>()
            .HasOne(c => c.School)
            .WithMany()
            .HasForeignKey(c => c.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        // -------------------- Punishment --------------------
        mb.Entity<Punishment>()
            .HasOne(p => p.School)
            .WithMany()
            .HasForeignKey(p => p.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Punishment>()
            .HasOne(p => p.Student)
            .WithMany()
            .HasForeignKey(p => p.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Punishment>()
            .HasOne(p => p.Employee)
            .WithMany()
            .HasForeignKey(p => p.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // -------------------- Announcement --------------------
        mb.Entity<Announcement>()
            .HasOne(a => a.School)
            .WithMany()
            .HasForeignKey(a => a.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        // ============================================
        // سلوك الحذف الافتراضي
        // ============================================
        
        foreach (var fk in mb.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
        {
            if (fk.DeclaringEntityType.ClrType == typeof(ReportCard) && 
                fk.PrincipalEntityType.ClrType == typeof(ReportCardSubject))
                continue;
                
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}