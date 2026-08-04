using PRN232.LMS.API.Models.Responses;
using PRN232.LMS.Services.Models;

namespace PRN232.LMS.API.Controllers.Helpers;

/// <summary>Maps between Business Models and Response Models</summary>
public static class ResponseMapper
{
    public static SemesterResponse ToResponse(SemesterBusinessModel m) => new()
    {
        SemesterId   = m.SemesterId,
        SemesterName = m.SemesterName,
        StartDate    = m.StartDate,
        EndDate      = m.EndDate,
        CourseCount  = m.CourseCount,
        Courses      = m.Courses?.Select(c => new CourseResponse
        {
            CourseId   = c.CourseId,
            CourseName = c.CourseName,
            SemesterId = c.SemesterId
        }).ToList()
    };

    public static CourseResponse ToResponse(CourseBusinessModel m) => new()
    {
        CourseId        = m.CourseId,
        CourseName      = m.CourseName,
        SemesterId      = m.SemesterId,
        EnrollmentCount = m.EnrollmentCount,
        Semester        = m.Semester == null ? null : ToResponse(m.Semester),
        Enrollments     = m.Enrollments?.Select(e => new EnrollmentResponse
        {
            EnrollmentId = e.EnrollmentId,
            StudentId    = e.StudentId,
            CourseId     = e.CourseId,
            EnrollDate   = e.EnrollDate,
            Status       = e.Status,
            Student      = e.Student == null ? null : new StudentResponse
            {
                StudentId   = e.Student.StudentId,
                FullName    = e.Student.FullName,
                Email       = e.Student.Email,
                DateOfBirth = e.Student.DateOfBirth
            }
        }).ToList()
    };

    public static SubjectResponse ToResponse(SubjectBusinessModel m) => new()
    {
        SubjectId   = m.SubjectId,
        SubjectCode = m.SubjectCode,
        SubjectName = m.SubjectName,
        Credit      = m.Credit
    };

    public static StudentResponse ToResponse(StudentBusinessModel m) => new()
    {
        StudentId       = m.StudentId,
        FullName        = m.FullName,
        Email           = m.Email,
        DateOfBirth     = m.DateOfBirth,
        EnrollmentCount = m.EnrollmentCount,
        Enrollments     = m.Enrollments?.Select(e => new EnrollmentResponse
        {
            EnrollmentId = e.EnrollmentId,
            StudentId    = e.StudentId,
            CourseId     = e.CourseId,
            EnrollDate   = e.EnrollDate,
            Status       = e.Status,
            Course       = e.Course == null ? null : new CourseResponse
            {
                CourseId   = e.Course.CourseId,
                CourseName = e.Course.CourseName,
                SemesterId = e.Course.SemesterId,
                Semester   = e.Course.Semester == null ? null : new SemesterResponse
                {
                    SemesterId   = e.Course.Semester.SemesterId,
                    SemesterName = e.Course.Semester.SemesterName,
                    StartDate    = e.Course.Semester.StartDate,
                    EndDate      = e.Course.Semester.EndDate
                }
            }
        }).ToList()
    };

    public static EnrollmentResponse ToResponse(EnrollmentBusinessModel m) => new()
    {
        EnrollmentId = m.EnrollmentId,
        StudentId    = m.StudentId,
        CourseId     = m.CourseId,
        EnrollDate   = m.EnrollDate,
        Status       = m.Status,
        Student      = m.Student == null ? null : new StudentResponse
        {
            StudentId   = m.Student.StudentId,
            FullName    = m.Student.FullName,
            Email       = m.Student.Email,
            DateOfBirth = m.Student.DateOfBirth
        },
        Course = m.Course == null ? null : new CourseResponse
        {
            CourseId   = m.Course.CourseId,
            CourseName = m.Course.CourseName,
            SemesterId = m.Course.SemesterId,
            Semester   = m.Course.Semester == null ? null : new SemesterResponse
            {
                SemesterId   = m.Course.Semester.SemesterId,
                SemesterName = m.Course.Semester.SemesterName,
                StartDate    = m.Course.Semester.StartDate,
                EndDate      = m.Course.Semester.EndDate
            }
        }
    };
}
