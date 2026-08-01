using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Auth;
using SchoolManagement.Api.Data;
using SchoolManagement.Api.Dtos;
using SchoolManagement.Api.Models;
using SchoolManagement.Api.Services;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/librarian")]
[Authorize(Roles = Roles.Librarian)]
public class LibrarianController(AppDbContext db, NotificationService notifier) : ControllerBase
{
    private int SchoolId => User.GetSchoolId();

    // ============================================
    // إدارة الكتب (Books)
    // ============================================

    [HttpPost("books")]
    public async Task<IActionResult> CreateBook(BookRequest request)
    {
        var maxLocalNumber = await db.Books
            .Where(b => b.SchoolId == SchoolId)
            .Select(b => (int?)b.LocalBookNumber)
            .MaxAsync() ?? 0;

        int newLocalNumber = maxLocalNumber + 1;

        var book = new Book
        {
            SchoolId = SchoolId,
            LocalBookNumber = newLocalNumber,
            Title = request.Title,
            Author = request.Author ?? "",
            Copies = request.Copies,
            AvailableCopies = request.Copies,
            ReservedCopies = 0,
            CreatedAt = DateTime.UtcNow
        };
        
        db.Books.Add(book);
        await db.SaveChangesAsync();
        
        return Created($"api/librarian/books/{book.LocalBookNumber}", new
        {
            success = true,
            message = "تم إضافة الكتاب بنجاح",
            data = new
            {
                book.Id,
                book.LocalBookNumber,
                book.Title,
                book.Author,
                book.Copies,
                book.AvailableCopies,
                book.ReservedCopies,
                AvailableForLoan = book.AvailableCopies - book.ReservedCopies,
                book.SchoolId,
                book.CreatedAt
            }
        });
    }

    [HttpGet("books")]
    public async Task<IActionResult> GetBooks()
    {
        var books = await db.Books
            .Where(b => b.SchoolId == SchoolId)
            .OrderBy(b => b.LocalBookNumber)
            .Select(b => new
            {
                b.Id,
                b.LocalBookNumber,
                b.Title,
                b.Author,
                b.Copies,
                b.AvailableCopies,
                b.ReservedCopies,
                AvailableForLoan = b.AvailableCopies - b.ReservedCopies,
                IsAvailable = (b.AvailableCopies - b.ReservedCopies) > 0,
                b.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب الكتب بنجاح",
            data = new
            {
                totalBooks = books.Count,
                availableBooks = books.Count(b => b.AvailableForLoan > 0),
                reservedBooks = books.Count(b => b.ReservedCopies > 0),
                books = books
            }
        });
    }

