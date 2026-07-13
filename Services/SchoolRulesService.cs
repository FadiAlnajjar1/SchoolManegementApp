using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Data;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Services;

public class SchoolRulesService(AppDbContext db)
{
    public static readonly EmployeeRole[] UniqueRoles =
    [
        EmployeeRole.Principal,
        EmployeeRole.Secretary,
        EmployeeRole.Librarian,
        EmployeeRole.ActivitySupervisor,
    ];

    // ============================================
    // التحقق من صلاحية التعيين في مدرسة
    // ============================================
    public async Task<string?> ValidateHireAsync(int schoolId, EmployeeRole role, int? excludeEmployeeId = null)
    {
        // 1. التحقق من وجود المدرسة
        if (!await db.Schools.AnyAsync(s => s.Id == schoolId))
            return "المدرسة غير موجودة";

        // 2. جلب الموظفين النشطين في هذه المدرسة (من خلال EmployeeSchool)
        var staffQuery = db.EmployeeSchools
            .Where(es => es.SchoolId == schoolId && es.IsActive && es.EmployeeId != excludeEmployeeId)
            .Join(db.Employees,
                es => es.EmployeeId,
                e => e.Id,
                (es, e) => new { EmployeeSchool = es, Employee = e })
            .Where(x => !x.Employee.IsDismissed);

        // 3. التحقق من وجود مدير في المدرسة
        var hasManager = await staffQuery.AnyAsync(x => x.EmployeeSchool.Role == EmployeeRole.Principal);

        // 4. إذا كان الدور مديراً
        if (role == EmployeeRole.Principal)
        {
            if (hasManager)
                return "يوجد مدير لهذه المدرسة بالفعل (مدير واحد فقط لكل مدرسة)";
        }
        else if (!hasManager)
        {
            return "لا يمكن إضافة أي موظف قبل تعيين مدير للمدرسة";
        }

        // 5. التحقق من الأدوار الفريدة (Principal, Secretary, Librarian, ActivitySupervisor)
        if (UniqueRoles.Contains(role))
        {
            var hasRole = await staffQuery.AnyAsync(x => x.EmployeeSchool.Role == role);
            if (hasRole)
                return $"يوجد موظف بدور {role} في هذه المدرسة بالفعل (واحد فقط مسموح)";
        }

        // 6. التحقق من عدد الموجهين (Counselor)
        if (role == EmployeeRole.Counselor)
        {
            var sections = await db.Sections.CountAsync(s => s.SchoolId == schoolId);
            var maxCounselors = Math.Max(1, (int)Math.Ceiling(sections / 12.0));
            var counselors = await staffQuery.CountAsync(x => x.EmployeeSchool.Role == EmployeeRole.Counselor);
            if (counselors >= maxCounselors)
                return $"عدد الموجهين مكتمل ({counselors}/{maxCounselors}) — يُسمح بموجه واحد لكل 12 شعبة";
        }

        return null;
    }

    // ============================================
    // التحقق من صلاحية حذف مدرسة
    // ============================================
    public async Task<string?> ValidateSchoolDeleteAsync(int schoolId)
    {
        // 1. التحقق من وجود طلاب في المدرسة
        if (await db.Students.AnyAsync(s => s.SchoolId == schoolId))
            return "لا يمكن حذف المدرسة وفيها طلاب";

        // 2. التحقق من وجود موظفين نشطين في المدرسة (باستثناء المدير)
        var hasActiveEmployees = await db.EmployeeSchools
            .Where(es => es.SchoolId == schoolId && es.IsActive)
            .Join(db.Employees,
                es => es.EmployeeId,
                e => e.Id,
                (es, e) => new { EmployeeSchool = es, Employee = e })
            .AnyAsync(x => x.EmployeeSchool.Role != EmployeeRole.Principal && !x.Employee.IsDismissed);

        if (hasActiveEmployees)
            return "لا يمكن حذف المدرسة وفيها موظفون — يجب حذف جميع الموظفين أولاً (المدير آخراً)";

        return null;
    }

    // ============================================
    // التحقق من صلاحية حذف موظف
    // ============================================
    public async Task<string?> ValidateEmployeeDeleteAsync(Employee employee)
    {
        // 1. جلب المدارس التي يكون فيها الموظف مديراً
        var principalSchools = await db.EmployeeSchools
            .Where(es => es.EmployeeId == employee.Id && es.Role == EmployeeRole.Principal && es.IsActive)
            .Select(es => es.SchoolId)
            .ToListAsync();

        if (!principalSchools.Any())
            return null;

        // 2. التحقق من وجود موظفين آخرين في هذه المدارس
        foreach (var schoolId in principalSchools)
        {
            var hasOtherEmployees = await db.EmployeeSchools
                .Where(es => es.SchoolId == schoolId && es.IsActive && es.EmployeeId != employee.Id)
                .Join(db.Employees,
                    es => es.EmployeeId,
                    e => e.Id,
                    (es, e) => e)
                .AnyAsync(e => !e.IsDismissed);

            if (hasOtherEmployees)
                return $"لا يمكن حذف المدير قبل حذف بقية موظفي المدرسة (المدير يُحذف آخراً)";
        }

        return null;
    }

