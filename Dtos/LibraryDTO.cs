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

    

    public int Copies { get; set; }
}
public class LoanRequestLocalRequest
{
    [Required]
    public int LocalBookNumber { get; set; }
    
    [Required]
    public int LocalStudentNumber { get; set; }
}
public class RejectLoanRequest
{
    public string? Reason { get; set; }
}
public class UpdateBookRequest
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public int? Copies { get; set; }
}