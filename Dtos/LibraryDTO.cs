// Dtos/ReservationRequest.cs
using System.ComponentModel.DataAnnotations;
using SchoolManagement.Api.Models;

namespace SchoolManagement.Api.Dtos;

public class ReservationRequest
{
    [Required]
    public int BookId { get; set; }
    
    [Required]
    public int MemberId { get; set; }
}
// Dtos/ReservationDecisionRequest.cs


public class ReservationDecisionRequest
{
    [Required]
    public ReservationStatus Status { get; set; }
}
// Dtos/LoanRequest.cs


public class LoanRequest
{
    [Required]
    public int BookId { get; set; }
    
    [Required]
    public int MemberId { get; set; }
    
    [Required]
    public DateOnly DueDate { get; set; }
}
// Dtos/MemberRequest.cs


public class MemberRequest
{
    [Required]
    public int StudentId { get; set; }
}
// Dtos/BookRequest.cs


public class BookRequest
{
    [Required]
    public string Title { get; set; } = string.Empty;
    
    public string? Author { get; set; }
    
    public string? Isbn { get; set; }
    
    [Required]
    [Range(1, int.MaxValue)]
    public int Copies { get; set; }
}