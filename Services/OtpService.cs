using System.Security.Cryptography;

namespace SchoolManagement.Api.Services;

public class OtpService
{
    private readonly Random _random = new();

    // دالة إنشاء كود عشوائي من 6 خانات
    public string GenerateOtp()
    {
        return _random.Next(100000, 999999).ToString();
    }

    // دالة إنشاء كود ثابت للمستخدم (يعتمد على رقم الهاتف)
    public string GenerateStaticOtp(string phoneNumber)
    {
        // استخدام SHA256 لإنشاء كود ثابت من رقم الهاتف
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(phoneNumber + "SchoolSecretSalt"));
        
        // تحويل الـ hash إلى رقم من 6 خانات
        var numericHash = BitConverter.ToUInt64(hash, 0) % 1000000;
        return numericHash.ToString("D6");  // تأكد من أنها 6 خانات
    }

    public bool ValidateOtp(string code)
    {
        return code.Length == 6 && code.All(char.IsDigit);
    }
}