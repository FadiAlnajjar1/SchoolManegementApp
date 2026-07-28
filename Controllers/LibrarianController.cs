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

        // حساب الرقم المحلي للكتاب (يبدأ من 1)
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
                b.LocalBookNumber,
                b.Title,
                b.Author,
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


        book.Title = request.Title;
        book.Author = request.Author ?? "";
        
        // تحديث عدد النسخ مع الحفاظ على النسخ المتاحة
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

        // التحقق من وجود استعارات نشطة للكتاب
        var activeLoans = await db.BookLoans
            .AnyAsync(l => l.BookId == book.Id && l.Status == LoanStatus.Active);

        if (activeLoans)
            return BadRequest(new { success = false, message = "لا يمكن حذف الكتاب لأن هناك استعارات نشطة له" });

        // حذف الحجوزات والإعارات المرتبطة
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
    // طلبات الاستعارة (Loan Requests) - بدون عضوية
    // ============================================

    [HttpPost("loans/requests")]
    public async Task<IActionResult> CreateLoanRequest(LoanRequestLocalRequest request)
    {
        // 1. ✅ البحث عن الكتاب باستخدام LocalBookNumber
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                      b.LocalBookNumber == request.LocalBookNumber);
        
        if (book is null)
            return NotFound(new { success = false, message = $"لا يوجد كتاب برقم {request.LocalBookNumber} في المكتبة" });

        if (book.AvailableCopies <= 0)
            return BadRequest(new { success = false, message = "لا توجد نسخ متاحة من هذا الكتاب" });

        // 2. ✅ البحث عن الطالب باستخدام LocalStudentNumber
        var student = await db.Students
            .Include(s => s.Section)
                .ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalStudentNumber == request.LocalStudentNumber);
        
        if (student is null)
            return NotFound(new { success = false, message = $"لا يوجد طالب برقم {request.LocalStudentNumber} في المدرسة" });

        // 3. التحقق من أن الطالب ليس عنده كتب مستعارة غير معادة (أكثر من 3 كتب)
        var activeLoansCount = await db.BookLoans
            .CountAsync(l => l.StudentId == student.Id && l.Status == LoanStatus.Active);
        
        if (activeLoansCount >= 3)
            return BadRequest(new { success = false, message = "لا يمكن للطالب استعارة أكثر من 3 كتب في نفس الوقت" });

        // 4. التحقق من عدم وجود طلب استعارة معلق لهذا الطالب على هذا الكتاب
        var existingRequest = await db.BookLoanRequests
            .AnyAsync(r => r.BookId == book.Id && 
                          r.StudentId == student.Id && 
                          r.Status == LoanRequestStatus.Pending);
        
        if (existingRequest)
            return BadRequest(new { success = false, message = "يوجد طلب استعارة معلق لهذا الكتاب بالفعل" });

        // 5. حساب رقم الطلب المحلي
        var maxLocalNumber = await db.BookLoanRequests
            .Where(r => r.Book!.SchoolId == SchoolId)
            .Select(r => (int?)r.LocalRequestNumber)
            .MaxAsync() ?? 0;

        int newLocalNumber = maxLocalNumber + 1;

        // 6. إنشاء طلب الاستعارة
        var loanRequest = new BookLoanRequest
        {
            BookId = book.Id,
            StudentId = student.Id,
            LocalRequestNumber = newLocalNumber,
            RequestDate = DateTime.UtcNow,
            Status = LoanRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.BookLoanRequests.Add(loanRequest);
        await db.SaveChangesAsync();

        // 7. إشعار لأمين المكتبة (سيتم إرساله عن طريق الـ Notification)
        await notifier.SendToLibrarianAsync(
            SchoolId,
            "طلب استعارة جديد",
            $"الطالب {student.Name} يطلب استعارة كتاب \"{book.Title}\"",
            "loan_request");

        return Created($"api/librarian/loans/requests/{loanRequest.LocalRequestNumber}", new
        {
            success = true,
            message = "تم إرسال طلب الاستعارة بنجاح، في انتظار موافقة أمين المكتبة",
            data = new
            {
                loanRequest.Id,
                loanRequest.LocalRequestNumber,
                LocalBookNumber = book.LocalBookNumber,
                BookTitle = book.Title,
                LocalStudentNumber = student.LocalStudentNumber,
                StudentName = student.Name,
                SectionName = student.Section?.Name,
                LocalSectionNumber = student.Section?.LocalSectionNumber ?? 0,
                GradeName = student.Section?.Grade?.Name,
                LocalGradeNumber = student.Section?.Grade?.LocalGradeNumber ?? 0,
                loanRequest.RequestDate,
                loanRequest.Status,
                loanRequest.CreatedAt
            }
        });
    }

    // ============================================
