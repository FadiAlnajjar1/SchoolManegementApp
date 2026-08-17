// Dtos/TransferDTO.cs
using System.ComponentModel.DataAnnotations;
using SchoolManagement.Api.Models;

public class TransferStudentByIdRequest
{
    public int StudentId { get; set; }
    public int NewSchoolId { get; set; }
    public int? LocalGradeNumber { get; set; }
    public int? LocalSectionNumber { get; set; }
}

public class TransferEmployeeRequest
{
    public int EmployeeId { get; set; }          // ID الموظف
    public int CurrentSchoolId { get; set; }     // المدرسة الحالية
    public int NewSchoolId { get; set; }         // المدرسة الجديدة
    public EmployeeRole NewRole { get; set; }    // الوظيفة الجديدة
}