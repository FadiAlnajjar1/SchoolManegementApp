using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Auth;
using SchoolManagement.Api.Data;
using SchoolManagement.Api.Dtos;
using SchoolManagement.Api.Models;
using SchoolManagement.Api.Services;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    TokenService tokens,
    OtpService otpService,
    NotificationService notifier) : ControllerBase
{
    // ============================================
    // 1. تسجيل الدخول بالبريد الإلكتروني + كلمة المرور
    // ============================================

   [HttpPost("login")]
public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
{
    var email = request.Email.Trim().ToLowerInvariant();

    // 1. Admin
    var admin = await db.Admins.FirstOrDefaultAsync(a => a.Email == email);
    if (admin is not null && BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash))
    {
        return new LoginResponse(
            tokens.CreateToken(admin.Id, admin.Name, admin.Email, Roles.Admin, null),
            UserType.Admin, 
            Roles.Admin, 
            admin.Id, 
            admin.Name, 
            null,
            null  // ✅ LocalId للمستخدم (Admin ليس له LocalId)
        );
    }

    // 2. Employee
    var employee = await db.Employees.FirstOrDefaultAsync(e => e.Email == email);
    if (employee is not null && BCrypt.Net.BCrypt.Verify(request.Password, employee.PasswordHash))
    {
        if (employee.IsDismissed)
            return Unauthorized(new { message = "هذا الموظف مفصول من العمل" });

        var employeeSchool = await db.EmployeeSchools
            .Include(es => es.School)
            .FirstOrDefaultAsync(es => es.EmployeeId == employee.Id && es.IsActive);

        if (employeeSchool is null)
            return Unauthorized(new { message = "الموظف غير مرتبط بأي مدرسة" });

        var role = employeeSchool.Role.ToString();
        var schoolId = employeeSchool.SchoolId;
        var localEmployeeNumber = employeeSchool.LocalEmployeeNumber; // ✅ Local ID

        return new LoginResponse(
            tokens.CreateToken(employee.Id, employee.Name, employee.Email, role, schoolId),
            UserType.Employee, 
            role, 
            employee.Id, 
            employee.Name, 
            schoolId,
            localEmployeeNumber  // ✅ إضافة LocalEmployeeNumber
        );
    }

    // 3. Student
    var student = await db.Students.FirstOrDefaultAsync(s => s.Email == email);
    if (student is not null && BCrypt.Net.BCrypt.Verify(request.Password, student.PasswordHash))
    {
        if (student.SchoolId == 0 || student.SchoolId == null)
            return Unauthorized(new { message = "الطالب غير مسجل في أي مدرسة" });

        var localStudentNumber = student.LocalStudentNumber; // ✅ Local ID

        return new LoginResponse(
            tokens.CreateToken(student.Id, student.Name, student.Email, Roles.Student, student.SchoolId),
            UserType.Student, 
            Roles.Student, 
            student.Id, 
            student.Name, 
            student.SchoolId,
            localStudentNumber  // ✅ إضافة LocalStudentNumber
        );
    }

    return Unauthorized(new { message = "بيانات الدخول غير صحيحة" });
}

    // ============================================
    // 2. طلب كود التحقق (OTP) - عبر رقم الهاتف
    // ============================================

    // [HttpPost("otp/request")]
    // public async Task<IActionResult> RequestOtp(OtpRequest request)
    // {
    //     if (string.IsNullOrWhiteSpace(request.PhoneNumber))
    //         return BadRequest(new { success = false, message = "رقم الهاتف مطلوب" });

    //     object? user = null;
    //     string? userName = null;
    //     int userId = 0;
    //     string? phoneNumber = null;

    //     switch (request.UserType)
    //     {
    //         case UserType.Student:
    //             var student = await db.Students
    //                 .FirstOrDefaultAsync(s => s.GuardianPhone == request.PhoneNumber && s.IsActive);
                
    //             if (student is null)
    //                 return NotFound(new { success = false, message = "لا يوجد طالب مرتبط بهذا الرقم" });
                
    //             user = student;
    //             userName = student.Name;
    //             userId = student.Id;
    //             phoneNumber = student.GuardianPhone;
    //             break;

    //         case UserType.Employee:
    //             var employee = await db.Employees
    //                 .FirstOrDefaultAsync(e => e.Phone == request.PhoneNumber && !e.IsDismissed);
                
    //             if (employee is null)
    //                 return NotFound(new { success = false, message = "لا يوجد موظف بهذا الرقم" });

    //             var employeeSchool = await db.EmployeeSchools
    //                 .Include(es => es.School)
    //                 .FirstOrDefaultAsync(es => es.EmployeeId == employee.Id && es.IsActive);

    //             if (employeeSchool is null)
    //                 return NotFound(new { success = false, message = "الموظف غير مرتبط بأي مدرسة" });

    //             var isAdmin = await db.Admins.AnyAsync(a => a.Id == employee.Id);
    //             if (isAdmin)
    //                 return BadRequest(new { success = false, message = "غير مسموح للمدراء بتسجيل الدخول عبر OTP" });

    //             var roleName = employeeSchool.Role.ToString();
    //             if (roleName == Roles.Manager)
    //                 return BadRequest(new { success = false, message = "غير مسموح للمدير بتسجيل الدخول عبر OTP" });

    //             if (roleName == Roles.Secretary)
    //                 return BadRequest(new { success = false, message = "غير مسموح لأمين السجل بتسجيل الدخول عبر OTP" });

    //             user = employee;
    //             userName = employee.Name;
    //             userId = employee.Id;
    //             phoneNumber = employee.Phone;
    //             break;

    //         default:
    //             return BadRequest(new { success = false, message = "نوع المستخدم غير مدعوم لتسجيل الدخول عبر OTP" });
    //     }

    //     var staticCode = otpService.GenerateStaticOtp(phoneNumber!);
    //     var expiresAt = DateTime.UtcNow.AddDays(365);

    //     var existingOtp = await db.OtpCodes
    //         .FirstOrDefaultAsync(o => o.PhoneNumber == phoneNumber && !o.IsUsed);

    //     if (existingOtp is not null)
    //     {
    //         existingOtp.Code = staticCode;
    //         existingOtp.ExpiresAt = expiresAt;
    //         existingOtp.CreatedAt = DateTime.UtcNow;
    //         existingOtp.Attempts = 0;
    //     }
    //     else
    //     {
    //         var otpRecord = new OtpCode
    //         {
    //             PhoneNumber = phoneNumber!,
    //             Code = staticCode,
    //             ExpiresAt = expiresAt,
    //             CreatedAt = DateTime.UtcNow,
    //             IsUsed = false,
    //             Attempts = 0
    //         };

    //         db.OtpCodes.Add(otpRecord);
    //     }

    //     await db.SaveChangesAsync();

    //     var userTypeText = request.UserType == UserType.Student ? "الطالب" : "الموظف";
    //     var message = $"رمز التحقق الثابت لـ{userTypeText} {userName} هو: {staticCode}";

    //     // ✅ إرسال الـ SMS باستخدام Twilio
    //     var smsSent = await smsService.SendSmsAsync(phoneNumber!, message);
        
    //     if (smsSent)
    //     {
    //         Console.WriteLine($"✅ SMS sent successfully to {phoneNumber}");
    //     }
    //     else
    //     {
    //         Console.WriteLine($"❌ Failed to send SMS to {phoneNumber}");
    //         // يمكنك إضافة منطق بديل هنا (مثل إرسال إشعار داخل التطبيق)
    //     }

    //     // ✅ أيضاً تسجيل في Console للتأكد
    //     Console.WriteLine($"📱 OTP generated for ({phoneNumber}) {userTypeText} {userName}: {staticCode}");

    //     return Ok(new
    //     {
    //         success = true,
    //         message = $"تم إرسال رمز التحقق إلى رقم {userTypeText}",
    //         data = new
    //         {
    //             isStatic = true
    //             // ⚠️ للتطوير فقط - لا ترسل الكود في الإنتاج
    //             // code = staticCode
    //         }
    //     });
    // }

    // // ============================================
    // // 3. التحقق من كود OTP وتسجيل الدخول
    // // ============================================

    // [HttpPost("otp/verify")]
    // public async Task<ActionResult<LoginResponse>> VerifyOtp(OtpVerifyRequest request)
    // {
    //     var otpRecord = await db.OtpCodes
    //         .FirstOrDefaultAsync(o => o.PhoneNumber == request.PhoneNumber && 
    //                                  o.Code == request.Code &&
    //                                  !o.IsUsed);

    //     if (otpRecord is null)
    //         return Unauthorized(new { success = false, message = "رمز التحقق غير صحيح" });

    //     if (otpRecord.ExpiresAt < DateTime.UtcNow)
    //         return Unauthorized(new { success = false, message = "رمز التحقق منتهي الصلاحية" });

    //     otpRecord.Attempts++;

    //     object? user = null;
    //     int userId = 0;
    //     string userName = "";
    //     string userEmail = "";
    //     int? schoolId = null;
    //     string role = "";
    //     string? fcmToken = null;

    //     switch (request.UserType)
    //     {
    //         case UserType.Student:
    //             var student = await db.Students
    //                 .FirstOrDefaultAsync(s => s.GuardianPhone == request.PhoneNumber && s.IsActive);
                
    //             if (student is null)
    //                 return NotFound(new { success = false, message = "الطالب غير موجود" });
                
    //             user = student;
    //             userId = student.Id;
    //             userName = student.Name;
    //             userEmail = student.Email;
    //             schoolId = student.SchoolId;
    //             role = Roles.Student;
    //             student.IsPhoneVerified = true;
    //             fcmToken = student.FcmToken;
    //             break;

    //         case UserType.Employee:
    //             var employee = await db.Employees
    //                 .FirstOrDefaultAsync(e => e.Phone == request.PhoneNumber && !e.IsDismissed);
                
    //             if (employee is null)
    //                 return NotFound(new { success = false, message = "الموظف غير موجود" });

    //             var employeeSchool = await db.EmployeeSchools
    //                 .Include(es => es.School)
    //                 .FirstOrDefaultAsync(es => es.EmployeeId == employee.Id && es.IsActive);

    //             if (employeeSchool is null)
    //                 return NotFound(new { success = false, message = "الموظف غير مرتبط بأي مدرسة" });

    //             var isAdmin = await db.Admins.AnyAsync(a => a.Id == employee.Id);
    //             if (isAdmin)
    //                 return BadRequest(new { success = false, message = "غير مسموح للمدراء بتسجيل الدخول عبر OTP" });

    //             var roleName = employeeSchool.Role.ToString();
    //             if (roleName == Roles.Manager)
    //                 return BadRequest(new { success = false, message = "غير مسموح للمدير بتسجيل الدخول عبر OTP" });

    //             if (roleName == Roles.Secretary)
    //                 return BadRequest(new { success = false, message = "غير مسموح لأمين السجل بتسجيل الدخول عبر OTP" });

    //             user = employee;
    //             userId = employee.Id;
    //             userName = employee.Name;
    //             userEmail = employee.Email;
    //             schoolId = employeeSchool.SchoolId;
    //             role = roleName;
    //             employee.IsPhoneVerified = true;
    //             fcmToken = employee.FcmToken;
    //             break;

    //         default:
    //             return BadRequest(new { success = false, message = "نوع المستخدم غير مدعوم" });
    //     }

    //     if (!string.IsNullOrEmpty(request.FcmToken))
    //     {
    //         switch (request.UserType)
    //         {
    //             case UserType.Student:
    //                 if (user is Student student)
    //                     student.FcmToken = request.FcmToken;
    //                 break;
    //             case UserType.Employee:
    //                 if (user is Employee employee)
    //                     employee.FcmToken = request.FcmToken;
    //                 break;
    //         }
    //     }

    //     otpRecord.IsUsed = true;
    //     await db.SaveChangesAsync();

    //     var token = tokens.CreateToken(userId, userName, userEmail, role, schoolId);

    //     return new LoginResponse(
    //         token,
    //         request.UserType,
    //         role,
    //         userId,
    //         userName,
    //         schoolId);
    // }

    // // ============================================
    // // 4. إعادة إرسال الكود (نفس الكود الثابت)
    // // ============================================

    // [HttpPost("otp/resend")]
    // public async Task<IActionResult> ResendOtp(OtpRequest request)
    // {
    //     bool userExists = false;
    //     string? userName = null;
    //     string? phoneNumber = null;

    //     switch (request.UserType)
    //     {
    //         case UserType.Student:
    //             var student = await db.Students
    //                 .FirstOrDefaultAsync(s => s.GuardianPhone == request.PhoneNumber && s.IsActive);
                
    //             if (student is not null)
    //             {
    //                 userExists = true;
    //                 userName = student.Name;
    //                 phoneNumber = student.GuardianPhone;
    //             }
    //             break;

    //         case UserType.Employee:
    //             var employee = await db.Employees
    //                 .FirstOrDefaultAsync(e => e.Phone == request.PhoneNumber && !e.IsDismissed);
                
    //             if (employee is not null)
    //             {
    //                 var employeeSchool = await db.EmployeeSchools
    //                     .FirstOrDefaultAsync(es => es.EmployeeId == employee.Id && es.IsActive);

    //                 if (employeeSchool is not null)
    //                 {
    //                     var isAdmin = await db.Admins.AnyAsync(a => a.Id == employee.Id);
    //                     var roleName = employeeSchool.Role.ToString();
                        
    //                     if (!isAdmin && 
    //                         roleName != Roles.Manager && 
    //                         roleName != Roles.Secretary)
    //                     {
    //                         userExists = true;
    //                         userName = employee.Name;
    //                         phoneNumber = employee.Phone;
    //                     }
    //                 }
    //             }
    //             break;

    //         default:
    //             return BadRequest(new { success = false, message = "نوع المستخدم غير مدعوم" });
    //     }

    //     if (!userExists || string.IsNullOrEmpty(phoneNumber))
    //         return NotFound(new { success = false, message = "لا يوجد مستخدم بهذا الرقم" });

    //     var existingOtp = await db.OtpCodes
    //         .FirstOrDefaultAsync(o => o.PhoneNumber == phoneNumber && !o.IsUsed);

    //     string code;
    //     if (existingOtp is not null)
    //     {
    //         code = existingOtp.Code;
    //         existingOtp.CreatedAt = DateTime.UtcNow;
    //         existingOtp.Attempts = 0;
    //     }
    //     else
    //     {
    //         code = otpService.GenerateStaticOtp(phoneNumber);
    //         var otpRecord = new OtpCode
    //         {
    //             PhoneNumber = phoneNumber,
    //             Code = code,
    //             ExpiresAt = DateTime.UtcNow.AddDays(365),
    //             CreatedAt = DateTime.UtcNow,
    //             IsUsed = false,
    //             Attempts = 0
    //         };
    //         db.OtpCodes.Add(otpRecord);
    //     }

    //     await db.SaveChangesAsync();

    //     var userTypeText = request.UserType == UserType.Student ? "الطالب" : "الموظف";
    //     var message = $"رمز التحقق الثابت لـ{userTypeText} {userName} هو: {code}";

    //     // ✅ إعادة إرسال الـ SMS
    //     await smsService.SendSmsAsync(phoneNumber, message);
    //     Console.WriteLine($"📱 Resent OTP to {phoneNumber} for {userTypeText} {userName}: {code}");

    //     return Ok(new
    //     {
    //         success = true,
    //         message = $"تم إعادة إرسال رمز التحقق الثابت لـ{userTypeText}",
    //         data = new
    //         {
    //             isStatic = true
    //         }
    //     });
    // }

    // // ============================================
    // // 5. التحقق من صلاحية الكود (بدون تسجيل دخول)
    // // ============================================

    // [HttpPost("otp/validate")]
    // public async Task<IActionResult> ValidateOtp(OtpVerifyRequest request)
    // {
    //     var otpRecord = await db.OtpCodes
    //         .FirstOrDefaultAsync(o => o.PhoneNumber == request.PhoneNumber && 
    //                                  o.Code == request.Code &&
    //                                  !o.IsUsed);

    //     if (otpRecord is null)
    //         return Ok(new { success = false, message = "رمز التحقق غير صحيح" });

    //     if (otpRecord.ExpiresAt < DateTime.UtcNow)
    //         return Ok(new { success = false, message = "رمز التحقق منتهي الصلاحية" });

    //     return Ok(new
    //     {
    //         success = true,
    //         message = "رمز التحقق صحيح",
    //         data = new
    //         {
    //             isValid = true
    //         }
    //     });
    // }
}