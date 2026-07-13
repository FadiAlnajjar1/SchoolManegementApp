// Dtos/TeacherSubjectDtos.cs
namespace SchoolManagement.Api.Dtos;

// Dtos/TeacherSubjectRequest.cs
public class TeacherSubjectRequest
{
    public int TeacherId { get; set; } // Employee.Id
    public int SubjectId { get; set; } // Subject.Id (العالمي)
}

// ✅ إضافة نسخة جديدة باستخدام LocalSubjectId
public class TeacherSubjectLocalRequest
{
    public int TeacherLocalNumber { get; set; } // LocalEmployeeNumber
    public int LocalSubjectId { get; set; } // LocalSubjectId
}

public class TeacherSubjectResponse
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public int SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public DateTime CreatedAt { get; set; }
}