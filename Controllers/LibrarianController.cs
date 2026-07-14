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
            return BadRequest(new { success = false, message = "يوجد كتاب بنفس الـ ISBN بالفعل" });
    }

    // 2. حساب الرقم المحلي للكتاب (يبدأ من 1)
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
        Isbn = request.Isbn ?? "",
        Copies = request.Copies,
        AvailableCopies = request.Copies,
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
            book.Isbn,
            book.Copies,
            book.AvailableCopies,
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
            b.LocalBookNumber,  // ✅ Local ID
            b.Title,
            b.Author,
            b.Isbn,
            b.Copies,
            b.AvailableCopies,
            IsAvailable = b.AvailableCopies > 0,
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
            availableBooks = books.Count(b => b.IsAvailable),
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
            b.LocalBookNumber,  // ✅ Local ID
            b.Title,
            b.Author,
            b.Isbn,
            b.Copies,
            b.AvailableCopies,
            IsAvailable = b.AvailableCopies > 0,
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

    // التحقق من عدم وجود كتاب بنفس الـ ISBN
    if (!string.IsNullOrEmpty(request.Isbn))
    {
        var existingBook = await db.Books
            .AnyAsync(b => b.Isbn == request.Isbn && 
                          b.SchoolId == SchoolId && 
                          b.LocalBookNumber != localBookNumber);
        
        if (existingBook)
            return BadRequest(new { success = false, message = "يوجد كتاب بنفس الـ ISBN بالفعل" });
    }

    book.Title = request.Title;
    book.Author = request.Author ?? "";
    book.Isbn = request.Isbn ?? "";
    book.Copies = request.Copies;
    book.AvailableCopies = request.Copies - (book.Copies - book.AvailableCopies);

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
        .Include(b => b.School)
        .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                  b.LocalBookNumber == localBookNumber);

    if (book is null)
        return NotFound(new { success = false, message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" });

    // التحقق من وجود استعارات نشطة للكتاب
    var activeLoans = await db.BookLoans
        .AnyAsync(l => l.BookId == book.Id && l.Status == LoanStatus.Active);

    if (activeLoans)
        return BadRequest(new { success = false, message = "لا يمكن حذف الكتاب لأن هناك استعارات نشطة له" });

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
public async Task<IActionResult> CreateLoan(LoanLocalRequest request)
{
    // 1. ✅ البحث عن الكتاب باستخدام LocalBookNumber
    var book = await db.Books
        .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                  b.LocalBookNumber == request.LocalBookNumber);
    
    if (book is null)
        return NotFound(new { success = false, message = $"لا يوجد كتاب برقم {request.LocalBookNumber} في المكتبة" });

    if (book.AvailableCopies <= 0)
        return BadRequest(new { success = false, message = "لا توجد نسخ متاحة" });

    // 2. ✅ البحث عن العضو باستخدام LocalMemberNumber
    var member = await db.LibraryMembers
        .Include(m => m.Student)
        .FirstOrDefaultAsync(m => m.SchoolId == SchoolId && 
                                  m.LocalMemberNumber == request.LocalMemberNumber);
    
    if (member is null)
        return NotFound(new { success = false, message = $"لا يوجد عضو برقم {request.LocalMemberNumber} في المكتبة" });

    if (member.Status != MemberStatus.Active)
        return BadRequest(new { success = false, message = "عضوية الطالب موقوفة" });

    // 3. ✅ حساب LocalLoanNumber
    var maxLocalNumber = await db.BookLoans
        .Where(l => l.Book!.SchoolId == SchoolId)
        .Select(l => (int?)l.LocalLoanNumber)
        .MaxAsync() ?? 0;

    int newLocalNumber = maxLocalNumber + 1;

    // 4. إنشاء الإعارة
    book.AvailableCopies--;
    
    var loan = new BookLoan
    {
        BookId = book.Id,
        MemberId = member.Id,
        LocalLoanNumber = newLocalNumber,  // ✅ تعيين Local ID
        LoanDate = DateOnly.FromDateTime(DateTime.Today),
        DueDate = request.DueDate,
        Status = LoanStatus.Active,
        CreatedAt = DateTime.UtcNow
    };

    db.BookLoans.Add(loan);

    // 5. تحديث الحجز إذا كان موجوداً
    var reservation = await db.BookReservations
        .FirstOrDefaultAsync(r => r.BookId == book.Id && 
                                  r.MemberId == member.Id && 
                                  r.Status == ReservationStatus.Pending);
    
    if (reservation is not null)
        reservation.Status = ReservationStatus.Fulfilled;

    await db.SaveChangesAsync();

    // 6. إشعار للطالب
    await notifier.SendAsync(member.StudentId, UserType.Student,
        "تم استعارة كتاب",
        $"لقد استعرت كتاب \"{book.Title}\" حتى تاريخ {request.DueDate}",
        "library_loan");

    return Created($"api/librarian/loans/{loan.LocalLoanNumber}", new
    {
        success = true,
        message = "تمت الإعارة بنجاح",
        data = new
        {
            loan.Id,
            loan.LocalLoanNumber,  // ✅ Local ID
            LocalBookNumber = book.LocalBookNumber,  // ✅ Local ID
            BookTitle = book.Title,
            LocalMemberNumber = member.LocalMemberNumber,  // ✅ Local ID
            MemberName = member.Student?.Name,
            loan.LoanDate,
            loan.DueDate,
            loan.Status
        }
    });
}

[HttpPost("loans/{localLoanNumber:int}/return")]
public async Task<IActionResult> ReturnLoan(int localLoanNumber)
{
    // ✅ البحث عن الإعارة باستخدام LocalLoanNumber
    var loan = await db.BookLoans
        .Include(l => l.Book)
        .Include(l => l.Member)
            .ThenInclude(m => m!.Student)
        .FirstOrDefaultAsync(l => l.LocalLoanNumber == localLoanNumber && 
                                  l.Book!.SchoolId == SchoolId);
    
    if (loan is null)
        return NotFound(new { success = false, message = $"لا توجد إعارة برقم {localLoanNumber}" });

    if (loan.Status == LoanStatus.Returned)
        return BadRequest(new { success = false, message = "الكتاب مُعاد بالفعل" });

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
        success = true,
        message = "تم إعادة الكتاب بنجاح",
        data = new
        {
            loan.Id,
            loan.LocalLoanNumber,  // ✅ Local ID
            LocalBookNumber = loan.Book != null ? loan.Book.LocalBookNumber : 0,
            BookTitle = loan.Book != null ? loan.Book.Title : null,
            LocalMemberNumber = loan.Member != null ? loan.Member.LocalMemberNumber : 0,
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
    [FromQuery] int? localMemberNumber,
    [FromQuery] int? localBookNumber,
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

    // ✅ تصفية حسب العضو باستخدام LocalMemberNumber
    if (localMemberNumber.HasValue)
    {
        var member = await db.LibraryMembers
            .Where(m => m.SchoolId == SchoolId && 
                        m.LocalMemberNumber == localMemberNumber.Value)
            .Select(m => m.Id)
            .FirstOrDefaultAsync();
        
        if (member > 0)
            query = query.Where(l => l.MemberId == member);
        else
            return Ok(new { success = true, message = "لا توجد إعارات", data = new List<object>() });
    }

    // ✅ تصفية حسب الكتاب باستخدام LocalBookNumber
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
            return Ok(new { success = true, message = "لا توجد إعارات", data = new List<object>() });
    }

    var totalCount = await query.CountAsync();
    var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

    var loans = await query
        .OrderByDescending(l => l.LoanDate)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(l => new
        {
            l.Id,
            l.LocalLoanNumber,  // ✅ Local ID
            LocalBookNumber = l.Book != null ? l.Book.LocalBookNumber : 0,
            BookTitle = l.Book != null ? l.Book.Title : null,
            LocalMemberNumber = l.Member != null ? l.Member.LocalMemberNumber : 0,
            StudentName = l.Member != null && l.Member.Student != null ? 
                l.Member.Student.Name : null,
            l.LoanDate,
            l.DueDate,
            l.ReturnDate,
            l.Status,
            IsOverdue = l.Status != LoanStatus.Returned && l.DueDate < today,
            l.CreatedAt
        })
        .ToListAsync();

    return Ok(new
    {
        success = true,
        message = "تم جلب الإعارات بنجاح",
        data = new
        {
            totalCount,
            totalPages,
            page,
            pageSize,
            loans
        }
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
            await notifier.SendAsync(student.Id, UserType.Student,
                "استحقاق إعادة كتاب",
                $"كتاب \"{loan.Book!.Title}\" مستحق الإعادة بتاريخ {loan.DueDate}",
                "library_due");
            
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
        success = true,
        message = "تم إرسال الإشعارات بنجاح",
        data = new
        {
            notified = notified,
            totalDue = dueLoans.Count
        }
    });
}

// ============================================
// إدارة الحجوزات (Reservations) - باستخدام Local IDs
// ============================================

[HttpPost("reservations")]
public async Task<IActionResult> CreateReservation(ReservationLocalRequest request)
{
    // 1. ✅ البحث عن الكتاب باستخدام LocalBookNumber
    var book = await db.Books
        .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                  b.LocalBookNumber == request.LocalBookNumber);
    
    if (book is null)
        return NotFound(new { success = false, message = $"لا يوجد كتاب برقم {request.LocalBookNumber} في المكتبة" });

    // 2. ✅ البحث عن العضو باستخدام LocalMemberNumber
    var member = await db.LibraryMembers
        .Include(m => m.Student)
        .FirstOrDefaultAsync(m => m.SchoolId == SchoolId && 
                                  m.LocalMemberNumber == request.LocalMemberNumber);
    
    if (member is null)
        return NotFound(new { success = false, message = $"لا يوجد عضو برقم {request.LocalMemberNumber} في المكتبة" });

    if (member.Status != MemberStatus.Active)
        return BadRequest(new { success = false, message = "عضوية الطالب موقوفة" });

    // 3. التحقق من عدم وجود حجز نشط
    var existingReservation = await db.BookReservations
        .AnyAsync(r => r.BookId == book.Id && 
                      r.MemberId == member.Id && 
                      r.Status == ReservationStatus.Pending);
    
    if (existingReservation)
        return BadRequest(new { success = false, message = "لديك حجز نشط لهذا الكتاب بالفعل" });

    // 4. إنشاء الحجز
    var reservation = new BookReservation
    {
        BookId = book.Id,
        MemberId = member.Id,
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
        success = true,
        message = "تم الحجز بنجاح",
        data = new
        {
            reservation.Id,
            LocalBookNumber = book.LocalBookNumber,  // ✅ Local ID
            BookTitle = book.Title,
            LocalMemberNumber = member.LocalMemberNumber,  // ✅ Local ID
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
    [FromQuery] int? localMemberNumber,
    [FromQuery] int? localBookNumber,
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

    // ✅ تصفية حسب العضو باستخدام LocalMemberNumber
    if (localMemberNumber.HasValue)
    {
        var member = await db.LibraryMembers
            .Where(m => m.SchoolId == SchoolId && 
                        m.LocalMemberNumber == localMemberNumber.Value)
            .Select(m => m.Id)
            .FirstOrDefaultAsync();
        
        if (member > 0)
            query = query.Where(r => r.MemberId == member);
        else
            return Ok(new { success = true, message = "لا توجد حجوزات", data = new List<object>() });
    }

    // ✅ تصفية حسب الكتاب باستخدام LocalBookNumber
    if (localBookNumber.HasValue)
    {
        var book = await db.Books
            .Where(b => b.SchoolId == SchoolId && 
                        b.LocalBookNumber == localBookNumber.Value)
            .Select(b => b.Id)
            .FirstOrDefaultAsync();
        
        if (book > 0)
            query = query.Where(r => r.BookId == book);
        else
            return Ok(new { success = true, message = "لا توجد حجوزات", data = new List<object>() });
    }

    var totalCount = await query.CountAsync();
    var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

    var reservations = await query
        .OrderByDescending(r => r.Date)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(r => new
        {
            r.Id,
            LocalBookNumber = r.Book != null ? r.Book.LocalBookNumber : 0,  // ✅ Local ID
            BookTitle = r.Book != null ? r.Book.Title : null,
            LocalMemberNumber = r.Member != null ? r.Member.LocalMemberNumber : 0,  // ✅ Local ID
            StudentName = r.Member != null && r.Member.Student != null ? 
                r.Member.Student.Name : null,
            r.Date,
            r.Status,
            r.CreatedAt
        })
        .ToListAsync();

    return Ok(new
    {
        success = true,
        message = "تم جلب الحجوزات بنجاح",
        data = new
        {
            totalCount,
            totalPages,
            page,
            pageSize,
            reservations
        }
    });
}

[HttpGet("reservations/{localBookNumber:int}/{localMemberNumber:int}")]
public async Task<IActionResult> GetReservation(int localBookNumber, int localMemberNumber)
{
    // ✅ البحث عن الكتاب
    var book = await db.Books
        .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                  b.LocalBookNumber == localBookNumber);
    
    if (book is null)
        return NotFound(new { success = false, message = $"لا يوجد كتاب برقم {localBookNumber}" });

    // ✅ البحث عن العضو
    var member = await db.LibraryMembers
        .FirstOrDefaultAsync(m => m.SchoolId == SchoolId && 
                                  m.LocalMemberNumber == localMemberNumber);
    
    if (member is null)
        return NotFound(new { success = false, message = $"لا يوجد عضو برقم {localMemberNumber}" });

    var reservation = await db.BookReservations
        .Include(r => r.Book)
        .Include(r => r.Member)
            .ThenInclude(m => m!.Student)
        .Where(r => r.BookId == book.Id && 
                    r.MemberId == member.Id)
        .OrderByDescending(r => r.CreatedAt)
        .Select(r => new
        {
            r.Id,
            LocalBookNumber = r.Book != null ? r.Book.LocalBookNumber : 0,
            BookTitle = r.Book != null ? r.Book.Title : null,
            LocalMemberNumber = r.Member != null ? r.Member.LocalMemberNumber : 0,
            StudentName = r.Member != null && r.Member.Student != null ? 
                r.Member.Student.Name : null,
            r.Date,
            r.Status,
            r.CreatedAt
        })
        .FirstOrDefaultAsync();

    if (reservation is null)
        return NotFound(new { success = false, message = "لا يوجد حجز لهذا العضو على هذا الكتاب" });

    return Ok(new
    {
        success = true,
        message = "تم جلب الحجز بنجاح",
        data = reservation
    });
}

[HttpPatch("reservations/{localBookNumber:int}/{localMemberNumber:int}")]
public async Task<IActionResult> UpdateReservation(int localBookNumber, int localMemberNumber, ReservationDecisionRequest request)
{
    // ✅ البحث عن الكتاب باستخدام LocalBookNumber
    var book = await db.Books
        .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                  b.LocalBookNumber == localBookNumber);
    
    if (book is null)
        return NotFound(new { success = false, message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" });

    // ✅ البحث عن العضو باستخدام LocalMemberNumber
    var member = await db.LibraryMembers
        .FirstOrDefaultAsync(m => m.SchoolId == SchoolId && 
                                  m.LocalMemberNumber == localMemberNumber);
    
    if (member is null)
        return NotFound(new { success = false, message = $"لا يوجد عضو برقم {localMemberNumber} في المكتبة" });

    // ✅ البحث عن الحجز
    var reservation = await db.BookReservations
        .Include(r => r.Book)
        .Include(r => r.Member)
            .ThenInclude(m => m!.Student)
        .FirstOrDefaultAsync(r => r.BookId == book.Id && 
                                  r.MemberId == member.Id &&
                                  r.Status == ReservationStatus.Pending);

    if (reservation is null)
        return NotFound(new { success = false, message = "لا يوجد حجز معلق لهذا العضو على هذا الكتاب" });

    // إذا تمت الموافقة على الحجز
    if (request.Status == ReservationStatus.Fulfilled)
    {
        // التحقق من توفر نسخة
        if (book.AvailableCopies <= 0)
            return BadRequest(new { success = false, message = "لا توجد نسخ متاحة حالياً" });
        
        // ✅ حساب LocalLoanNumber
        var maxLocalNumber = await db.BookLoans
            .Where(l => l.Book!.SchoolId == SchoolId)
            .Select(l => (int?)l.LocalLoanNumber)
            .MaxAsync() ?? 0;

        int newLocalNumber = maxLocalNumber + 1;

        // إنشاء إعارة تلقائياً
        var loan = new BookLoan
        {
            BookId = book.Id,
            MemberId = member.Id,
            LocalLoanNumber = newLocalNumber,
            LoanDate = DateOnly.FromDateTime(DateTime.Today),
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(14)),
            Status = LoanStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        book.AvailableCopies--;
        db.BookLoans.Add(loan);
    }

    reservation.Status = request.Status;
    await db.SaveChangesAsync();

    // إشعار للطالب
    if (member.Student is not null)
    {
        var statusMessage = request.Status == ReservationStatus.Fulfilled ? "تمت الموافقة" : "تم الإلغاء";
        await notifier.SendAsync(member.Student.Id, UserType.Student,
            "تحديث حالة حجز كتاب",
            $"تم {statusMessage} على حجزك لكتاب \"{book.Title}\"",
            "library_reservation");
    }

    return Ok(new
    {
        success = true,
        message = "تم تحديث الحجز بنجاح",
        data = new
        {
            reservation.Id,
            LocalBookNumber = book.LocalBookNumber,
            BookTitle = book.Title,
            LocalMemberNumber = member.LocalMemberNumber,
            StudentName = member.Student?.Name,
            reservation.Date,
            reservation.Status,
            reservation.CreatedAt
        }
    });
}

[HttpDelete("reservations/{localBookNumber:int}/{localMemberNumber:int}")]
public async Task<IActionResult> DeleteReservation(int localBookNumber, int localMemberNumber)
{
    // ✅ البحث عن الكتاب باستخدام LocalBookNumber
    var book = await db.Books
        .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                  b.LocalBookNumber == localBookNumber);
    
    if (book is null)
        return NotFound(new { success = false, message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" });

    // ✅ البحث عن العضو باستخدام LocalMemberNumber
    var member = await db.LibraryMembers
        .FirstOrDefaultAsync(m => m.SchoolId == SchoolId && 
                                  m.LocalMemberNumber == localMemberNumber);
    
    if (member is null)
        return NotFound(new { success = false, message = $"لا يوجد عضو برقم {localMemberNumber} في المكتبة" });

    // ✅ البحث عن الحجز
    var reservation = await db.BookReservations
        .Include(r => r.Book)
        .FirstOrDefaultAsync(r => r.BookId == book.Id && 
                                  r.MemberId == member.Id &&
                                  r.Status == ReservationStatus.Pending);
    
    if (reservation is null)
        return NotFound(new { success = false, message = "لا يوجد حجز معلق لهذا العضو على هذا الكتاب" });

    db.BookReservations.Remove(reservation);
    await db.SaveChangesAsync();

    return Ok(new
    {
        success = true,
        message = "تم حذف الحجز بنجاح",
        data = new
        {
            LocalBookNumber = book.LocalBookNumber,
            BookTitle = book.Title,
            LocalMemberNumber = member.LocalMemberNumber,
            MemberName = member.Student?.Name
        }
    });
}
}