// Dtos/LoginRequest.cs
using System.ComponentModel.DataAnnotations;
using SchoolManagement.Api.Models;



public record LoginRequest(
    [Required] string Email,
    [Required] string Password
    // LoginMethod Method,
    // UserType? UserType,
    // string? FcmToken
);

public enum LoginMethod
{
    Password,
    OTP
}
// Dtos/OtpRequest.cs


public record OtpRequest(
    [Required, Phone] string PhoneNumber,
    [Required] UserType UserType
);
// Dtos/OtpVerifyRequest.cs


public record OtpVerifyRequest(
    [Required, Phone] string PhoneNumber,
    [Required, MinLength(6), MaxLength(6)] string Code,
    [Required] UserType UserType,
    string? FcmToken
);