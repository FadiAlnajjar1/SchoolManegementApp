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

       
    }
}