using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolManagement.Api.Models;

public class Book
{
    public int Id { get; set; }
    public int LocalBookNumber { get; set; }
    public int SchoolId { get; set; }
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public int Copies { get; set; }
    public int AvailableCopies { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public School? School { get; set; }
}




public class BookLoan
{
    public int Id { get; set; }
    public int LocalLoanNumber { get; set; }
    public int BookId { get; set; }
    [Required]
    public int StudentId { get; set; }
    public Book? Book { get; set; }
    public DateOnly LoanDate { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly? ReturnDate { get; set; }
    public LoanStatus Status { get; set; } = LoanStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [ForeignKey(nameof(StudentId))]
    public virtual Student? Student { get; set; }
}


public class BookReservation
{
    public int Id { get; set; }
    public int BookId { get; set; }
    [Required]
    public int StudentId { get; set; }
    public Book? Book { get; set; }
    public DateOnly Date { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [ForeignKey(nameof(StudentId))]
    public virtual Student? Student { get; set; }
}
