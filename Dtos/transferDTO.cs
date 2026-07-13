using System.ComponentModel.DataAnnotations;
using SchoolManagement.Api.Models;

public class TransferRequest1
{
    public int StudentId { get; set; }        // ID الطالب
    public int CurrentSchoolId { get; set; }  // المدرسة الحالية (للتأكيد)
    public int NewSchoolId { get; set; }      // المدرسة الجديدة
    public int? GradeId { get; set; }         // الصف الجديد (اختياري)

}

// Dtos/TransferEmployeeRequest.cs

public class TransferEmployeeRequest
{
    public int EmployeeId { get; set; }          // ID الموظف
    public int CurrentSchoolId { get; set; }     // المدرسة الحالية
    public int NewSchoolId { get; set; }         // المدرسة الجديدة
    public EmployeeRole NewRole { get; set; }    // الوظيفة الجديدة
}