// 1. جلب جميع الطلبات حسب الحالة (بدون فلتر)
// ============================================

[HttpGet("loans/requests")]
public async Task<IActionResult> GetAllLoanRequests(
    [FromQuery] LoanRequestStatus? status = null)
{
    var query = db.BookLoanRequests
        .Include(r => r.Book)
        .Include(r => r.Student)
            .ThenInclude(s => s!.Section)
                .ThenInclude(sec => sec!.Grade)
        .Where(r => r.Book!.SchoolId == SchoolId);

    // تصفية حسب الحالة (اختياري)
    if (status.HasValue)
        query = query.Where(r => r.Status == status);

    var requests = await query
        .OrderByDescending(r => r.RequestDate)
        .Select(r => new
        {
            r.Id,
            r.LocalRequestNumber,
            LocalBookNumber = r.Book != null ? r.Book.LocalBookNumber : 0,
            BookTitle = r.Book != null ? r.Book.Title : null,
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
            r.RequestDate,
            r.Status,
            StatusName = r.Status.ToString(),
            StatusArabic = r.Status == LoanRequestStatus.Pending ? "قيد الانتظار" :
                          r.Status == LoanRequestStatus.Approved ? "تمت الموافقة" :
                          r.Status == LoanRequestStatus.Rejected ? "مرفوض" :
                          r.Status == LoanRequestStatus.Cancelled ? "ملغي" : "غير معروف",
            r.CreatedAt
        })
        .ToListAsync();

    return Ok(new
    {
        success = true,
        message = "تم جلب طلبات الاستعارة بنجاح",
        data = new
        {
            totalCount = requests.Count,
            pendingCount = requests.Count(r => r.Status == LoanRequestStatus.Pending),
            approvedCount = requests.Count(r => r.Status == LoanRequestStatus.Approved),
            rejectedCount = requests.Count(r => r.Status == LoanRequestStatus.Rejected),
            cancelledCount = requests.Count(r => r.Status == LoanRequestStatus.Cancelled),
            requests = requests
        }
    });
}

// ============================================
// 2. جلب طلبات طالب معين حسب الحالة
// ============================================

[HttpGet("loans/requests/student/{localStudentNumber:int}")]
public async Task<IActionResult> GetLoanRequestsByStudent(
    int localStudentNumber,
    [FromQuery] LoanRequestStatus? status = null)
{
    // ✅ البحث عن الطالب
    var student = await db.Students
        .Where(s => s.SchoolId == SchoolId && 
                    s.LocalStudentNumber == localStudentNumber)
        .Select(s => new { s.Id, s.Name, s.LocalStudentNumber })
        .FirstOrDefaultAsync();

    if (student is null)
        return NotFound(new { 
            success = false, 
            message = $"لا يوجد طالب برقم {localStudentNumber} في هذه المدرسة" 
        });

    var query = db.BookLoanRequests
        .Include(r => r.Book)
        .Include(r => r.Student)
            .ThenInclude(s => s!.Section)
                .ThenInclude(sec => sec!.Grade)
        .Where(r => r.Book!.SchoolId == SchoolId && 
                    r.StudentId == student.Id);

    // تصفية حسب الحالة (اختياري)
    if (status.HasValue)
        query = query.Where(r => r.Status == status);

    var requests = await query
        .OrderByDescending(r => r.RequestDate)
        .Select(r => new
        {
            r.Id,
            r.LocalRequestNumber,
            LocalBookNumber = r.Book != null ? r.Book.LocalBookNumber : 0,
            BookTitle = r.Book != null ? r.Book.Title : null,
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
            r.RequestDate,
            r.Status,
            StatusName = r.Status.ToString(),
            StatusArabic = r.Status == LoanRequestStatus.Pending ? "قيد الانتظار" :
                          r.Status == LoanRequestStatus.Approved ? "تمت الموافقة" :
                          r.Status == LoanRequestStatus.Rejected ? "مرفوض" :
                          r.Status == LoanRequestStatus.Cancelled ? "ملغي" : "غير معروف",
            r.CreatedAt
        })
        .ToListAsync();

    return Ok(new
    {
        success = true,
        message = $"تم جلب طلبات الاستعارة للطالب {student.Name} بنجاح",
        data = new
        {
            Student = new
            {
                student.Id,
                student.Name,
                student.LocalStudentNumber
            },
            totalCount = requests.Count,
            pendingCount = requests.Count(r => r.Status == LoanRequestStatus.Pending),
            approvedCount = requests.Count(r => r.Status == LoanRequestStatus.Approved),
            rejectedCount = requests.Count(r => r.Status == LoanRequestStatus.Rejected),
            cancelledCount = requests.Count(r => r.Status == LoanRequestStatus.Cancelled),
            requests = requests
        }
    });
}

