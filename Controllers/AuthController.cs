// Controllers/AuthController.cs
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
            // ✅ تسجيل FCM Token في الجدول المنفصل
            if (!string.IsNullOrEmpty(request.FcmToken))
            {
                await notifier.RegisterFcmTokenAsync(admin.Id, request.FcmToken);
            }

            var response = new LoginResponse(
                tokens.CreateToken(admin.Id, admin.Name, admin.Email, Roles.Admin, null),
                UserType.Admin,
                Roles.Admin,
                admin.Id,
                admin.Name,
                null,
                null,
                request.FcmToken
            );
            return Ok(response);
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

            // ✅ تسجيل FCM Token في الجدول المنفصل
            if (!string.IsNullOrEmpty(request.FcmToken))
            {
                await notifier.RegisterFcmTokenAsync(employee.Id, request.FcmToken);
            }

            var role = employeeSchool.Role.ToString();
            var schoolId = employeeSchool.SchoolId;
            var localEmployeeNumber = employeeSchool.LocalEmployeeNumber;

            var response = new LoginResponse(
                tokens.CreateToken(employee.Id, employee.Name, employee.Email, role, schoolId, localEmployeeNumber),
                UserType.Employee,
                role,
                employee.Id,
                employee.Name,
                schoolId,
                localEmployeeNumber,
                request.FcmToken
            );
            return Ok(response);
        }

        // 3. Student
        var student = await db.Students.FirstOrDefaultAsync(s => s.Email == email);
        if (student is not null && BCrypt.Net.BCrypt.Verify(request.Password, student.PasswordHash))
        {
            if (student.SchoolId == 0 || student.SchoolId == null)
                return Unauthorized(new { message = "الطالب غير مسجل في أي مدرسة" });

            // ✅ تسجيل FCM Token في الجدول المنفصل
            if (!string.IsNullOrEmpty(request.FcmToken))
            {
                await notifier.RegisterFcmTokenAsync(student.Id, request.FcmToken);
            }

            var localStudentNumber = student.LocalStudentNumber;

            var response = new LoginResponse(
                tokens.CreateToken(student.Id, student.Name, student.Email, Roles.Student, student.SchoolId, localStudentNumber),
                UserType.Student,
                Roles.Student,
                student.Id,
                student.Name,
                student.SchoolId,
                localStudentNumber,
                request.FcmToken
            );
            return Ok(response);
        }

        return Unauthorized(new { message = "بيانات الدخول غير صحيحة" });
    }
}