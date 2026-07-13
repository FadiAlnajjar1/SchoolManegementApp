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
    // إدارة الكتب (Books) - مع LocalBookNumber
    // ============================================

    [HttpPost("books")]
    public async Task<IActionResult> CreateBook(BookRequest request)
    {
        // 1. التحقق من عدم وجود كتاب بنفس الـ ISBN
        if (!string.IsNullOrEmpty(request.Isbn))
        {
            var existingBook = await db.Books
                .AnyAsync(b => b.Isbn == request.Isbn && b.SchoolId == SchoolId);
            
            if (existingBook)
                return BadRequest(new { message = "يوجد كتاب بنفس الـ ISBN بالفعل" });
        }

        // 2. حساب الرقم المحلي للكتاب
        var usedNumbers = await db.Books
            .Where(b => b.SchoolId == SchoolId)
            .Select(b => b.LocalBookNumber)
            .ToListAsync();
        
        int newLocalNumber = 1;
        while (usedNumbers.Contains(newLocalNumber)) newLocalNumber++;

        var book = new Book
        {
            SchoolId = SchoolId,
            LocalBookNumber = newLocalNumber,
            Title = request.Title,
            Author = request.Author ?? "",
            Isbn = request.Isbn ?? "",
            Copies = request.Copies,
            AvailableCopies = request.Copies,
        };
        
        db.Books.Add(book);
        await db.SaveChangesAsync();
        
        return Created($"api/librarian/books/{book.LocalBookNumber}", new
        {
            message = "تم إضافة الكتاب بنجاح",
            book = new
            {
                book.Id,
                book.LocalBookNumber,
                book.Title,
                book.Author,
                book.Isbn,
                book.Copies,
                book.AvailableCopies,
                book.SchoolId,
                book.CreatedAt
            }
        });
    }

    [HttpGet("books")]
    public async Task<IActionResult> GetBooks(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? available = null)
    {
        var query = db.Books
            .Where(b => b.SchoolId == SchoolId);

        // البحث حسب العنوان أو المؤلف
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(b => 
                b.Title.Contains(search) || 
                b.Author.Contains(search) ||
                b.Isbn.Contains(search));
        }

        // تصفية حسب التوفر
        if (available == true)
            query = query.Where(b => b.AvailableCopies > 0);
        else if (available == false)
            query = query.Where(b => b.AvailableCopies == 0);

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var books = await query
            .OrderBy(b => b.LocalBookNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new
            {
                b.Id,
                b.LocalBookNumber,
                b.Title,
                b.Author,
                b.Isbn,
                b.Copies,
                b.AvailableCopies,
                b.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            totalCount,
            totalPages,
            page,
            pageSize,
            books
        });
    }

    [HttpGet("books/{localBookNumber:int}")]
    public async Task<IActionResult> GetBook(int localBookNumber)
    {
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                      b.LocalBookNumber == localBookNumber);
        
        if (book is null)
            return NotFound(new { message = "الكتاب غير موجود" });

        return Ok(new
        {
            book.Id,
            book.LocalBookNumber,
            book.Title,
            book.Author,
            book.Isbn,
            book.Copies,
            book.AvailableCopies,
            book.CreatedAt
        });
    }

    [HttpPut("books/{localBookNumber:int}")]
    public async Task<IActionResult> UpdateBook(int localBookNumber, BookRequest request)
    {
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                      b.LocalBookNumber == localBookNumber);
        
        if (book is null) 
            return NotFound(new { message = "الكتاب غير موجود" });

        // التحقق من عدم وجود كتاب بنفس الـ ISBN (باستثناء هذا الكتاب)
        if (!string.IsNullOrEmpty(request.Isbn) && request.Isbn != book.Isbn)
        {
            var existingBook = await db.Books
                .AnyAsync(b => b.Isbn == request.Isbn && 
                              b.SchoolId == SchoolId && 
                              b.LocalBookNumber != localBookNumber);
            
            if (existingBook)
                return BadRequest(new { message = "يوجد كتاب بنفس الـ ISBN بالفعل" });
        }

        var loaned = book.Copies - book.AvailableCopies;
        if (request.Copies < loaned)
            return BadRequest(new { message = $"يوجد {loaned} نسخة معارة — لا يمكن إنقاص النسخ دونها" });

        book.Title = request.Title;
        book.Author = request.Author ?? book.Author;
        book.Isbn = request.Isbn ?? book.Isbn;
        book.AvailableCopies = request.Copies - loaned;
        book.Copies = request.Copies;
        
        await db.SaveChangesAsync();
        
        return Ok(new
        {
            message = "تم تحديث الكتاب بنجاح",
            book = new
            {
                book.Id,
                book.LocalBookNumber,
                book.Title,
                book.Author,
                book.Isbn,
                book.Copies,
                book.AvailableCopies,
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
            return NotFound(new { message = "الكتاب غير موجود" });

        // التحقق من عدم وجود إعارات نشطة
        if (await db.BookLoans.AnyAsync(l => l.BookId == book.Id && l.Status == LoanStatus.Active))
            return BadRequest(new { message = "لا يمكن حذف كتاب له إعارات نشطة" });

        // حذف الحجوزات والإعارات المرتبطة
        db.BookReservations.RemoveRange(db.BookReservations.Where(r => r.BookId == book.Id));
        db.BookLoans.RemoveRange(db.BookLoans.Where(l => l.BookId == book.Id));
        db.Books.Remove(book);
        
        await db.SaveChangesAsync();
        
        return Ok(new { message = "تم حذف الكتاب بنجاح" });
    }

    // ============================================
    // إدارة الأعضاء (Members) - مع LocalMemberNumber
    // ============================================

    [HttpPost("members")]
    public async Task<IActionResult> CreateMember(MemberRequest request)
    {
        // التحقق من وجود الطالب
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.SchoolId == SchoolId);
        
        if (student is null)
            return BadRequest(new { message = "الطالب غير موجود في مدرستك" });

        // التحقق من عدم وجود العضو
        if (await db.LibraryMembers.AnyAsync(m => m.StudentId == request.StudentId))
            return BadRequest(new { message = "الطالب عضو في المكتبة بالفعل" });

        // حساب الرقم المحلي
        var usedNumbers = await db.LibraryMembers
            .Where(m => m.SchoolId == SchoolId)
            .Select(m => m.LocalMemberNumber)
            .ToListAsync();
        
        int newLocalNumber = 1;
        while (usedNumbers.Contains(newLocalNumber)) newLocalNumber++;

        var member = new LibraryMember 
        { 
            StudentId = request.StudentId, 
            SchoolId = SchoolId,
            LocalMemberNumber = newLocalNumber,
            Status = MemberStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        db.LibraryMembers.Add(member);
        await db.SaveChangesAsync();

        // إشعار للطالب
        await notifier.SendAsync(student.Id, UserType.Student,
            "مرحباً في المكتبة",
            $"تم تسجيلك كعضو في مكتبة المدرسة برقم {newLocalNumber}",
            "library_member");

        return Created($"api/librarian/members/{member.LocalMemberNumber}", new
        {
            message = "تم إضافة العضو بنجاح",
            member = new
            {
                member.Id,
                member.LocalMemberNumber,
                member.StudentId,
                StudentName = student.Name,
                member.Status,
                member.CreatedAt
            }
        });
    }

    [HttpGet("members")]
    public async Task<IActionResult> GetMembers()
    {
        var members = await db.LibraryMembers
            .Include(m => m.Student)
            .Where(m => m.SchoolId == SchoolId)
            .OrderBy(m => m.LocalMemberNumber)
            .Select(m => new
            {
                m.Id,
                m.LocalMemberNumber,
                m.StudentId,
                StudentName = m.Student != null ? m.Student.Name : null,
                StudentEmail = m.Student != null ? m.Student.Email : null,
                m.Status,
                m.CreatedAt
            })
            .ToListAsync();

        return Ok(members);
    }

    [HttpGet("members/{localMemberNumber:int}")]
    public async Task<IActionResult> GetMember(int localMemberNumber)
    {
        var member = await db.LibraryMembers
            .Include(m => m.Student)
            .FirstOrDefaultAsync(m => m.SchoolId == SchoolId && 
                                      m.LocalMemberNumber == localMemberNumber);
        
        if (member is null)
            return NotFound(new { message = "العضو غير موجود" });

        return Ok(new
        {
            member.Id,
            member.LocalMemberNumber,
            member.StudentId,
            StudentName = member.Student != null ? member.Student.Name : null,
            StudentEmail = member.Student != null ? member.Student.Email : null,
            member.Status,
            member.CreatedAt
        });
    }

    [HttpPatch("members/{localMemberNumber:int}/status")]
    public async Task<IActionResult> SetMemberStatus(int localMemberNumber, [FromQuery] MemberStatus status)
    {
        var member = await db.LibraryMembers
            .FirstOrDefaultAsync(m => m.SchoolId == SchoolId && 
                                      m.LocalMemberNumber == localMemberNumber);
        
        if (member is null)
            return NotFound(new { message = "العضو غير موجود" });

        member.Status = status;
        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "تم تحديث حالة العضو بنجاح",
            member = new
            {
                member.Id,
                member.LocalMemberNumber,
                member.StudentId,
                member.Status,
                member.CreatedAt
            }
        });
    }

    [HttpDelete("members/{localMemberNumber:int}")]
    public async Task<IActionResult> DeleteMember(int localMemberNumber)
    {
        var member = await db.LibraryMembers
            .FirstOrDefaultAsync(m => m.SchoolId == SchoolId && 
                                      m.LocalMemberNumber == localMemberNumber);
        
        if (member is null)
            return NotFound(new { message = "العضو غير موجود" });

        // التحقق من وجود إعارات نشطة
        var activeLoans = await db.BookLoans
            .AnyAsync(l => l.MemberId == member.Id && l.Status == LoanStatus.Active);
        
        if (activeLoans)
            return BadRequest(new { message = "لا يمكن حذف العضو لأنه لديه إعارات نشطة" });

        // حذف الحجوزات والإعارات
        db.BookReservations.RemoveRange(db.BookReservations.Where(r => r.MemberId == member.Id));
        db.BookLoans.RemoveRange(db.BookLoans.Where(l => l.MemberId == member.Id));
        db.LibraryMembers.Remove(member);
        
        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف العضو بنجاح" });
    }

    // ============================================
    // إدارة الإعارات (Loans)
    // ============================================

    [HttpPost("loans")]
    public async Task<IActionResult> CreateLoan(LoanRequest request)
    {
        // 1. التحقق من وجود الكتاب
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.Id == request.BookId && b.SchoolId == SchoolId);
        
        if (book is null)
            return NotFound(new { message = "الكتاب غير موجود" });

        if (book.AvailableCopies <= 0)
            return BadRequest(new { message = "لا توجد نسخ متاحة" });

        // 2. التحقق من وجود العضو
        var member = await db.LibraryMembers
            .Include(m => m.Student)
            .FirstOrDefaultAsync(m => m.Id == request.MemberId && m.SchoolId == SchoolId);
        
        if (member is null)
            return NotFound(new { message = "العضو غير موجود" });

        if (member.Status != MemberStatus.Active)
            return BadRequest(new { message = "عضوية الطالب موقوفة" });

        // 3. إنشاء الإعارة
        book.AvailableCopies--;
        
        var loan = new BookLoan
        {
            BookId = book.Id,
            MemberId = member.Id,
            LoanDate = DateOnly.FromDateTime(DateTime.Today),
            DueDate = request.DueDate,
            Status = LoanStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        db.BookLoans.Add(loan);

        // 4. تحديث الحجز إذا كان موجوداً
        var reservation = await db.BookReservations
            .FirstOrDefaultAsync(r => r.BookId == book.Id && 
                                      r.MemberId == member.Id && 
                                      r.Status == ReservationStatus.Pending);
        
        if (reservation is not null)
            reservation.Status = ReservationStatus.Fulfilled;

        await db.SaveChangesAsync();

        // 5. إشعار للطالب
        await notifier.SendAsync(member.StudentId, UserType.Student,
            "تم استعارة كتاب",
            $"لقد استعرت كتاب \"{book.Title}\" حتى تاريخ {request.DueDate}",
            "library_loan");

        return Created($"api/librarian/loans/{loan.Id}", new
        {
            message = "تمت الإعارة بنجاح",
            loan = new
            {
                loan.Id,
                loan.BookId,
                BookTitle = book.Title,
                loan.MemberId,
                MemberName = member.Student?.Name,
                loan.LoanDate,
                loan.DueDate,
                loan.Status
            }
        });
    }

    [HttpPost("loans/{id:int}/return")]
    public async Task<IActionResult> ReturnLoan(int id)
    {
        var loan = await db.BookLoans
            .Include(l => l.Book)
            .Include(l => l.Member)
                .ThenInclude(m => m!.Student)
            .FirstOrDefaultAsync(l => l.Id == id && l.Book!.SchoolId == SchoolId);
        
        if (loan is null)
            return NotFound(new { message = "الإعارة غير موجودة" });

        if (loan.Status == LoanStatus.Returned)
            return BadRequest(new { message = "الكتاب مُعاد بالفعل" });

        // تحديث حالة الإعارة
        loan.Status = LoanStatus.Returned;
        loan.ReturnDate = DateOnly.FromDateTime(DateTime.Today);
        loan.Book!.AvailableCopies++;

        await db.SaveChangesAsync();

        // إشعار للطالب
        if (loan.Member?.Student is not null)
        {
            await notifier.SendAsync(loan.Member.Student.Id, UserType.Student,
                "تم إعادة الكتاب",
                $"لقد قمت بإعادة كتاب \"{loan.Book.Title}\" بنجاح",
                "library_return");
        }

        return Ok(new
        {
            message = "تم إعادة الكتاب بنجاح",
            loan = new
            {
                loan.Id,
                loan.BookId,
                BookTitle = loan.Book.Title,
                loan.MemberId,
                StudentName = loan.Member?.Student?.Name,
                loan.LoanDate,
                loan.DueDate,
                loan.ReturnDate,
                loan.Status
            }
        });
    }

    [HttpGet("loans")]
    public async Task<IActionResult> GetLoans(
        [FromQuery] bool? overdue,
        [FromQuery] int? memberId,
        [FromQuery] int? bookId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var query = db.BookLoans
            .Include(l => l.Book)
            .Include(l => l.Member)
                .ThenInclude(m => m!.Student)
            .Where(l => l.Book!.SchoolId == SchoolId);

        // تصفية حسب الحالة
        if (overdue == true)
            query = query.Where(l => l.Status != LoanStatus.Returned && l.DueDate < today);
        else if (overdue == false)
            query = query.Where(l => l.Status == LoanStatus.Returned);

        // تصفية حسب العضو
        if (memberId.HasValue)
            query = query.Where(l => l.MemberId == memberId);

        // تصفية حسب الكتاب
        if (bookId.HasValue)
            query = query.Where(l => l.BookId == bookId);

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var loans = await query
            .OrderByDescending(l => l.LoanDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.Id,
                BookId = l.Book != null ? l.Book.Id : 0,
                BookTitle = l.Book != null ? l.Book.Title : null,
                BookLocalNumber = l.Book != null ? l.Book.LocalBookNumber : 0,
                MemberId = l.Member != null ? l.Member.Id : 0,
                MemberLocalNumber = l.Member != null ? l.Member.LocalMemberNumber : 0,
                StudentName = l.Member != null && l.Member.Student != null ? 
                    l.Member.Student.Name : null,
                l.LoanDate,
                l.DueDate,
                l.ReturnDate,
                l.Status,
                l.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            totalCount,
            totalPages,
            page,
            pageSize,
            loans
        });
    }

    [HttpGet("loans/member/{localMemberNumber:int}")]
    public async Task<IActionResult> GetMemberLoans(int localMemberNumber)
    {
        var member = await db.LibraryMembers
            .FirstOrDefaultAsync(m => m.SchoolId == SchoolId && 
                                      m.LocalMemberNumber == localMemberNumber);
        
        if (member is null)
            return NotFound(new { message = "العضو غير موجود" });

        var loans = await db.BookLoans
            .Include(l => l.Book)
            .Where(l => l.MemberId == member.Id)
            .OrderByDescending(l => l.LoanDate)
            .Select(l => new
            {
                l.Id,
                BookTitle = l.Book != null ? l.Book.Title : null,
                BookLocalNumber = l.Book != null ? l.Book.LocalBookNumber : 0,
                l.LoanDate,
                l.DueDate,
                l.ReturnDate,
                l.Status
            })
            .ToListAsync();

        return Ok(new
        {
            memberId = member.Id,
            localMemberNumber = member.LocalMemberNumber,
            loans = loans
        });
    }

    [HttpPost("loans/notify-due")]
    public async Task<IActionResult> NotifyDue()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var dueLoans = await db.BookLoans
            .Include(l => l.Book)
            .Include(l => l.Member)
                .ThenInclude(m => m!.Student)
            .Where(l => l.Book!.SchoolId == SchoolId &&
                        l.Status != LoanStatus.Returned &&
                        l.DueDate <= today.AddDays(1))
            .ToListAsync();

        var notified = 0;

        foreach (var loan in dueLoans)
        {
            if (loan.DueDate < today) loan.Status = LoanStatus.Overdue;
            
            var student = loan.Member?.Student;
            if (student is not null)
            {
                // إشعار للطالب
                await notifier.SendAsync(student.Id, UserType.Student,
                    "استحقاق إعادة كتاب",
                    $"كتاب \"{loan.Book!.Title}\" مستحق الإعادة بتاريخ {loan.DueDate}",
                    "library_due");
                
                // إشعار لولي الأمر
                await notifier.SendToGuardianAsync(student,
                    "استحقاق إعادة كتاب لابنكم",
                    $"كتاب \"{loan.Book!.Title}\" مستحق الإعادة بتاريخ {loan.DueDate}",
                    "library_due");
                
                notified++;
            }
        }

        await db.SaveChangesAsync();
        
        return Ok(new 
        { 
            message = "تم إرسال الإشعارات بنجاح",
            notified = notified,
            totalDue = dueLoans.Count
        });
    }

    // ============================================
    // إدارة الحجوزات (Reservations)
    // ============================================

    [HttpPost("reservations")]
    public async Task<IActionResult> CreateReservation(ReservationRequest request)
    {
        // 1. التحقق من وجود الكتاب
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.Id == request.BookId && b.SchoolId == SchoolId);
        
        if (book is null)
            return NotFound(new { message = "الكتاب غير موجود" });

        // 2. التحقق من وجود العضو
        var member = await db.LibraryMembers
            .Include(m => m.Student)
            .FirstOrDefaultAsync(m => m.Id == request.MemberId && m.SchoolId == SchoolId);
        
        if (member is null)
            return NotFound(new { message = "العضو غير موجود" });

        if (member.Status != MemberStatus.Active)
            return BadRequest(new { message = "عضوية الطالب موقوفة" });

        // 3. التحقق من عدم وجود حجز نشط
        var existingReservation = await db.BookReservations
            .AnyAsync(r => r.BookId == request.BookId && 
                          r.MemberId == request.MemberId && 
                          r.Status == ReservationStatus.Pending);
        
        if (existingReservation)
            return BadRequest(new { message = "لديك حجز نشط لهذا الكتاب بالفعل" });

        // 4. إنشاء الحجز
        var reservation = new BookReservation
        {
            BookId = request.BookId,
            MemberId = request.MemberId,
            Date = DateOnly.FromDateTime(DateTime.Today),
            Status = ReservationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.BookReservations.Add(reservation);
        await db.SaveChangesAsync();

        // 5. إشعار للطالب
        await notifier.SendAsync(member.StudentId, UserType.Student,
            "تم حجز كتاب",
            $"لقد قمت بحجز كتاب \"{book.Title}\" بنجاح",
            "library_reservation");

        return Created($"api/librarian/reservations/{reservation.Id}", new
        {
            message = "تم الحجز بنجاح",
            reservation = new
            {
                reservation.Id,
                reservation.BookId,
                BookTitle = book.Title,
                reservation.MemberId,
                MemberName = member.Student?.Name,
                reservation.Date,
                reservation.Status,
                reservation.CreatedAt
            }
        });
    }

    [HttpGet("reservations")]
    public async Task<IActionResult> GetReservations(
        [FromQuery] ReservationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = db.BookReservations
            .Include(r => r.Book)
            .Include(r => r.Member)
                .ThenInclude(m => m!.Student)
            .Where(r => r.Book!.SchoolId == SchoolId);

        if (status.HasValue)
            query = query.Where(r => r.Status == status);

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var reservations = await query
            .OrderByDescending(r => r.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                BookId = r.Book != null ? r.Book.Id : 0,
                BookTitle = r.Book != null ? r.Book.Title : null,
                BookLocalNumber = r.Book != null ? r.Book.LocalBookNumber : 0,
                MemberId = r.Member != null ? r.Member.Id : 0,
                MemberLocalNumber = r.Member != null ? r.Member.LocalMemberNumber : 0,
                StudentName = r.Member != null && r.Member.Student != null ? 
                    r.Member.Student.Name : null,
                r.Date,
                r.Status,
                r.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            totalCount,
            totalPages,
            page,
            pageSize,
            reservations
        });
    }

    [HttpPatch("reservations/{id:int}")]
    public async Task<IActionResult> UpdateReservation(int id, ReservationDecisionRequest request)
    {
        var reservation = await db.BookReservations
            .Include(r => r.Book)
            .Include(r => r.Member)
                .ThenInclude(m => m!.Student)
            .FirstOrDefaultAsync(r => r.Id == id && r.Book!.SchoolId == SchoolId);
        
        if (reservation is null)
            return NotFound(new { message = "الحجز غير موجود" });

        // إذا تمت الموافقة على الحجز
        if (request.Status == ReservationStatus.Fulfilled)
        {
            // التحقق من توفر نسخة
            if (reservation.Book!.AvailableCopies <= 0)
                return BadRequest(new { message = "لا توجد نسخ متاحة حالياً" });
            
            // إنشاء إعارة تلقائياً
            var loan = new BookLoan
            {
                BookId = reservation.BookId,
                MemberId = reservation.MemberId,
                LoanDate = DateOnly.FromDateTime(DateTime.Today),
                DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(14)),
                Status = LoanStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            reservation.Book.AvailableCopies--;
            db.BookLoans.Add(loan);
        }

        reservation.Status = request.Status;
        await db.SaveChangesAsync();

        // إشعار للطالب
        if (reservation.Member?.Student is not null)
        {
            var statusMessage = request.Status == ReservationStatus.Fulfilled ? "تمت الموافقة" : "تم الإلغاء";
            await notifier.SendAsync(reservation.Member.Student.Id, UserType.Student,
                $"تحديث حالة حجز كتاب",
                $"تم {statusMessage} على حجزك لكتاب \"{reservation.Book!.Title}\"",
                "library_reservation");
        }

        return Ok(new
        {
            message = "تم تحديث الحجز بنجاح",
            reservation = new
            {
                reservation.Id,
                reservation.BookId,
                BookTitle = reservation.Book?.Title,
                reservation.MemberId,
                StudentName = reservation.Member?.Student?.Name,
                reservation.Date,
                reservation.Status,
                reservation.CreatedAt
            }
        });
    }

    [HttpDelete("reservations/{id:int}")]
    public async Task<IActionResult> DeleteReservation(int id)
    {
        var reservation = await db.BookReservations
            .FirstOrDefaultAsync(r => r.Id == id && r.Book!.SchoolId == SchoolId);
        
        if (reservation is null)
            return NotFound(new { message = "الحجز غير موجود" });

        db.BookReservations.Remove(reservation);
        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف الحجز بنجاح" });
    }
}