// ============================================
// 3. جلب طلبات كتاب معين حسب الحالة
// ============================================

[HttpGet("loans/requests/book/{localBookNumber:int}")]
public async Task<IActionResult> GetLoanRequestsByBook(
    int localBookNumber,
    [FromQuery] LoanRequestStatus? status = null)
{
    // ✅ البحث عن الكتاب
    var book = await db.Books
        .Where(b => b.SchoolId == SchoolId && 
                    b.LocalBookNumber == localBookNumber)
        .Select(b => new { b.Id, b.Title, b.LocalBookNumber, b.Author })
        .FirstOrDefaultAsync();

    if (book is null)
        return NotFound(new { 
            success = false, 
            message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" 
        });

    var query = db.BookLoanRequests
        .Include(r => r.Book)
        .Include(r => r.Student)
            .ThenInclude(s => s!.Section)
                .ThenInclude(sec => sec!.Grade)
        .Where(r => r.Book!.SchoolId == SchoolId && 
                    r.BookId == book.Id);

    // تصفية حسب الحالة (اختياري)
    if (status.HasValue)
        query = query.Where(r => r.Status == status);

    var requests = await query
        .OrderByDescending(r => r.RequestDate)
        .Select(r => new
        {
            r.Id,
            r.LocalRequestNumber,
            LocalBookNumber = r.Book != null ? r.Book.LocalBookNumber : 0,
            BookTitle = r.Book != null ? r.Book.Title : null,
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
            r.RequestDate,
            r.Status,
            StatusName = r.Status.ToString(),
            StatusArabic = r.Status == LoanRequestStatus.Pending ? "قيد الانتظار" :
                          r.Status == LoanRequestStatus.Approved ? "تمت الموافقة" :
                          r.Status == LoanRequestStatus.Rejected ? "مرفوض" :
                          r.Status == LoanRequestStatus.Cancelled ? "ملغي" : "غير معروف",
            r.CreatedAt
        })
        .ToListAsync();

    return Ok(new
    {
        success = true,
        message = $"تم جلب طلبات الاستعارة للكتاب {book.Title} بنجاح",
        data = new
        {
            Book = new
            {
                book.Id,
                book.Title,
                book.LocalBookNumber,
                book.Author
            },
            totalCount = requests.Count,
            pendingCount = requests.Count(r => r.Status == LoanRequestStatus.Pending),
            approvedCount = requests.Count(r => r.Status == LoanRequestStatus.Approved),
            rejectedCount = requests.Count(r => r.Status == LoanRequestStatus.Rejected),
            cancelledCount = requests.Count(r => r.Status == LoanRequestStatus.Cancelled),
            requests = requests
        }
    });
}


    // ============================================
// الموافقة على طلب استعارة - باستخدام Local IDs
// ============================================