    // ============================================
    // التحقق من صلاحية إضافة طالب
    // ============================================
    public async Task<string?> ValidateStudentCreatorAsync(int schoolId, EmployeeRole creatorRole, int? creatorEmployeeId = null)
    {
        if (creatorRole == EmployeeRole.Secretary)
            return null;

        if (creatorRole == EmployeeRole.Principal)
        {
            // التحقق من وجود أمين سر في المدرسة
            var hasSecretary = await db.EmployeeSchools
                .Where(es => es.SchoolId == schoolId && es.IsActive)
                .Join(db.Employees,
                    es => es.EmployeeId,
                    e => e.Id,
                    (es, e) => new { EmployeeSchool = es, Employee = e })
                .AnyAsync(x => x.EmployeeSchool.Role == EmployeeRole.Secretary && !x.Employee.IsDismissed);

            return hasSecretary
                ? "إضافة الطلاب من اختصاص أمين السر — يضيف المدير الطلاب فقط عند عدم وجود أمين سر"
                : null;
        }

        return "غير مصرح بإضافة طلاب";
    }

    // ============================================
    // التحقق من تسجيل حضور الحصة الثانية
    // ============================================
   public async Task<string?> ValidateSecondPeriodAttendanceTakenAsync(int teacherId)
{
    var today = DateOnly.FromDateTime(DateTime.Today);
    var day = (int)DateTime.Today.DayOfWeek; // 0=Sunday, 1=Monday, ...

    // ✅ جلب الشعب التي يدرسها المعلم في هذا اليوم (من خلال TeacherGrade)
    // لا يوجد نظام محدد للحصص، لذلك نتحقق من جميع الشعب التي يدرسها المعلم
    var sectionIds = await db.TeacherGrades
        .Where(tg => tg.TeacherId == teacherId)
        .Select(tg => tg.SectionId)
        .Distinct()
        .ToListAsync();

    // ✅ التحقق من وجود صورة جدول للمعلم
    var scheduleImage = await db.ScheduleImages
        .Where(s => 
                    s.TeacherId == teacherId && 
                    s.Type == ScheduleImageType.Teacher)
        .OrderByDescending(s => s.CreatedAt)
        .FirstOrDefaultAsync();

    // إذا لم توجد صورة جدول، نعتبر أن المعلم لديه حصص
    // أو يمكنك إرجاع رسالة مختلفة
    if (scheduleImage is null)
        return null; // لا يوجد جدول، لا يمكن التحقق

    // ✅ التحقق من أن اليوم موجود في أيام الأسبوع للجدول

    // ✅ التحقق من تسجيل حضور كل شعبة
    foreach (var sectionId in sectionIds)
    {
        var taken = await db.StudentAttendances
            .AnyAsync(a => a.SectionId == sectionId && a.Date == today);
        
        if (!taken)
            return "يجب تسجيل حضور الشعبة قبل إدخال العلامات أو التقارير";
    }

    return null;
}

    // ============================================
    // دوال مساعدة إضافية
    // ============================================

    // جلب موظفي مدرسة معينة
    public async Task<List<Employee>> GetSchoolEmployeesAsync(int schoolId)
    {
        return await db.EmployeeSchools
            .Where(es => es.SchoolId == schoolId && es.IsActive)
            .Join(db.Employees,
                es => es.EmployeeId,
                e => e.Id,
                (es, e) => e)
            .Where(e => !e.IsDismissed)
            .ToListAsync();
    }

    // جلب مدير مدرسة معينة
    public async Task<Employee?> GetSchoolManagerAsync(int schoolId)
    {
        return await db.EmployeeSchools
            .Where(es => es.SchoolId == schoolId && es.Role == EmployeeRole.Principal && es.IsActive)
            .Join(db.Employees,
                es => es.EmployeeId,
                e => e.Id,
                (es, e) => e)
            .FirstOrDefaultAsync(e => !e.IsDismissed);
    }

    // جلب جميع المدارس التي يعمل فيها موظف
    public async Task<List<int>> GetEmployeeSchoolsAsync(int employeeId)
    {
        return await db.EmployeeSchools
            .Where(es => es.EmployeeId == employeeId && es.IsActive)
            .Select(es => es.SchoolId)
            .ToListAsync();
    }

    // التحقق من أن موظف يعمل في مدرسة معينة
    public async Task<bool> IsEmployeeInSchoolAsync(int employeeId, int schoolId)
    {
        return await db.EmployeeSchools
            .AnyAsync(es => es.EmployeeId == employeeId && es.SchoolId == schoolId && es.IsActive);
    }

    // جلب دور موظف في مدرسة معينة
    public async Task<EmployeeRole?> GetEmployeeRoleInSchoolAsync(int employeeId, int schoolId)
    {
        return await db.EmployeeSchools
            .Where(es => es.EmployeeId == employeeId && es.SchoolId == schoolId && es.IsActive)
            .Select(es => es.Role)
            .FirstOrDefaultAsync();
    }

    // جلب جميع الموظفين بدور معين في مدرسة
    public async Task<List<Employee>> GetEmployeesByRoleInSchoolAsync(int schoolId, EmployeeRole role)
    {
        return await db.EmployeeSchools
            .Where(es => es.SchoolId == schoolId && es.Role == role && es.IsActive)
            .Join(db.Employees,
                es => es.EmployeeId,
                e => e.Id,
                (es, e) => e)
            .Where(e => !e.IsDismissed)
            .ToListAsync();
    }
}