    [HttpGet("books/{localBookNumber:int}")]
    public async Task<IActionResult> GetBook(int localBookNumber)
    {
        var book = await db.Books
            .Where(b => b.SchoolId == SchoolId && 
                        b.LocalBookNumber == localBookNumber)
            .Select(b => new
            {
                b.Id,
                b.LocalBookNumber,
                b.Title,
                b.Author,
                b.Copies,
                b.AvailableCopies,
                b.ReservedCopies,
                AvailableForLoan = b.AvailableCopies - b.ReservedCopies,
                IsAvailable = (b.AvailableCopies - b.ReservedCopies) > 0,
                b.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (book is null)
            return NotFound(new { success = false, message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" });

        return Ok(new
        {
            success = true,
            message = "تم جلب الكتاب بنجاح",
            data = book
        });
    }

    [HttpPut("books/{localBookNumber:int}")]
    public async Task<IActionResult> UpdateBook(int localBookNumber, BookRequest request)
    {
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                      b.LocalBookNumber == localBookNumber);

        if (book is null)
            return NotFound(new { success = false, message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" });

        book.Title = request.Title;
        book.Author = request.Author ?? "";
        
        var borrowedCopies = book.Copies - book.AvailableCopies;
        book.Copies = request.Copies;
        book.AvailableCopies = request.Copies - borrowedCopies;
        if (book.AvailableCopies < 0) book.AvailableCopies = 0;

        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "تم تحديث الكتاب بنجاح",
            data = new
            {
                book.Id,
                book.LocalBookNumber,
                book.Title,
                book.Author,
                book.Copies,
                book.AvailableCopies,
                book.ReservedCopies,
                AvailableForLoan = book.AvailableCopies - book.ReservedCopies,
                book.CreatedAt
            }
        });
    }

    [HttpDelete("books/{localBookNumber:int}")]
    public async Task<IActionResult> DeleteBook(int localBookNumber)
    {
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                      b.LocalBookNumber == localBookNumber);

        if (book is null)
            return NotFound(new { success = false, message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" });

        var activeLoans = await db.BookLoans
            .AnyAsync(l => l.BookId == book.Id && l.Status == LoanStatus.Active);

        if (activeLoans)
            return BadRequest(new { success = false, message = "لا يمكن حذف الكتاب لأن هناك استعارات نشطة له" });

        db.BookReservations.RemoveRange(db.BookReservations.Where(r => r.BookId == book.Id));
        db.BookLoans.RemoveRange(db.BookLoans.Where(l => l.BookId == book.Id));
        db.Books.Remove(book);
        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = $"تم حذف الكتاب رقم {localBookNumber} بنجاح",
            data = new
            {
                localBookNumber = localBookNumber,
                title = book.Title
            }
        });
    }

    // ============================================
    // الحجوزات (Reservations)
    // ============================================

    // [HttpPost("reservations")]
    // public async Task<IActionResult> CreateReservation(ReservationLocalRequest request)
    // {
    //     var book = await db.Books
    //         .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
    //                                   b.LocalBookNumber == request.LocalBookNumber);
        
    //     if (book is null)
    //         return NotFound(new { success = false, message = $"لا يوجد كتاب برقم {request.LocalBookNumber} في المكتبة" });

    //     var student = await db.Students
    //         .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
    //                                   s.LocalStudentNumber == request.LocalStudentNumber);
        
    //     if (student is null)
    //         return NotFound(new { success = false, message = $"لا يوجد طالب برقم {request.LocalStudentNumber} في المدرسة" });

    //     // التحقق من وجود نسخ متاحة
    //     if (book.AvailableCopies <= 0)
    //         return BadRequest(new { 
    //             success = false, 
    //             message = "لا توجد نسخ متاحة من هذا الكتاب حالياً." 
    //         });

    //     var existingReservation = await db.BookReservations
    //         .AnyAsync(r => r.BookId == book.Id && 
    //                       r.StudentId == student.Id && 
    //                       r.Status == ReservationStatus.Pending);
        
    //     if (existingReservation)
    //         return BadRequest(new { 
    //             success = false, 
    //             message = "لديك حجز نشط لهذا الكتاب بالفعل" 
    //         });

    //     var hasActiveLoan = await db.BookLoans
    //         .AnyAsync(l => l.BookId == book.Id && 
    //                       l.StudentId == student.Id && 
    //                       l.Status == LoanStatus.Active);
        
    //     if (hasActiveLoan)
    //         return BadRequest(new { 
    //             success = false, 
    //             message = "الكتاب مستعار من قبلك بالفعل" 
    //         });

    //     var reservation = new BookReservation
    //     {
    //         BookId = book.Id,
    //         StudentId = student.Id,
    //         Date = DateOnly.FromDateTime(DateTime.Today),
    //         ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
    //         Status = ReservationStatus.Pending,
    //         CreatedAt = DateTime.UtcNow
    //     };

    //     book.ReservedCopies++;

    //     db.BookReservations.Add(reservation);
    //     await db.SaveChangesAsync();

    //     await notifier.SendAsync(student.Id, UserType.Student,
    //         "تم حجز كتاب",
    //         $"تم حجز كتاب \"{book.Title}\" بنجاح. يجب عليك استعارته خلال 7 أيام وإلا سيتم إلغاء الحجز تلقائياً.",
    //         "library_reservation");

    //     return Created($"api/librarian/reservations/{reservation.Id}", new
    //     {
    //         success = true,
    //         message = "تم الحجز بنجاح. لديك 7 أيام لاستعارة الكتاب.",
    //         data = new
    //         {
    //             reservation.Id,
    //             LocalBookNumber = book.LocalBookNumber,
    //             BookTitle = book.Title,
    //             LocalStudentNumber = student.LocalStudentNumber,
    //             StudentName = student.Name,
    //             reservation.Date,
    //             reservation.ExpiryDate,
    //             reservation.Status,
    //             reservation.CreatedAt,
    //             AvailableCopies = book.AvailableCopies,
    //             ReservedCopies = book.ReservedCopies,
    //             AvailableForLoan = book.AvailableCopies - book.ReservedCopies
    //         }
    //     });
    // }

    [HttpGet("reservations")]
public async Task<IActionResult> GetReservations(
    [FromQuery] ReservationStatus? status = null)
{
    var query = db.BookReservations
        .Include(r => r.Book)
        .Include(r => r.Student)
            .ThenInclude(s => s!.Section)
                .ThenInclude(sec => sec!.Grade)
        .Where(r => r.Book!.SchoolId == SchoolId);

    if (status.HasValue)
        query = query.Where(r => r.Status == status);

    var today = DateOnly.FromDateTime(DateTime.Today);

    var reservations = await query
        .OrderByDescending(r => r.Date)
        .Select(r => new
        {
            r.Id,
            LocalBookNumber = r.Book != null ? r.Book.LocalBookNumber : 0,
            BookTitle = r.Book != null ? r.Book.Title : null,
            BookAuthor = r.Book != null ? r.Book.Author : null,
            LocalStudentNumber = r.Student != null ? r.Student.LocalStudentNumber : 0,
            StudentName = r.Student != null ? r.Student.Name : null,
            SectionName = r.Student != null && r.Student.Section != null ? 
                r.Student.Section.Name : null,
            LocalSectionNumber = r.Student != null && r.Student.Section != null ? 
                r.Student.Section.LocalSectionNumber : 0,
            GradeName = r.Student != null && r.Student.Section != null && r.Student.Section.Grade != null ? 
                r.Student.Section.Grade.Name : null,
            LocalGradeNumber = r.Student != null && r.Student.Section != null && r.Student.Section.Grade != null ? 
                r.Student.Section.Grade.LocalGradeNumber : 0,
            r.Date,
            r.ExpiryDate,
            IsExpired = r.ExpiryDate < today && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Approved),
            r.Status,
            StatusName = r.Status.ToString(),
            StatusArabic = r.Status == ReservationStatus.Pending ? "قيد الانتظار" :
                          r.Status == ReservationStatus.Approved ? "تمت الموافقة (في انتظار الاستعارة)" :
                          r.Status == ReservationStatus.Rejected ? "مرفوض" :
                          r.Status == ReservationStatus.Fulfilled ? "تم الاستعارة" :
                          r.Status == ReservationStatus.Cancelled ? "ملغي" : "غير معروف",
            r.RejectionReason,
            r.CreatedAt,
            r.UpdatedAt
        })
        .ToListAsync();

    var totalCount = reservations.Count;
    var pendingCount = reservations.Count(r => r.Status == ReservationStatus.Pending);
    var approvedCount = reservations.Count(r => r.Status == ReservationStatus.Approved);
    var rejectedCount = reservations.Count(r => r.Status == ReservationStatus.Rejected);
    var fulfilledCount = reservations.Count(r => r.Status == ReservationStatus.Fulfilled);
    var cancelledCount = reservations.Count(r => r.Status == ReservationStatus.Cancelled);
    var expiredCount = reservations.Count(r => r.IsExpired);

    return Ok(new
    {
        success = true,
        message = "تم جلب الحجوزات بنجاح",
        data = new
        {
            totalCount,
            pendingCount,
            approvedCount,
            rejectedCount,
            fulfilledCount,
            cancelledCount,
            expiredCount,
            reservations
        }
    });
}

    [HttpGet("reservations/book/{localBookNumber:int}")]
    public async Task<IActionResult> GetReservationsByBook(
        int localBookNumber,
        [FromQuery] ReservationStatus? status = null)
    {
        var book = await db.Books
            .Where(b => b.SchoolId == SchoolId && 
                        b.LocalBookNumber == localBookNumber)
            .Select(b => new { b.Id, b.Title, b.LocalBookNumber, b.Author, b.AvailableCopies, b.ReservedCopies })
            .FirstOrDefaultAsync();

        if (book is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" 
            });

        var query = db.BookReservations
            .Include(r => r.Student)
                .ThenInclude(s => s!.Section)
                    .ThenInclude(sec => sec!.Grade)
            .Where(r => r.BookId == book.Id);

        if (status.HasValue)
            query = query.Where(r => r.Status == status);

        var today = DateOnly.FromDateTime(DateTime.Today);

        var reservations = await query
            .OrderByDescending(r => r.Date)
            .Select(r => new
            {
                r.Id,
                LocalStudentNumber = r.Student != null ? r.Student.LocalStudentNumber : 0,
                StudentName = r.Student != null ? r.Student.Name : null,
                SectionName = r.Student != null && r.Student.Section != null ? 
                    r.Student.Section.Name : null,
                LocalSectionNumber = r.Student != null && r.Student.Section != null ? 
                    r.Student.Section.LocalSectionNumber : 0,
                GradeName = r.Student != null && r.Student.Section != null && r.Student.Section.Grade != null ? 
                    r.Student.Section.Grade.Name : null,
                LocalGradeNumber = r.Student != null && r.Student.Section != null && r.Student.Section.Grade != null ? 
                    r.Student.Section.Grade.LocalGradeNumber : 0,
                r.Date,
                r.ExpiryDate,
                IsExpired = r.ExpiryDate < today && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Approved),
                r.Status,
                StatusName = r.Status.ToString(),
                StatusArabic = r.Status == ReservationStatus.Pending ? "قيد الانتظار" :
                              r.Status == ReservationStatus.Approved ? "تمت الموافقة (في انتظار الاستعارة)" :
                              r.Status == ReservationStatus.Rejected ? "مرفوض" :
                              r.Status == ReservationStatus.Fulfilled ? "تم الاستعارة" :
                              r.Status == ReservationStatus.Cancelled ? "ملغي" : "غير معروف",
                r.RejectionReason,
                r.CreatedAt,
                r.UpdatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = $"تم جلب حجوزات الكتاب {book.Title} بنجاح",
            data = new
            {
                Book = new
                {
                    book.Id,
                    book.Title,
                    book.LocalBookNumber,
                    book.Author,
                    AvailableCopies = book.AvailableCopies,
                    ReservedCopies = book.ReservedCopies,
                    AvailableForLoan = book.AvailableCopies - book.ReservedCopies
                },
                totalCount = reservations.Count,
                pendingCount = reservations.Count(r => r.Status == ReservationStatus.Pending),
                approvedCount = reservations.Count(r => r.Status == ReservationStatus.Approved),
                rejectedCount = reservations.Count(r => r.Status == ReservationStatus.Rejected),
                fulfilledCount = reservations.Count(r => r.Status == ReservationStatus.Fulfilled),
                cancelledCount = reservations.Count(r => r.Status == ReservationStatus.Cancelled),
                expiredCount = reservations.Count(r => r.IsExpired),
                reservations = reservations
            }
        });
    }

    [HttpGet("reservations/student/{localStudentNumber:int}")]
    public async Task<IActionResult> GetReservationsByStudent(
        int localStudentNumber,
        [FromQuery] ReservationStatus? status = null)
    {
        var student = await db.Students
            .Where(s => s.SchoolId == SchoolId && 
                        s.LocalStudentNumber == localStudentNumber)
            .Select(s => new { s.Id, s.Name, s.LocalStudentNumber })
            .FirstOrDefaultAsync();

        if (student is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد طالب برقم {localStudentNumber} في المدرسة" 
            });

        var query = db.BookReservations
            .Include(r => r.Book)
            .Where(r => r.StudentId == student.Id);

        if (status.HasValue)
            query = query.Where(r => r.Status == status);

        var today = DateOnly.FromDateTime(DateTime.Today);

        var reservations = await query
            .OrderByDescending(r => r.Date)
            .Select(r => new
            {
                r.Id,
                LocalBookNumber = r.Book != null ? r.Book.LocalBookNumber : 0,
                BookTitle = r.Book != null ? r.Book.Title : null,
                BookAuthor = r.Book != null ? r.Book.Author : null,
                r.Date,
                r.ExpiryDate,
                IsExpired = r.ExpiryDate < today && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Approved),
                r.Status,
                StatusName = r.Status.ToString(),
                StatusArabic = r.Status == ReservationStatus.Pending ? "قيد الانتظار" :
                              r.Status == ReservationStatus.Approved ? "تمت الموافقة (في انتظار الاستعارة)" :
                              r.Status == ReservationStatus.Rejected ? "مرفوض" :
                              r.Status == ReservationStatus.Fulfilled ? "تم الاستعارة" :
                              r.Status == ReservationStatus.Cancelled ? "ملغي" : "غير معروف",
                r.RejectionReason,
                r.CreatedAt,
                r.UpdatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = $"تم جلب حجوزات الطالب {student.Name} بنجاح",
            data = new
            {
                Student = new
                {
                    student.Id,
                    student.Name,
                    student.LocalStudentNumber
                },
                totalCount = reservations.Count,
                pendingCount = reservations.Count(r => r.Status == ReservationStatus.Pending),
                approvedCount = reservations.Count(r => r.Status == ReservationStatus.Approved),
                rejectedCount = reservations.Count(r => r.Status == ReservationStatus.Rejected),
                fulfilledCount = reservations.Count(r => r.Status == ReservationStatus.Fulfilled),
                cancelledCount = reservations.Count(r => r.Status == ReservationStatus.Cancelled),
                expiredCount = reservations.Count(r => r.IsExpired),
                reservations = reservations
            }
        });
    }

    // ✅ قبول الحجز (تغيير الحالة إلى Approved)
    [HttpPut("reservations/{localBookNumber:int}/{localStudentNumber:int}/approve")]
    public async Task<IActionResult> ApproveReservation(
        int localBookNumber,
        int localStudentNumber)
    {
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                      b.LocalBookNumber == localBookNumber);
        
        if (book is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" 
            });

        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalStudentNumber == localStudentNumber);
        
        if (student is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد طالب برقم {localStudentNumber} في المدرسة" 
            });

        var reservation = await db.BookReservations
            .Include(r => r.Book)
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.BookId == book.Id && 
                                      r.StudentId == student.Id && 
                                      r.Status == ReservationStatus.Pending);

        if (reservation is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد حجز معلق للطالب {localStudentNumber} على الكتاب {localBookNumber}" 
            });

        // التحقق من أن الحجز لم ينتهِ
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (reservation.ExpiryDate < today)
        {
            reservation.Status = ReservationStatus.Cancelled;
            reservation.RejectionReason = "انتهت مهلة الحجز (7 أيام)";
            reservation.UpdatedAt = DateTime.UtcNow;
            book.ReservedCopies--;
            
            await db.SaveChangesAsync();
            
            return BadRequest(new { 
                success = false, 
                message = "انتهت مهلة الحجز (7 أيام). تم إلغاء الحجز تلقائياً." 
            });
        }

        // ✅ تغيير الحالة إلى Approved (تمت الموافقة ولكن لم تستعار بعد)
        reservation.Status = ReservationStatus.Approved;
        reservation.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // إشعار للطالب
        await notifier.SendAsync(student.Id, UserType.Student,
            "تمت الموافقة على حجز كتاب",
            $"تمت الموافقة على حجزك لكتاب \"{book.Title}\". يرجى التوجه إلى المكتبة لاستعارة الكتاب خلال {reservation.ExpiryDate:yyyy-MM-dd}",
            "reservation_approved");

        return Ok(new
        {
            success = true,
            message = "تمت الموافقة على الحجز بنجاح. يرجى انتظار الطالب لاستعارة الكتاب.",
            data = new
            {
                Student = new
                {
                    student.Id,
                    student.Name,
                    student.LocalStudentNumber
                },
                Book = new
                {
                    book.Id,
                    book.Title,
                    book.LocalBookNumber,
                    book.Author,
                    AvailableCopies = book.AvailableCopies,
                    ReservedCopies = book.ReservedCopies,
                    AvailableForLoan = book.AvailableCopies - book.ReservedCopies
                },
                Reservation = new
                {
                    reservation.Id,
                    reservation.Status,
                    reservation.Date,
                    reservation.ExpiryDate,
                    reservation.UpdatedAt
                }
            }
        });
    }

    // ✅ رفض الحجز
    [HttpPut("reservations/{localBookNumber:int}/{localStudentNumber:int}/reject")]
    public async Task<IActionResult> RejectReservation(
        int localBookNumber,
        int localStudentNumber,
        [FromBody] RejectReservationRequest? request = null)
    {
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                      b.LocalBookNumber == localBookNumber);
        
        if (book is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" 
            });

        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalStudentNumber == localStudentNumber);
        
        if (student is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد طالب برقم {localStudentNumber} في المدرسة" 
            });

        var reservation = await db.BookReservations
            .Include(r => r.Book)
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.BookId == book.Id && 
                                      r.StudentId == student.Id && 
                                      r.Status == ReservationStatus.Pending);

        if (reservation is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد حجز معلق للطالب {localStudentNumber} على الكتاب {localBookNumber}" 
            });

        reservation.Status = ReservationStatus.Rejected;
        reservation.RejectionReason = request?.Reason ?? "تم رفض الحجز من قبل أمين المكتبة";
        reservation.UpdatedAt = DateTime.UtcNow;
        
        book.ReservedCopies--;

        await db.SaveChangesAsync();

        await notifier.SendAsync(student.Id, UserType.Student,
            "تم رفض حجز كتاب",
            $"تم رفض حجزك لكتاب \"{book.Title}\". السبب: {reservation.RejectionReason}",
            "reservation_rejected");

        return Ok(new
        {
            success = true,
            message = "تم رفض الحجز بنجاح",
            data = new
            {
                Student = new
                {
                    student.Id,
                    student.Name,
                    student.LocalStudentNumber
                },
                Book = new
                {
                    book.Id,
                    book.Title,
                    book.LocalBookNumber,
                    book.Author,
                    AvailableCopies = book.AvailableCopies,
                    ReservedCopies = book.ReservedCopies,
                    AvailableForLoan = book.AvailableCopies - book.ReservedCopies
                },
                Reservation = new
                {
                    reservation.Id,
                    reservation.Status,
                    reservation.RejectionReason,
                    reservation.Date,
                    reservation.UpdatedAt
                }
            }
        });
    }

    // ✅ تسجيل الإعارة للطالب (تحويل Approved إلى Fulfilled)
    [HttpPost("loans/from-reservation")]
    public async Task<IActionResult> CreateLoanFromReservation(
        [FromQuery] int localStudentNumber,
        [FromQuery] int localBookNumber,
        [FromQuery] int? days = 14)
    {
        var student = await db.Students
            .Include(s => s.Section)
                .ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalStudentNumber == localStudentNumber);

        if (student is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد طالب برقم {localStudentNumber} في المدرسة" 
            });

        var book = await db.Books
            .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                      b.LocalBookNumber == localBookNumber);

        if (book is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" 
            });

        // ✅ البحث عن الحجز الموافق عليه (Approved)
        var reservation = await db.BookReservations
            .FirstOrDefaultAsync(r => r.BookId == book.Id && 
                                      r.StudentId == student.Id && 
                                      r.Status == ReservationStatus.Approved);

        if (reservation is null)
            return BadRequest(new { 
                success = false, 
                message = $"لا يوجد حجز موافق عليه للطالب {localStudentNumber} على الكتاب {localBookNumber}" 
            });

        // التحقق من أن الحجز لم ينتهِ
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (reservation.ExpiryDate < today)
        {
            reservation.Status = ReservationStatus.Cancelled;
            reservation.RejectionReason = "انتهت مهلة الحجز (7 أيام)";
            reservation.UpdatedAt = DateTime.UtcNow;
            book.ReservedCopies--;
            
            await db.SaveChangesAsync();
            
            return BadRequest(new { 
                success = false, 
                message = "انتهت مهلة الحجز (7 أيام). تم إلغاء الحجز تلقائياً." 
            });
        }

        // التحقق من توفر نسخة
        if (book.AvailableCopies <= 0)
            return BadRequest(new { 
                success = false, 
                message = "لا توجد نسخ متاحة من هذا الكتاب" 
            });

        // التحقق من أن الطالب ليس عنده أكثر من 3 كتب مستعارة
        var activeLoansCount = await db.BookLoans
            .CountAsync(l => l.StudentId == student.Id && l.Status == LoanStatus.Active);
        
        if (activeLoansCount >= 3)
            return BadRequest(new { 
                success = false, 
                message = $"الطالب لديه {activeLoansCount} كتب مستعارة، لا يمكنه استعارة المزيد (الحد الأقصى 3)" 
            });

        // حساب LocalLoanNumber
        var maxLocalNumber = await db.BookLoans
            .Where(l => l.Book!.SchoolId == SchoolId)
            .Select(l => (int?)l.LocalLoanNumber)
            .MaxAsync() ?? 0;

        int newLocalNumber = maxLocalNumber + 1;

        // ✅ إنشاء الإعارة
        book.AvailableCopies--;
        book.ReservedCopies--;
        
        var loan = new BookLoan
        {
            BookId = book.Id,
            StudentId = student.Id,
            LocalLoanNumber = newLocalNumber,
            LoanDate = today,
            DueDate = today.AddDays(days ?? 14),
            Status = LoanStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        db.BookLoans.Add(loan);

        // ✅ تحديث حالة الحجز إلى Fulfilled (تم الاستعارة)
        reservation.Status = ReservationStatus.Fulfilled;
        reservation.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        await notifier.SendAsync(student.Id, UserType.Student,
            "تم تسجيل استعارة كتاب",
            $"تم تسجيل استعارتك لكتاب \"{book.Title}\". مدة الاستعارة {days ?? 14} يوماً تنتهي في {loan.DueDate:yyyy-MM-dd}",
            "loan_created");

        return Created($"api/librarian/loans/{loan.LocalLoanNumber}", new
        {
            success = true,
            message = "تم تسجيل الإعارة بنجاح",
            data = new
            {
                Student = new
                {
                    student.Id,
                    student.Name,
                    student.LocalStudentNumber,
                    LocalSectionNumber = student.Section?.LocalSectionNumber ?? 0,
                    GradeName = student.Section?.Grade?.Name,
                    LocalGradeNumber = student.Section?.Grade?.LocalGradeNumber ?? 0
                },
                Book = new
                {
                    book.Id,
                    book.Title,
                    book.LocalBookNumber,
                    book.Author,
                    AvailableCopies = book.AvailableCopies,
                    ReservedCopies = book.ReservedCopies,
                    AvailableForLoan = book.AvailableCopies - book.ReservedCopies
                },
                Reservation = new
                {
                    reservation.Id,
                    reservation.Status,
                    reservation.Date,
                    reservation.ExpiryDate,
                    reservation.UpdatedAt
                },
                Loan = new
                {
                    loan.Id,
                    loan.LocalLoanNumber,
                    LoanDate = loan.LoanDate.ToString("yyyy-MM-dd"),
                    DueDate = loan.DueDate.ToString("yyyy-MM-dd"),
                    loan.Status,
                    loan.CreatedAt
                }
            }
        });
    }

    // [HttpDelete("reservations/{localBookNumber:int}/{localStudentNumber:int}")]
    // public async Task<IActionResult> DeleteReservation(int localBookNumber, int localStudentNumber)
    // {
    //     var book = await db.Books
    //         .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
    //                                   b.LocalBookNumber == localBookNumber);
        
    //     if (book is null)
    //         return NotFound(new { success = false, message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" });

    //     var student = await db.Students
    //         .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
    //                                   s.LocalStudentNumber == localStudentNumber);
        
    //     if (student is null)
    //         return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber} في المدرسة" });

    //     var reservation = await db.BookReservations
    //         .FirstOrDefaultAsync(r => r.BookId == book.Id && 
    //                                   r.StudentId == student.Id &&
    //                                   (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Approved));
        
    //     if (reservation is null)
    //         return NotFound(new { success = false, message = "لا يوجد حجز معلق أو موافق عليه لهذا الطالب على هذا الكتاب" });

    //     book.ReservedCopies--;
    //     db.BookReservations.Remove(reservation);
    //     await db.SaveChangesAsync();

    //     return Ok(new
    //     {
    //         success = true,
    //         message = "تم حذف الحجز بنجاح",
    //         data = new
    //         {
    //             LocalBookNumber = book.LocalBookNumber,
    //             BookTitle = book.Title,
    //             LocalStudentNumber = student.LocalStudentNumber,
    //             StudentName = student.Name,
    //             AvailableCopies = book.AvailableCopies,
    //             ReservedCopies = book.ReservedCopies,
    //             AvailableForLoan = book.AvailableCopies - book.ReservedCopies
    //         }
    //     });
    // }

    // [HttpPost("reservations/cleanup")]
    // public async Task<IActionResult> CleanupExpiredReservations()
    // {
    //     var today = DateOnly.FromDateTime(DateTime.Today);
        
    //     var expiredReservations = await db.BookReservations
    //         .Include(r => r.Book)
    //         .Where(r => (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Approved) &&
    //                    r.ExpiryDate < today)
    //         .ToListAsync();

    //     var count = 0;

    //     foreach (var reservation in expiredReservations)
    //     {
    //         reservation.Status = ReservationStatus.Cancelled;
    //         reservation.RejectionReason = "انتهت مهلة الحجز (7 أيام)";
    //         reservation.UpdatedAt = DateTime.UtcNow;
            
    //         if (reservation.Book is not null)
    //         {
    //             reservation.Book.ReservedCopies--;
    //         }
            
    //         count++;
    //     }

    //     await db.SaveChangesAsync();

    //     return Ok(new
    //     {
    //         success = true,
    //         message = "تم إلغاء الحجوزات المنتهية",
    //         data = new
    //         {
    //             expiredCount = count
    //         }
    //     });
    // }

    // ============================================
    // إدارة الإعارات (Loans)
    // ============================================

    [HttpPost("loans")]
    public async Task<IActionResult> CreateLoanDirect(
        [FromQuery] int localStudentNumber,
        [FromQuery] int localBookNumber,
        [FromQuery] int? days = 14)
    {
        var student = await db.Students
            .Include(s => s.Section)
                .ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalStudentNumber == localStudentNumber);

        if (student is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد طالب برقم {localStudentNumber} في المدرسة" 
            });

        var book = await db.Books
            .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                      b.LocalBookNumber == localBookNumber);

        if (book is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" 
            });

        var availableForLoan = book.AvailableCopies - book.ReservedCopies;
        if (availableForLoan <= 0)
            return BadRequest(new { 
                success = false, 
                message = "لا توجد نسخ متاحة من هذا الكتاب" 
            });

        var activeLoansCount = await db.BookLoans
            .CountAsync(l => l.StudentId == student.Id && l.Status == LoanStatus.Active);
        
        if (activeLoansCount >= 3)
            return BadRequest(new { 
                success = false, 
                message = $"الطالب لديه {activeLoansCount} كتب مستعارة، لا يمكنه استعارة المزيد (الحد الأقصى 3)" 
            });

        var hasActiveLoan = await db.BookLoans
            .AnyAsync(l => l.BookId == book.Id && 
                          l.StudentId == student.Id && 
                          l.Status == LoanStatus.Active);

        if (hasActiveLoan)
            return BadRequest(new { 
                success = false, 
                message = "الطالب لديه هذا الكتاب مستعاراً بالفعل" 
            });

        var maxLocalNumber = await db.BookLoans
            .Where(l => l.Book!.SchoolId == SchoolId)
            .Select(l => (int?)l.LocalLoanNumber)
            .MaxAsync() ?? 0;

        int newLocalNumber = maxLocalNumber + 1;

        book.AvailableCopies--;
        
        var loan = new BookLoan
        {
            BookId = book.Id,
            StudentId = student.Id,
            LocalLoanNumber = newLocalNumber,
            LoanDate = DateOnly.FromDateTime(DateTime.Today),
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(days ?? 14)),
            Status = LoanStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        db.BookLoans.Add(loan);
        await db.SaveChangesAsync();

        await notifier.SendAsync(student.Id, UserType.Student,
            "تم تسجيل استعارة كتاب",
            $"تم تسجيل استعارتك لكتاب \"{book.Title}\". مدة الاستعارة {days ?? 14} يوماً تنتهي في {loan.DueDate:yyyy-MM-dd}",
            "loan_created");

        return Created($"api/librarian/loans/{loan.LocalLoanNumber}", new
        {
            success = true,
            message = "تم تسجيل الإعارة بنجاح",
            data = new
            {
                Student = new
                {
                    student.Id,
                    student.Name,
                    student.LocalStudentNumber,
                    LocalSectionNumber = student.Section?.LocalSectionNumber ?? 0,
                    GradeName = student.Section?.Grade?.Name,
                    LocalGradeNumber = student.Section?.Grade?.LocalGradeNumber ?? 0
                },
                Book = new
                {
                    book.Id,
                    book.Title,
                    book.LocalBookNumber,
                    book.Author,
                    AvailableCopies = book.AvailableCopies,
                    ReservedCopies = book.ReservedCopies,
                    AvailableForLoan = book.AvailableCopies - book.ReservedCopies
                },
                Loan = new
                {
                    loan.Id,
                    loan.LocalLoanNumber,
                    LoanDate = loan.LoanDate.ToString("yyyy-MM-dd"),
                    DueDate = loan.DueDate.ToString("yyyy-MM-dd"),
                    loan.Status,
                    loan.CreatedAt
                }
            }
        });
    }

    [HttpPost("loans/custom")]
    public async Task<IActionResult> CreateLoanWithCustomReturn(
        [FromQuery] int localStudentNumber,
        [FromQuery] int localBookNumber,
        [FromQuery] DateOnly dueDate)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (dueDate <= today)
            return BadRequest(new { 
                success = false, 
                message = "تاريخ الإرجاع يجب أن يكون بعد تاريخ اليوم" 
            });

        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalStudentNumber == localStudentNumber);

        if (student is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد طالب برقم {localStudentNumber} في المدرسة" 
            });

        var book = await db.Books
            .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                      b.LocalBookNumber == localBookNumber);

        if (book is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" 
            });

        var availableForLoan = book.AvailableCopies - book.ReservedCopies;
        if (availableForLoan <= 0)
            return BadRequest(new { 
                success = false, 
                message = "لا توجد نسخ متاحة من هذا الكتاب" 
            });

        var activeLoansCount = await db.BookLoans
            .CountAsync(l => l.StudentId == student.Id && l.Status == LoanStatus.Active);
        
        if (activeLoansCount >= 3)
            return BadRequest(new { 
                success = false, 
                message = $"الطالب لديه {activeLoansCount} كتب مستعارة، لا يمكنه استعارة المزيد (الحد الأقصى 3)" 
            });

        var hasActiveLoan = await db.BookLoans
            .AnyAsync(l => l.BookId == book.Id && 
                          l.StudentId == student.Id && 
                          l.Status == LoanStatus.Active);

        if (hasActiveLoan)
            return BadRequest(new { 
                success = false, 
                message = "الطالب لديه هذا الكتاب مستعاراً بالفعل" 
            });

        var maxLocalNumber = await db.BookLoans
            .Where(l => l.Book!.SchoolId == SchoolId)
            .Select(l => (int?)l.LocalLoanNumber)
            .MaxAsync() ?? 0;

        int newLocalNumber = maxLocalNumber + 1;

        book.AvailableCopies--;
        
        var loan = new BookLoan
        {
            BookId = book.Id,
            StudentId = student.Id,
            LocalLoanNumber = newLocalNumber,
            LoanDate = today,
            DueDate = dueDate,
            Status = LoanStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        db.BookLoans.Add(loan);
        await db.SaveChangesAsync();

        var daysDiff = (dueDate.DayNumber - today.DayNumber);
        await notifier.SendAsync(student.Id, UserType.Student,
            "تم تسجيل استعارة كتاب",
            $"تم تسجيل استعارتك لكتاب \"{book.Title}\". مدة الاستعارة {daysDiff} يوماً تنتهي في {dueDate:yyyy-MM-dd}",
            "loan_created");

        return Created($"api/librarian/loans/{loan.LocalLoanNumber}", new
        {
            success = true,
            message = "تم تسجيل الإعارة بنجاح",
            data = new
            {
                Student = new
                {
                    student.Id,
                    student.Name,
                    student.LocalStudentNumber
                },
                Book = new
                {
                    book.Id,
                    book.Title,
                    book.LocalBookNumber,
                    AvailableCopies = book.AvailableCopies,
                    ReservedCopies = book.ReservedCopies,
                    AvailableForLoan = book.AvailableCopies - book.ReservedCopies
                },
                Loan = new
                {
                    loan.Id,
                    loan.LocalLoanNumber,
                    LoanDate = loan.LoanDate.ToString("yyyy-MM-dd"),
                    DueDate = loan.DueDate.ToString("yyyy-MM-dd"),
                    DurationDays = daysDiff,
                    loan.Status,
                    loan.CreatedAt
                }
            }
        });
    }

    [HttpPost("loans/return")]
    public async Task<IActionResult> ReturnLoan(
        [FromQuery] int localStudentNumber,
        [FromQuery] int localBookNumber)
    {
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalStudentNumber == localStudentNumber);

        if (student is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد طالب برقم {localStudentNumber} في هذه المدرسة" 
            });

        var book = await db.Books
            .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                      b.LocalBookNumber == localBookNumber);

        if (book is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" 
            });

        var loan = await db.BookLoans
            .Include(l => l.Book)
            .Include(l => l.Student)
            .FirstOrDefaultAsync(l => l.BookId == book.Id && 
                                      l.StudentId == student.Id && 
                                      l.Status == LoanStatus.Active);

        if (loan is null)
            return NotFound(new { 
                success = false, 
                message = $"لا توجد إعارة نشطة للطالب {localStudentNumber} على الكتاب {localBookNumber}" 
            });

        loan.Status = LoanStatus.Returned;
        loan.ReturnDate = DateOnly.FromDateTime(DateTime.Today);
        book.AvailableCopies++;

        // التحقق من وجود حجوزات معلقة لهذا الكتاب
        var pendingReservations = await db.BookReservations
            .Where(r => r.BookId == book.Id && 
                       (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Approved))
            .OrderBy(r => r.Date)
            .ToListAsync();

        if (pendingReservations.Any())
        {
            var firstReservation = pendingReservations.First();
            
            await notifier.SendAsync(firstReservation.StudentId, UserType.Student,
                "الكتاب الذي حجزته أصبح متوفراً",
                $"كتاب \"{book.Title}\" أصبح متوفراً. يرجى التوجه إلى المكتبة لاستعارته خلال 7 أيام.",
                "book_available");
        }

        await db.SaveChangesAsync();

        await notifier.SendAsync(student.Id, UserType.Student,
            "تم إعادة الكتاب",
            $"لقد قمت بإعادة كتاب \"{book.Title}\" بنجاح",
            "library_return");

        if (!string.IsNullOrEmpty(student.GuardianPhone))
        {
            await notifier.SendToGuardianAsync(student,
                "تم إعادة كتاب من قبل ابنكم",
                $"قام ابنكم {student.Name} بإعادة كتاب \"{book.Title}\" إلى المكتبة",
                "library_return");
        }

        return Ok(new
        {
            success = true,
            message = "تم إعادة الكتاب بنجاح",
            data = new
            {
                Student = new
                {
                    student.Id,
                    student.Name,
                    student.LocalStudentNumber
                },
                Book = new
                {
                    book.Id,
                    book.Title,
                    book.LocalBookNumber,
                    book.Author,
                    AvailableCopies = book.AvailableCopies,
                    ReservedCopies = book.ReservedCopies,
                    AvailableForLoan = book.AvailableCopies - book.ReservedCopies
                },
                Loan = new
                {
                    loan.Id,
                    loan.LocalLoanNumber,
                    LoanDate = loan.LoanDate.ToString("yyyy-MM-dd"),
                    DueDate = loan.DueDate.ToString("yyyy-MM-dd"),
                    ReturnDate = loan.ReturnDate?.ToString("yyyy-MM-dd"),
                    loan.Status
                },
                WaitingReservations = pendingReservations.Count
            }
        });
    }

    [HttpGet("loans")]
    public async Task<IActionResult> GetLoans(
        [FromQuery] LoanStatus? status = null,
        [FromQuery] int? localStudentNumber = null,
        [FromQuery] int? localBookNumber = null,
        [FromQuery] bool? overdue = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var query = db.BookLoans
            .Include(l => l.Book)
            .Include(l => l.Student)
                .ThenInclude(s => s!.Section)
                    .ThenInclude(sec => sec!.Grade)
            .Where(l => l.Book!.SchoolId == SchoolId);

        if (status.HasValue)
            query = query.Where(l => l.Status == status);

        if (overdue == true)
            query = query.Where(l => l.Status == LoanStatus.Active && l.DueDate < today);
        else if (overdue == false)
            query = query.Where(l => l.Status == LoanStatus.Returned);

        if (localStudentNumber.HasValue)
        {
            var student = await db.Students
                .Where(s => s.SchoolId == SchoolId && 
                            s.LocalStudentNumber == localStudentNumber.Value)
                .Select(s => s.Id)
                .FirstOrDefaultAsync();
            
            if (student > 0)
                query = query.Where(l => l.StudentId == student);
            else
                return Ok(new { 
                    success = true, 
                    message = "لا توجد إعارات لهذا الطالب", 
                    data = new { totalCount = 0, activeCount = 0, returnedCount = 0, overdueCount = 0, loans = new List<object>() } 
                });
        }

        if (localBookNumber.HasValue)
        {
            var book = await db.Books
                .Where(b => b.SchoolId == SchoolId && 
                            b.LocalBookNumber == localBookNumber.Value)
                .Select(b => b.Id)
                .FirstOrDefaultAsync();
            
            if (book > 0)
                query = query.Where(l => l.BookId == book);
            else
                return Ok(new { 
                    success = true, 
                    message = "لا توجد إعارات لهذا الكتاب", 
                    data = new { totalCount = 0, activeCount = 0, returnedCount = 0, overdueCount = 0, loans = new List<object>() } 
                });
        }

        var loans = await query
            .OrderByDescending(l => l.LoanDate)
            .Select(l => new
            {
                l.Id,
                l.LocalLoanNumber,
                LocalBookNumber = l.Book != null ? l.Book.LocalBookNumber : 0,
                BookTitle = l.Book != null ? l.Book.Title : null,
                BookAuthor = l.Book != null ? l.Book.Author : null,
                LocalStudentNumber = l.Student != null ? l.Student.LocalStudentNumber : 0,
                StudentName = l.Student != null ? l.Student.Name : null,
                SectionName = l.Student != null && l.Student.Section != null ? 
                    l.Student.Section.Name : null,
                LocalSectionNumber = l.Student != null && l.Student.Section != null ? 
                    l.Student.Section.LocalSectionNumber : 0,
                GradeName = l.Student != null && l.Student.Section != null && l.Student.Section.Grade != null ? 
                    l.Student.Section.Grade.Name : null,
                LocalGradeNumber = l.Student != null && l.Student.Section != null && l.Student.Section.Grade != null ? 
                    l.Student.Section.Grade.LocalGradeNumber : 0,
                l.LoanDate,
                l.DueDate,
                l.ReturnDate,
                l.Status,
                StatusName = l.Status.ToString(),
                StatusArabic = l.Status == LoanStatus.Active ? "نشط" :
                              l.Status == LoanStatus.Returned ? "مُعاد" :
                              l.Status == LoanStatus.Overdue ? "متأخر" : "غير معروف",
                IsOverdue = l.Status == LoanStatus.Active && l.DueDate < today,
                DaysOverdue = l.Status == LoanStatus.Active && l.DueDate < today ? 
                    (today.DayNumber - l.DueDate.DayNumber) : 0,
                l.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب الإعارات بنجاح",
            data = new
            {
                totalCount = loans.Count,
                activeCount = loans.Count(l => l.Status == LoanStatus.Active),
                returnedCount = loans.Count(l => l.Status == LoanStatus.Returned),
                overdueCount = loans.Count(l => l.Status == LoanStatus.Overdue),
                loans = loans
            }
        });
    }

    [HttpGet("loans/student/{localStudentNumber:int}")]
    public async Task<IActionResult> GetStudentLoans(int localStudentNumber)
    {
        var student = await db.Students
            .Include(s => s.Section)
                .ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalStudentNumber == localStudentNumber);
        
        if (student is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد طالب برقم {localStudentNumber} في المدرسة" 
            });

        var loans = await db.BookLoans
            .Include(l => l.Book)
            .Where(l => l.StudentId == student.Id)
            .OrderByDescending(l => l.LoanDate)
            .Select(l => new
            {
                l.Id,
                l.LocalLoanNumber,
                LocalBookNumber = l.Book != null ? l.Book.LocalBookNumber : 0,
                BookTitle = l.Book != null ? l.Book.Title : null,
                BookAuthor = l.Book != null ? l.Book.Author : null,
                l.LoanDate,
                l.DueDate,
                l.ReturnDate,
                l.Status,
                StatusName = l.Status.ToString(),
                StatusArabic = l.Status == LoanStatus.Active ? "نشط" :
                              l.Status == LoanStatus.Returned ? "مُعاد" :
                              l.Status == LoanStatus.Overdue ? "متأخر" : "غير معروف",
                IsOverdue = l.Status == LoanStatus.Active && l.DueDate < DateOnly.FromDateTime(DateTime.Today),
                DaysOverdue = l.Status == LoanStatus.Active && l.DueDate < DateOnly.FromDateTime(DateTime.Today) ? 
                    (DateOnly.FromDateTime(DateTime.Today).DayNumber - l.DueDate.DayNumber) : 0
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = $"تم جلب إعارات الطالب {student.Name} بنجاح",
            data = new
            {
                Student = new
                {
                    student.Id,
                    student.Name,
                    student.LocalStudentNumber,
                    SectionName = student.Section?.Name,
                    LocalSectionNumber = student.Section?.LocalSectionNumber ?? 0,
                    GradeName = student.Section?.Grade?.Name,
                    LocalGradeNumber = student.Section?.Grade?.LocalGradeNumber ?? 0
                },
                totalLoans = loans.Count,
                activeLoans = loans.Count(l => l.Status == LoanStatus.Active),
                returnedLoans = loans.Count(l => l.Status == LoanStatus.Returned),
                overdueLoans = loans.Count(l => l.Status == LoanStatus.Overdue),
                loans = loans
            }
        });
    }

    [HttpGet("loans/book/{localBookNumber:int}")]
    public async Task<IActionResult> GetBookLoans(
        int localBookNumber,
        [FromQuery] LoanStatus? status = null)
    {
        var book = await db.Books
            .Where(b => b.SchoolId == SchoolId && 
                        b.LocalBookNumber == localBookNumber)
            .Select(b => new { b.Id, b.Title, b.LocalBookNumber, b.Author, b.AvailableCopies, b.ReservedCopies, b.Copies })
            .FirstOrDefaultAsync();

        if (book is null)
            return NotFound(new { 
                success = false, 
                message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" 
            });

        var query = db.BookLoans
            .Include(l => l.Student)
                .ThenInclude(s => s!.Section)
                    .ThenInclude(sec => sec!.Grade)
            .Where(l => l.BookId == book.Id);

        if (status.HasValue)
            query = query.Where(l => l.Status == status);

        var loans = await query
            .OrderByDescending(l => l.LoanDate)
            .Select(l => new
            {
                l.Id,
                l.LocalLoanNumber,
                LocalStudentNumber = l.Student != null ? l.Student.LocalStudentNumber : 0,
                StudentName = l.Student != null ? l.Student.Name : null,
                SectionName = l.Student != null && l.Student.Section != null ? 
                    l.Student.Section.Name : null,
                LocalSectionNumber = l.Student != null && l.Student.Section != null ? 
                    l.Student.Section.LocalSectionNumber : 0,
                GradeName = l.Student != null && l.Student.Section != null && l.Student.Section.Grade != null ? 
                    l.Student.Section.Grade.Name : null,
                LocalGradeNumber = l.Student != null && l.Student.Section != null && l.Student.Section.Grade != null ? 
                    l.Student.Section.Grade.LocalGradeNumber : 0,
                l.LoanDate,
                l.DueDate,
                l.ReturnDate,
                l.Status,
                StatusName = l.Status.ToString(),
                StatusArabic = l.Status == LoanStatus.Active ? "نشط" :
                              l.Status == LoanStatus.Returned ? "مُعاد" :
                              l.Status == LoanStatus.Overdue ? "متأخر" : "غير معروف",
                IsOverdue = l.Status == LoanStatus.Active && l.DueDate < DateOnly.FromDateTime(DateTime.Today),
                DaysOverdue = l.Status == LoanStatus.Active && l.DueDate < DateOnly.FromDateTime(DateTime.Today) ? 
                    (DateOnly.FromDateTime(DateTime.Today).DayNumber - l.DueDate.DayNumber) : 0,
                l.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = $"تم جلب إعارات الكتاب {book.Title} بنجاح",
            data = new
            {
                Book = new
                {
                    book.Id,
                    book.Title,
                    book.LocalBookNumber,
                    book.Author,
                    TotalCopies = book.Copies,
                    AvailableCopies = book.AvailableCopies,
                    ReservedCopies = book.ReservedCopies,
                    AvailableForLoan = book.AvailableCopies - book.ReservedCopies,
                    IsAvailable = (book.AvailableCopies - book.ReservedCopies) > 0
                },
                Statistics = new
                {
                    totalLoans = loans.Count,
                    activeLoans = loans.Count(l => l.Status == LoanStatus.Active),
                    returnedLoans = loans.Count(l => l.Status == LoanStatus.Returned),
                    overdueLoans = loans.Count(l => l.Status == LoanStatus.Overdue)
                },
                Loans = loans
            }
        });
    }

    // ============================================
    // إشعارات الكتب المتأخرة
    // ============================================

    [HttpPost("loans/notify-overdue")]
    public async Task<IActionResult> NotifyOverdueLoans()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var overdueLoans = await db.BookLoans
            .Include(l => l.Book)
            .Include(l => l.Student)
            .Where(l => l.Book!.SchoolId == SchoolId &&
                        l.Status == LoanStatus.Active &&
                        l.DueDate < today)
            .ToListAsync();

        var notified = 0;

        foreach (var loan in overdueLoans)
        {
            loan.Status = LoanStatus.Overdue;
            
            if (loan.Student is not null)
            {
                var daysOverdue = today.DayNumber - loan.DueDate.DayNumber;
                
                await notifier.SendAsync(loan.Student.Id, UserType.Student,
                    "تنبيه: كتاب متأخر عن الإعادة",
                    $"كتاب \"{loan.Book!.Title}\" متأخر عن الإعادة منذ {daysOverdue} يوماً. يرجى إعادته في أقرب وقت.",
                    "library_overdue");
                
                if (!string.IsNullOrEmpty(loan.Student.GuardianPhone))
                {
                    await notifier.SendToGuardianAsync(loan.Student,
                        "تنبيه: كتاب متأخر عن الإعادة لابنكم",
                        $"كتاب \"{loan.Book!.Title}\" متأخر عن الإعادة منذ {daysOverdue} يوماً.",
                        "library_overdue");
                }
                
                notified++;
            }
        }

        await db.SaveChangesAsync();
        
        return Ok(new 
        { 
            success = true,
            message = "تم إرسال الإشعارات للكتب المتأخرة",
            data = new
            {
                notified = notified,
                totalOverdue = overdueLoans.Count
            }
        });
    }

    // ============================================
    // إحصائيات المكتبة
    // ============================================

    // [HttpGet("statistics")]
    // public async Task<IActionResult> GetStatistics()
    // {
    //     var totalBooks = await db.Books.CountAsync(b => b.SchoolId == SchoolId);
    //     var totalCopies = await db.Books.Where(b => b.SchoolId == SchoolId).SumAsync(b => b.Copies);
    //     var availableCopies = await db.Books.Where(b => b.SchoolId == SchoolId).SumAsync(b => b.AvailableCopies);
    //     var reservedCopies = await db.Books.Where(b => b.SchoolId == SchoolId).SumAsync(b => b.ReservedCopies);
        
    //     var activeLoans = await db.BookLoans
    //         .CountAsync(l => l.Book!.SchoolId == SchoolId && l.Status == LoanStatus.Active);
        
    //     var overdueLoans = await db.BookLoans
    //         .CountAsync(l => l.Book!.SchoolId == SchoolId && l.Status == LoanStatus.Overdue);
        
    //     var pendingReservations = await db.BookReservations
    //         .CountAsync(r => r.Book!.SchoolId == SchoolId && r.Status == ReservationStatus.Pending);
        
    //     var approvedReservations = await db.BookReservations
    //         .CountAsync(r => r.Book!.SchoolId == SchoolId && r.Status == ReservationStatus.Approved);
        
    //     var totalStudents = await db.Students.CountAsync(s => s.SchoolId == SchoolId);
        
    //     var studentsWithLoans = await db.BookLoans
    //         .Where(l => l.Book!.SchoolId == SchoolId && l.Status == LoanStatus.Active)
    //         .Select(l => l.StudentId)
    //         .Distinct()
    //         .CountAsync();

    //     return Ok(new
    //     {
    //         success = true,
    //         message = "تم جلب إحصائيات المكتبة بنجاح",
    //         data = new
    //         {
    //             Books = new
    //             {
    //                 TotalTitles = totalBooks,
    //                 TotalCopies = totalCopies,
    //                 AvailableCopies = availableCopies,
    //                 ReservedCopies = reservedCopies,
    //                 BorrowedCopies = activeLoans,
    //                 AvailableForLoan = availableCopies - reservedCopies
    //             },
    //             Loans = new
    //             {
    //                 Active = activeLoans,
    //                 Overdue = overdueLoans,
    //                 TotalStudentsWithLoans = studentsWithLoans
    //             },
    //             Reservations = new
    //             {
    //                 Pending = pendingReservations,
    //                 Approved = approvedReservations,
    //                 Total = pendingReservations + approvedReservations
    //             },
    //             Students = new
    //             {
    //                 Total = totalStudents,
    //                 WithActiveLoans = studentsWithLoans
    //             }
    //         }
    //     });
    // }
}