[HttpPut("loans/requests/approve")]
public async Task<IActionResult> ApproveLoanRequest(
    [FromQuery] int localStudentNumber,
    [FromQuery] int localBookNumber)
{
    // ✅ البحث عن الطالب باستخدام LocalStudentNumber
    var student = await db.Students
        .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                  s.LocalStudentNumber == localStudentNumber);

    if (student is null)
        return NotFound(new { 
            success = false, 
            message = $"لا يوجد طالب برقم {localStudentNumber} في هذه المدرسة" 
        });

    // ✅ البحث عن الكتاب باستخدام LocalBookNumber
    var book = await db.Books
        .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                  b.LocalBookNumber == localBookNumber);

    if (book is null)
        return NotFound(new { 
            success = false, 
            message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" 
        });

    // ✅ البحث عن طلب الاستعارة المعلق
    var loanRequest = await db.BookLoanRequests
        .Include(r => r.Book)
        .Include(r => r.Student)
        .FirstOrDefaultAsync(r => r.BookId == book.Id && 
                                  r.StudentId == student.Id && 
                                  r.Status == LoanRequestStatus.Pending);

    if (loanRequest is null)
        return NotFound(new { 
            success = false, 
            message = $"لا يوجد طلب استعارة معلق للطالب {localStudentNumber} على الكتاب {localBookNumber}" 
        });

    // التحقق من توفر نسخة
    if (book.AvailableCopies <= 0)
        return BadRequest(new { 
            success = false, 
            message = "لا توجد نسخ متاحة من هذا الكتاب حالياً" 
        });

    // التحقق من أن الطالب ليس عنده أكثر من 3 كتب مستعارة
    var activeLoansCount = await db.BookLoans
        .CountAsync(l => l.StudentId == student.Id && l.Status == LoanStatus.Active);
    
    if (activeLoansCount >= 3)
        return BadRequest(new { 
            success = false, 
            message = "الطالب لديه 3 كتب مستعارة بالفعل، لا يمكنه استعارة المزيد" 
        });

    // ✅ حساب LocalLoanNumber
    var maxLocalNumber = await db.BookLoans
        .Where(l => l.Book!.SchoolId == SchoolId)
        .Select(l => (int?)l.LocalLoanNumber)
        .MaxAsync() ?? 0;

    int newLocalNumber = maxLocalNumber + 1;

    // إنشاء الإعارة
    book.AvailableCopies--;
    
    var loan = new BookLoan
    {
        BookId = book.Id,
        StudentId = student.Id,
        LocalLoanNumber = newLocalNumber,
        LoanDate = DateOnly.FromDateTime(DateTime.Today),
        DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(14)), // مدة الاستعارة 14 يوم
        Status = LoanStatus.Active,
        CreatedAt = DateTime.UtcNow
    };

    db.BookLoans.Add(loan);

    // تحديث حالة الطلب
    loanRequest.Status = LoanRequestStatus.Approved;
    loanRequest.ProcessedAt = DateTime.UtcNow;

    // حذف أي حجوزات معلقة لهذا الطالب على هذا الكتاب
    var reservations = await db.BookReservations
        .Where(r => r.BookId == book.Id && 
                   r.StudentId == student.Id && 
                   r.Status == ReservationStatus.Pending)
        .ToListAsync();
    
    foreach (var res in reservations)
        res.Status = ReservationStatus.Fulfilled;

    await db.SaveChangesAsync();

    // إشعار للطالب بالموافقة
    await notifier.SendAsync(student.Id, UserType.Student,
        "تمت الموافقة على طلب استعارة كتاب",
        $"تمت الموافقة على طلب استعارتك لكتاب \"{book.Title}\". مدة الاستعارة 14 يوماً تنتهي في {loan.DueDate}",
        "loan_approved");

    return Ok(new
    {
        success = true,
        message = "تمت الموافقة على طلب الاستعارة وإنشاء الإعارة بنجاح",
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
                book.Author
            },
            LoanRequest = new
            {
                loanRequest.Id,
                loanRequest.LocalRequestNumber,
                loanRequest.Status,
                loanRequest.ProcessedAt,
                RequestDate = loanRequest.RequestDate
            },
            Loan = new
            {
                loan.Id,
                loan.LocalLoanNumber,
                loan.LoanDate,
                loan.DueDate,
                loan.Status,
                loan.CreatedAt
            },
            AvailableCopiesRemaining = book.AvailableCopies
        }
    });
}


    // ============================================
    // إدارة الإعارات (Loans)
    // ============================================

    [HttpPost("loans/return/{localLoanNumber:int}")]
    public async Task<IActionResult> ReturnLoan(int localLoanNumber)
    {
        // ✅ البحث عن الإعارة باستخدام LocalLoanNumber
        var loan = await db.BookLoans
            .Include(l => l.Book)
            .Include(l => l.Student)
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
        if (loan.Student is not null)
        {
            await notifier.SendAsync(loan.Student.Id, UserType.Student,
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
                loan.LocalLoanNumber,
                LocalBookNumber = loan.Book != null ? loan.Book.LocalBookNumber : 0,
                BookTitle = loan.Book != null ? loan.Book.Title : null,
                LocalStudentNumber = loan.Student != null ? loan.Student.LocalStudentNumber : 0,
                StudentName = loan.Student != null ? loan.Student.Name : null,
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
        [FromQuery] int? localStudentNumber,
        [FromQuery] int? localBookNumber,
        [FromQuery] LoanStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var query = db.BookLoans
            .Include(l => l.Book)
            .Include(l => l.Student)
            .Where(l => l.Book!.SchoolId == SchoolId);

        // تصفية حسب الحالة
        if (status.HasValue)
            query = query.Where(l => l.Status == status);

        // تصفية حسب التأخير
        if (overdue == true)
            query = query.Where(l => l.Status != LoanStatus.Returned && l.DueDate < today);
        else if (overdue == false)
            query = query.Where(l => l.Status == LoanStatus.Returned);

        // ✅ تصفية حسب الطالب باستخدام LocalStudentNumber
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
                l.LocalLoanNumber,
                LocalBookNumber = l.Book != null ? l.Book.LocalBookNumber : 0,
                BookTitle = l.Book != null ? l.Book.Title : null,
                BookAuthor = l.Book != null ? l.Book.Author : null,
                LocalStudentNumber = l.Student != null ? l.Student.LocalStudentNumber : 0,
                StudentName = l.Student != null ? l.Student.Name : null,
                l.LoanDate,
                l.DueDate,
                l.ReturnDate,
                l.Status,
                StatusName = l.Status.ToString(),
                StatusArabic = l.Status == LoanStatus.Active ? "نشط" :
                              l.Status == LoanStatus.Returned ? "مُعاد" :
                              l.Status == LoanStatus.Overdue ? "متأخر" : "غير معروف",
                IsOverdue = l.Status != LoanStatus.Returned && l.DueDate < today,
                DaysOverdue = l.Status != LoanStatus.Returned && l.DueDate < today ? 
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
                totalCount,
                totalPages,
                page,
                pageSize,
                activeLoans = await db.BookLoans
                    .CountAsync(l => l.Book!.SchoolId == SchoolId && l.Status == LoanStatus.Active),
                overdueLoans = await db.BookLoans
                    .CountAsync(l => l.Book!.SchoolId == SchoolId && 
                                    l.Status != LoanStatus.Returned && l.DueDate < today),
                loans
            }
        });
    }

    [HttpGet("loans/student/{localStudentNumber:int}")]
    public async Task<IActionResult> GetStudentLoans(int localStudentNumber)
    {
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalStudentNumber == localStudentNumber);
        
        if (student is null)
            return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber} في المدرسة" });

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
                IsOverdue = l.Status != LoanStatus.Returned && l.DueDate < DateOnly.FromDateTime(DateTime.Today)
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "تم جلب إعارات الطالب بنجاح",
            data = new
            {
                Student = new
                {
                    student.Id,
                    student.Name,
                    student.LocalStudentNumber,
                    SectionName = student.Section != null ? student.Section.Name : null,
                    GradeName = student.Section != null && student.Section.Grade != null ? 
                        student.Section.Grade.Name : null
                },
                totalLoans = loans.Count,
                activeLoans = loans.Count(l => l.Status == LoanStatus.Active),
                loans = loans
            }
        });
    }

    // ============================================
    // الحجوزات (Reservations) - بدون عضوية
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

        // 2. ✅ البحث عن الطالب باستخدام LocalStudentNumber
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalStudentNumber == request.LocalStudentNumber);
        
        if (student is null)
            return NotFound(new { success = false, message = $"لا يوجد طالب برقم {request.LocalStudentNumber} في المدرسة" });

        // 3. التحقق من عدم وجود حجز نشط
        var existingReservation = await db.BookReservations
            .AnyAsync(r => r.BookId == book.Id && 
                          r.StudentId == student.Id && 
                          r.Status == ReservationStatus.Pending);
        
        if (existingReservation)
            return BadRequest(new { success = false, message = "لديك حجز نشط لهذا الكتاب بالفعل" });

        // 4. إنشاء الحجز
        var reservation = new BookReservation
        {
            BookId = book.Id,
            StudentId = student.Id,
            Date = DateOnly.FromDateTime(DateTime.Today),
            Status = ReservationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.BookReservations.Add(reservation);
        await db.SaveChangesAsync();

        // 5. إشعار للطالب
        await notifier.SendAsync(student.Id, UserType.Student,
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
                LocalBookNumber = book.LocalBookNumber,
                BookTitle = book.Title,
                LocalStudentNumber = student.LocalStudentNumber,
                StudentName = student.Name,
                reservation.Date,
                reservation.Status,
                reservation.CreatedAt
            }
        });
    }

    [HttpGet("reservations")]
    public async Task<IActionResult> GetReservations(
        [FromQuery] ReservationStatus? status,
        [FromQuery] int? localStudentNumber,
        [FromQuery] int? localBookNumber,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = db.BookReservations
            .Include(r => r.Book)
            .Include(r => r.Student)
            .Where(r => r.Book!.SchoolId == SchoolId);

        if (status.HasValue)
            query = query.Where(r => r.Status == status);

        // ✅ تصفية حسب الطالب باستخدام LocalStudentNumber
        if (localStudentNumber.HasValue)
        {
            var student = await db.Students
                .Where(s => s.SchoolId == SchoolId && 
                            s.LocalStudentNumber == localStudentNumber.Value)
                .Select(s => s.Id)
                .FirstOrDefaultAsync();
            
            if (student > 0)
                query = query.Where(r => r.StudentId == student);
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
                LocalBookNumber = r.Book != null ? r.Book.LocalBookNumber : 0,
                BookTitle = r.Book != null ? r.Book.Title : null,
                BookAuthor = r.Book != null ? r.Book.Author : null,
                LocalStudentNumber = r.Student != null ? r.Student.LocalStudentNumber : 0,
                StudentName = r.Student != null ? r.Student.Name : null,
                r.Date,
                r.Status,
                StatusName = r.Status.ToString(),
                StatusArabic = r.Status == ReservationStatus.Pending ? "قيد الانتظار" :
                              r.Status == ReservationStatus.Fulfilled ? "تم التنفيذ" :
                              r.Status == ReservationStatus.Cancelled ? "ملغي" : "غير معروف",
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

    [HttpDelete("reservations/{localBookNumber:int}/{localStudentNumber:int}")]
    public async Task<IActionResult> DeleteReservation(int localBookNumber, int localStudentNumber)
    {
        // ✅ البحث عن الكتاب باستخدام LocalBookNumber
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.SchoolId == SchoolId && 
                                      b.LocalBookNumber == localBookNumber);
        
        if (book is null)
            return NotFound(new { success = false, message = $"لا يوجد كتاب برقم {localBookNumber} في المكتبة" });

        // ✅ البحث عن الطالب باستخدام LocalStudentNumber
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolId && 
                                      s.LocalStudentNumber == localStudentNumber);
        
        if (student is null)
            return NotFound(new { success = false, message = $"لا يوجد طالب برقم {localStudentNumber} في المدرسة" });

        // ✅ البحث عن الحجز
        var reservation = await db.BookReservations
            .FirstOrDefaultAsync(r => r.BookId == book.Id && 
                                      r.StudentId == student.Id &&
                                      r.Status == ReservationStatus.Pending);
        
        if (reservation is null)
            return NotFound(new { success = false, message = "لا يوجد حجز معلق لهذا الطالب على هذا الكتاب" });

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
                LocalStudentNumber = student.LocalStudentNumber,
                StudentName = student.Name
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
            // تحديث حالة الإعارة إلى متأخرة
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

    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics()
    {
        var totalBooks = await db.Books.CountAsync(b => b.SchoolId == SchoolId);
        var availableBooks = await db.Books.SumAsync(b => b.AvailableCopies);
        var totalStudents = await db.Students.CountAsync(s => s.SchoolId == SchoolId);
        
        var activeLoans = await db.BookLoans
            .CountAsync(l => l.Book!.SchoolId == SchoolId && l.Status == LoanStatus.Active);
        
        var overdueLoans = await db.BookLoans
            .CountAsync(l => l.Book!.SchoolId == SchoolId && 
                            l.Status == LoanStatus.Overdue);
        
        var pendingRequests = await db.BookLoanRequests
            .CountAsync(r => r.Book!.SchoolId == SchoolId && r.Status == LoanRequestStatus.Pending);
        
        var pendingReservations = await db.BookReservations
            .CountAsync(r => r.Book!.SchoolId == SchoolId && r.Status == ReservationStatus.Pending);

        return Ok(new
        {
            success = true,
            message = "تم جلب إحصائيات المكتبة بنجاح",
            data = new
            {
                Books = new
                {
                    Total = totalBooks,
                    Available = availableBooks,
                    Borrowed = activeLoans,
                    Overdue = overdueLoans
                },
                Loans = new
                {
                    Active = activeLoans,
                    Overdue = overdueLoans,
                    PendingRequests = pendingRequests
                },
                Reservations = new
                {
                    Pending = pendingReservations
                },
                Students = new
                {
                    Total = totalStudents,
                    WithActiveLoans = await db.BookLoans
                        .Where(l => l.Book!.SchoolId == SchoolId && l.Status == LoanStatus.Active)
                        .Select(l => l.StudentId)
                        .Distinct()
                        .CountAsync()
                }
            }
        });
    }
}